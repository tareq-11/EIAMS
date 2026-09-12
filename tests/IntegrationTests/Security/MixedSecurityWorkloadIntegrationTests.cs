using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Application.Abstractions.Authentication;
using Domain.Common;
using Domain.Permissions;
using Domain.Roles;
using Domain.UserRoleScopes;
using Domain.Warehouses;
using Infrastructure.Database;
using IntegrationTests.Regression;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IntegrationTests.Security;

/// <summary>Bounded Quick mixed-load correctness test; it is not representative of production load.</summary>
[Collection(nameof(IntegrationTestCollection))]
public sealed class MixedSecurityWorkloadIntegrationTests : BaseIntegrationTest
{
    private const int IterationsPerHostPerPhase = 8;
    private static readonly TimeSpan WorkloadTimeout = TimeSpan.FromSeconds(15);
    private readonly IntegrationTestWebAppFactory factory;

    public MixedSecurityWorkloadIntegrationTests(IntegrationTestWebAppFactory factory) : base(factory)
    {
        this.factory = factory;
    }

    [Fact]
    public async Task QuickMixedSecurityWorkload_ShouldDenyBolaReplayAndStaleScopeAcrossTwoHosts()
    {
        RegressionSeedData seed = await RegressionTestHelper.SeedAsync(factory.Services);
        Guid permanentlyOutsideWarehouseId = await CreatePermanentlyOutsideWarehouseAsync(seed);
        (Guid userId, AccessTokens originalTokens) = await RegisterAndLoginAsync();
        Guid roleId = await GrantWarehouseReadAndManageAsync(userId, seed.WarehouseId);

        using IntegrationTestWebAppFactory.BenchmarkProfiledWebAppFactory sibling = factory.CreateSiblingFactory();
        using HttpClient primary = factory.CreateClient();
        using HttpClient secondary = sibling.CreateClient();
        primary.BaseAddress = new Uri("http://localhost/api/v1/");
        secondary.BaseAddress = new Uri("http://localhost/api/v1/");
        primary.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", originalTokens.AccessToken);
        secondary.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", originalTokens.AccessToken);

        // Warm both independent authorization caches through the HTTP boundary.
        using (HttpResponseMessage primaryWarmup = await primary.GetAsync($"warehouses/{seed.WarehouseId}"))
        {
            primaryWarmup.StatusCode.ShouldBe(HttpStatusCode.OK);
        }

        using (HttpResponseMessage secondaryWarmup = await secondary.GetAsync($"warehouses/{seed.WarehouseId}"))
        {
            secondaryWarmup.StatusCode.ShouldBe(HttpStatusCode.OK);
        }

        var bothWorkersAtBarrier = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseWorkersAfterScopeReplacement = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        int workersAtBarrier = 0;

        void ArriveAtBarrier()
        {
            if (Interlocked.Increment(ref workersAtBarrier) == 2)
            {
                bothWorkersAtBarrier.TrySetResult();
            }
        }

#pragma warning disable CA2025 // Both tasks are awaited before either HTTP client is disposed.
        Task<WorkerPhaseResult> primaryWorker = RunHostWorkloadAsync(
            primary,
            seed.WarehouseId,
            seed.DestinationWarehouseId,
            permanentlyOutsideWarehouseId,
            ArriveAtBarrier,
            releaseWorkersAfterScopeReplacement.Task,
            new WarehouseWrite(seed.DestinationWarehouseId, Status.Inactive, 1, HttpStatusCode.OK),
            new WarehouseWrite(permanentlyOutsideWarehouseId, Status.Inactive, 1, HttpStatusCode.Forbidden));
        Task<WorkerPhaseResult> secondaryWorker = RunHostWorkloadAsync(
            secondary,
            seed.WarehouseId,
            seed.DestinationWarehouseId,
            permanentlyOutsideWarehouseId,
            ArriveAtBarrier,
            releaseWorkersAfterScopeReplacement.Task,
            new WarehouseWrite(seed.WarehouseId, Status.Active, 2, HttpStatusCode.Forbidden));

        await bothWorkersAtBarrier.Task.WaitAsync(WorkloadTimeout);

        Task<WorkloadResult> authorizedOldScopeWrite = SetWarehouseStatusAsync(primary, seed.WarehouseId, Status.Inactive, 1);
        Task<WorkloadResult> deniedPermanentOutsideWrite = SetWarehouseStatusAsync(
            secondary, permanentlyOutsideWarehouseId, Status.Inactive, 1);
        WorkloadResult[] beforeReplacementWrites = await Task.WhenAll(authorizedOldScopeWrite, deniedPermanentOutsideWrite)
            .WaitAsync(WorkloadTimeout);
        beforeReplacementWrites.Select(result => result.StatusCode)
            .ShouldBe([HttpStatusCode.OK, HttpStatusCode.Forbidden]);

        long versionBeforeReplacement = await ReadAuthorizationVersionAsync();
        await ReplaceScopeAsync(userId, roleId, seed.DestinationWarehouseId);
        long versionAfterReplacement = await ReadAuthorizationVersionAsync();
        versionAfterReplacement.ShouldBeGreaterThan(versionBeforeReplacement);

        releaseWorkersAfterScopeReplacement.TrySetResult();
        WorkerPhaseResult[] workload = await Task.WhenAll(primaryWorker, secondaryWorker).WaitAsync(WorkloadTimeout);
#pragma warning restore CA2025

        workload.Sum(result => result.BeforeReplacementReads).ShouldBe(2 * IterationsPerHostPerPhase * 3);
        workload.Sum(result => result.AfterReplacementReads).ShouldBe(2 * IterationsPerHostPerPhase * 3);
        workload.Sum(result => result.Writes).ShouldBe(3);
        await AssertWarehouseStatesAsync(seed.WarehouseId, seed.DestinationWarehouseId, permanentlyOutsideWarehouseId);

        RefreshReplayResult replay = await RotateAndReplayAcrossHostsAsync(primary, secondary, originalTokens.RefreshToken);
        replay.SuccessfulRotations.ShouldBe(1);
        replay.RejectedReplays.ShouldBe(1);
        replay.RejectedFamilyReuse.ShouldBe(1);

    }

    private async Task<Guid> CreatePermanentlyOutsideWarehouseAsync(RegressionSeedData seed)
    {
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var warehouseId = Guid.NewGuid();
        context.Warehouses.Add(Warehouse.Create(
            warehouseId,
            seed.SiteId,
            $"Outside warehouse {warehouseId:N}",
            $"OUT-{warehouseId:N}"[..20],
            "Isolation",
            true,
            seed.OrgUnitId));
        await context.SaveChangesAsync();
        return warehouseId;
    }

    private async Task<Guid> GrantWarehouseReadAndManageAsync(Guid userId, Guid warehouseId)
    {
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var roleId = Guid.NewGuid();
        context.AddRange(
            Role.Create(roleId, $"Mixed workload {roleId:N}", null),
            RolePermission.Create(roleId, WellKnownPermissions.WarehousesViewId),
            RolePermission.Create(roleId, WellKnownPermissions.WarehousesManageId),
            UserRoleScope.Create(Guid.NewGuid(), userId, roleId, ScopeType.Warehouse, warehouseId));
        await context.SaveChangesAsync();
        return roleId;
    }

    private async Task ReplaceScopeAsync(Guid userId, Guid roleId, Guid replacementWarehouseId)
    {
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        UserRoleScope assignment = await context.UserRoleScopes.SingleAsync(item => item.UserId == userId);
        assignment.ReplaceAssignment(roleId, ScopeType.Warehouse, replacementWarehouseId);
        await context.SaveChangesAsync();
    }

    private async Task<long> ReadAuthorizationVersionAsync()
    {
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await context.Database
            .SqlQueryRaw<long>("SELECT version AS \"Value\" FROM public.authorization_versions WHERE id = 1")
            .SingleAsync();
    }

    private static async Task<WorkloadResult> GetStatusAsync(HttpClient client, string path)
    {
        using HttpResponseMessage response = await client.GetAsync(path);
        return new WorkloadResult(response.StatusCode);
    }

    private static async Task<WorkloadResult> SetWarehouseStatusAsync(
        HttpClient client,
        Guid warehouseId,
        Status status,
        int expectedRowVersion)
    {
        using HttpResponseMessage response = await client.PutAsJsonAsync($"warehouses/{warehouseId}/status", new
        {
            status = (int)status,
            expectedRowVersion
        });
        return new WorkloadResult(response.StatusCode);
    }

    private async Task<RefreshReplayResult> RotateAndReplayAcrossHostsAsync(
        HttpClient primary,
        HttpClient secondary,
        string refreshToken)
    {
        string tokenHash;
        await using (AsyncServiceScope scope = factory.Services.CreateAsyncScope())
        {
            tokenHash = scope.ServiceProvider.GetRequiredService<ITokenProvider>().HashRefreshToken(refreshToken);
        }

        Task<HttpResponseMessage>[] requests;
        await using (IntegrationTestWebAppFactory.PostgresAdvisoryLockLease barrier =
                     await factory.HoldApplicationLockAsync($"security:refresh-token:{tokenHash}"))
        {
            Task<HttpResponseMessage> first = primary.PostAsJsonAsync("auth/refresh", new { refreshToken });
            await barrier.WaitUntilContendedAsync();
            Task<HttpResponseMessage> replay = secondary.PostAsJsonAsync("auth/refresh", new { refreshToken });
            requests = [first, replay];
            await barrier.ReleaseAsync();
        }

        HttpResponseMessage[] responses = await Task.WhenAll(requests).WaitAsync(WorkloadTimeout);
        int successes = responses.Count(response => response.StatusCode == HttpStatusCode.OK);
        int rejected = responses.Count(response => response.StatusCode == HttpStatusCode.BadRequest);
        HttpResponseMessage success = responses.Single(response => response.StatusCode == HttpStatusCode.OK);
        ApiEnvelope<AccessTokens>? body = await success.Content.ReadFromJsonAsync<ApiEnvelope<AccessTokens>>();
        body.ShouldNotBeNull();

        using HttpResponseMessage familyReuse = await primary.PostAsJsonAsync(
            "auth/refresh", new { refreshToken = body.Data.RefreshToken });
        foreach (HttpResponseMessage response in responses)
        {
            response.Dispose();
        }

        return new RefreshReplayResult(successes, rejected, familyReuse.StatusCode == HttpStatusCode.BadRequest ? 1 : 0);
    }

    private async Task AssertWarehouseStatesAsync(
        Guid oldWarehouseId,
        Guid replacementWarehouseId,
        Guid permanentlyOutsideWarehouseId)
    {
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Warehouse[] warehouses = await context.Warehouses
            .Where(item => item.Id == oldWarehouseId ||
                           item.Id == replacementWarehouseId ||
                           item.Id == permanentlyOutsideWarehouseId)
            .ToArrayAsync();
        warehouses.Single(item => item.Id == oldWarehouseId).ShouldSatisfyAllConditions(
            warehouse => warehouse.Status.ShouldBe(Status.Inactive),
            warehouse => warehouse.RowVersion.ShouldBe(2));
        warehouses.Single(item => item.Id == replacementWarehouseId).ShouldSatisfyAllConditions(
            warehouse => warehouse.Status.ShouldBe(Status.Inactive),
            warehouse => warehouse.RowVersion.ShouldBe(2));
        warehouses.Single(item => item.Id == permanentlyOutsideWarehouseId).ShouldSatisfyAllConditions(
            warehouse => warehouse.Status.ShouldBe(Status.Active),
            warehouse => warehouse.RowVersion.ShouldBe(1));
    }

    private static async Task<WorkerPhaseResult> RunHostWorkloadAsync(
        HttpClient client,
        Guid oldWarehouseId,
        Guid replacementWarehouseId,
        Guid permanentlyOutsideWarehouseId,
        Action arriveAtBarrier,
        Task releaseAfterScopeReplacement,
        params WarehouseWrite[] postReplacementWrites)
    {
        for (int iteration = 0; iteration < IterationsPerHostPerPhase; iteration++)
        {
            await AssertReadStatusAsync(client, oldWarehouseId, HttpStatusCode.OK);
            await AssertReadStatusAsync(client, replacementWarehouseId, HttpStatusCode.Forbidden);
            await AssertReadStatusAsync(client, permanentlyOutsideWarehouseId, HttpStatusCode.Forbidden);
        }

        arriveAtBarrier();
        await releaseAfterScopeReplacement.WaitAsync(WorkloadTimeout);

        for (int iteration = 0; iteration < IterationsPerHostPerPhase; iteration++)
        {
            await AssertReadStatusAsync(client, oldWarehouseId, HttpStatusCode.Forbidden);
            await AssertReadStatusAsync(client, replacementWarehouseId, HttpStatusCode.OK);
            await AssertReadStatusAsync(client, permanentlyOutsideWarehouseId, HttpStatusCode.Forbidden);
        }

        foreach (WarehouseWrite write in postReplacementWrites)
        {
            (await SetWarehouseStatusAsync(client, write.WarehouseId, write.Status, write.ExpectedRowVersion))
                .StatusCode.ShouldBe(write.ExpectedStatusCode);
        }

        return new WorkerPhaseResult(
            IterationsPerHostPerPhase * 3,
            IterationsPerHostPerPhase * 3,
            postReplacementWrites.Length);
    }

    private static async Task AssertReadStatusAsync(HttpClient client, Guid warehouseId, HttpStatusCode expectedStatusCode)
    {
        WorkloadResult result = await GetStatusAsync(client, $"warehouses/{warehouseId}");
        result.StatusCode.ShouldBe(expectedStatusCode);
    }

    private sealed record WorkloadResult(HttpStatusCode StatusCode);
    private sealed record WarehouseWrite(Guid WarehouseId, Status Status, int ExpectedRowVersion, HttpStatusCode ExpectedStatusCode);
    private sealed record WorkerPhaseResult(int BeforeReplacementReads, int AfterReplacementReads, int Writes);
    private sealed record RefreshReplayResult(int SuccessfulRotations, int RejectedReplays, int RejectedFamilyReuse);
}
