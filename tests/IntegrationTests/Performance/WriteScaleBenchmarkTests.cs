using System.Diagnostics;
using Application.Abstractions.Idempotency;
using Application.Abstractions.Posting;
using Domain.Common;
using Domain.DocumentLines;
using Domain.Materials;
using Domain.WarehouseDocuments;
using Infrastructure.Database;
using IntegrationTests.Regression;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SharedKernel;
using Xunit.Abstractions;

namespace IntegrationTests.Performance;

/// <summary>
/// Measures the real posting coordinator and PostgreSQL transaction, rather than an HTTP route.
/// Setup (catalog rows, submitted document, attachment and warm-up) is deliberately outside the
/// measured window. This is evidence for posting scale only, not end-to-end API latency.
/// </summary>
[Collection(nameof(IntegrationTestCollection))]
public sealed class WriteScaleBenchmarkTests(
    IntegrationTestWebAppFactory factory,
    ITestOutputHelper output)
{
    [ExplicitWriteScaleBenchmarkFact]
    [Trait("Category", "Performance")]
    [Trait("WorkloadClass", PerformanceWorkloadContracts.NormalExpectedTraffic)]
    public async Task MeasureBoundedDocumentPostingScaleAsync()
    {
        var configuration = WriteScaleBenchmarkConfiguration.FromEnvironment();
        int totalLines = configuration.WarmupOperations + configuration.Scenarios.Sum(scenario => scenario.LineCount);
        int totalInventoryKeys = configuration.WarmupOperations + configuration.Scenarios.Sum(scenario => scenario.InventoryKeyCount);
        totalLines.ShouldBeLessThanOrEqualTo(configuration.MaximumTotalDocumentLines);
        totalInventoryKeys.ShouldBeLessThanOrEqualTo(configuration.MaximumTotalInventoryKeys);

        RegressionSeedData seed = await RegressionTestHelper.SeedAsync(factory.Services);
        await RunWarmupAsync(seed);

        var results = new List<WriteScaleBenchmarkResult>(configuration.Scenarios.Count);
        foreach (WriteScaleScenario scenario in configuration.Scenarios)
        {
            WriteScaleBenchmarkResult result = await MeasureScenarioAsync(seed, scenario);
            results.Add(result);
            output.WriteLine($"posting scale: scenario={result.Scenario}, lines={result.LineCount}, keys={result.InventoryKeyCount}, " +
                             $"transaction_wall={result.TransactionWallClockMilliseconds:F1}ms, sql={result.Sql.Count}, " +
                             $"sql_total={result.Sql.TotalDurationMs:F1}ms, lock_samples={result.SampledLockWaitOccupancy.SampleCount}");
        }

        bool correct = results.All(result => result.ResultVerificationPassed);
        var artifact = new WriteScaleBenchmarkArtifact(
            correct ? "passed" : "failed_result_verification",
            PerformanceWorkloadContracts.NormalExpectedTraffic,
            configuration.Profile.ToString(),
            new WriteScaleBenchmarkSafetyBudget(
                configuration.MaximumTotalDocumentLines,
                configuration.MaximumTotalInventoryKeys,
                WriteScaleBenchmarkConfiguration.MaximumLinesPerScenario,
                WriteScaleBenchmarkConfiguration.MaximumInventoryKeysPerScenario,
                "QuickValidation measures one small consumable receiving post only. Scale requires a separate opt-in and is not production-load evidence."),
            new WriteScaleBenchmarkScope(
                "real application posting coordinator plus PostgreSQL Testcontainers transaction",
                "No Neon or production connection is accepted: the shared integration factory owns a disposable postgres:17-alpine container.",
                "HTTP, authentication, authorization and document creation/submission are intentionally excluded from transaction wall time.",
                "Lock values are sampled pg_stat_activity occupancy, never exact lock duration or per-request attribution.",
                "CPU, RSS and allocations are process-wide deltas for each window, not isolated per-transaction attribution."),
            results,
            "Artifacts intentionally omit document/user/material/warehouse IDs, connection strings, SQL text, parameters, request hashes and secrets.");

        string path = await ApiLoadSuiteArtifactWriter.WriteAsync(
            configuration.ResultDirectory,
            $"write-scale-{DateTimeOffset.UtcNow:yyyyMMddHHmmssfff}-{Guid.NewGuid():N}.json",
            artifact);
        output.WriteLine($"write-scale benchmark result: {path}");
        correct.ShouldBeTrue();
    }

    private async Task RunWarmupAsync(RegressionSeedData seed)
    {
        WarehouseDocument document = await CreateSubmittedReceivingAsync(seed, new WriteScaleScenario("warmup", 1, 1));
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        IDocumentPostingCoordinator coordinator = scope.ServiceProvider.GetRequiredService<IDocumentPostingCoordinator>();
        Result<PostingOutcome> result = await coordinator.PostAsync(
            document.Id,
            document.RowVersion,
            seed.ManagerUserId,
            IdempotencyRequest.Create(Guid.NewGuid(), "write-scale-benchmark.warmup", "warmup"),
            CancellationToken.None);
        result.IsSuccess.ShouldBeTrue();
    }

    private async Task<WriteScaleBenchmarkResult> MeasureScenarioAsync(
        RegressionSeedData seed,
        WriteScaleScenario scenario)
    {
        WarehouseDocument document = await CreateSubmittedReceivingAsync(seed, scenario);
        IReadOnlyDictionary<WriteScaleInventoryKey, decimal> balanceBaseline =
            await CaptureBalanceBaselineAsync(document);
        SqlCommandCounterInterceptor counter = factory.Services.GetRequiredService<SqlCommandCounterInterceptor>();
        await using var lockSampler = new PostgreSqlLockWaitSampler(factory.DatabaseConnectionString);
        counter.Reset();
        await lockSampler.StartAsync();
        ApiBenchmarkProcessSnapshot before = ApiLatencyBenchmarkMetrics.CaptureProcessSnapshot();
        var stopwatch = Stopwatch.StartNew();
        Result<PostingOutcome> posting;
        try
        {
            await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
            IDocumentPostingCoordinator coordinator = scope.ServiceProvider.GetRequiredService<IDocumentPostingCoordinator>();
            posting = await coordinator.PostAsync(
                document.Id,
                document.RowVersion,
                seed.ManagerUserId,
                IdempotencyRequest.Create(Guid.NewGuid(), "write-scale-benchmark.post", scenario.Name),
                CancellationToken.None);
        }
        finally
        {
            stopwatch.Stop();
        }
        ApiBenchmarkProcessSnapshot after = ApiLatencyBenchmarkMetrics.CaptureProcessSnapshot();
        PostgreSqlLockWaitSnapshot lockWait = await lockSampler.StopAsync();
        SqlCommandDurationSnapshot sql = counter.Snapshot();

        posting.IsSuccess.ShouldBeTrue();
        bool verification = await VerifyPostedResultAsync(document.Id, scenario, balanceBaseline);
        return new WriteScaleBenchmarkResult(
            scenario.Name,
            scenario.LineCount,
            scenario.InventoryKeyCount,
            stopwatch.Elapsed.TotalMilliseconds,
            sql,
            ApiLatencyBenchmarkMetrics.CalculateDelta(before, after),
            lockWait,
            verification);
    }

    private async Task<WarehouseDocument> CreateSubmittedReceivingAsync(
        RegressionSeedData seed,
        WriteScaleScenario scenario)
    {
        IReadOnlyList<Guid> materialIds = await CreateConsumableMaterialsAsync(seed, scenario.InventoryKeyCount);
        (Guid MaterialId, DocumentLineType LineType, decimal Quantity)[] lines = Enumerable.Range(0, scenario.LineCount)
            .Select(index => (materialIds[index % materialIds.Count], DocumentLineType.Normal, 1m))
            .ToArray();
        return await RegressionTestHelper.CreateAndSubmitDocumentAsync(
            factory.Services,
            seed.WarehouseId,
            DocumentType.Receiving,
            seed.KeeperUserId,
            lines);
    }

    private async Task<IReadOnlyList<Guid>> CreateConsumableMaterialsAsync(RegressionSeedData seed, int count)
    {
        // The first key reuses the regular seed material; remaining keys are unique active
        // consumables in the same permitted family/capability. Catalog setup is not measured.
        var materialIds = new List<Guid>(count) { seed.NormalMaterialId };
        if (count == 1)
        {
            return materialIds;
        }

        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        for (int index = 1; index < count; index++)
        {
            string suffix = Guid.NewGuid().ToString("N");
            var material = Material.Create(
                Guid.NewGuid(), seed.FamilyId, seed.UnitOfMeasureId,
                $"Scale consumable {index}", $"Scale consumable {index}",
                $"S{suffix}"[..12], MaterialKind.Consumable, TrackingType.Quantity, false, null);
            context.Materials.Add(material);
            materialIds.Add(material.Id);
        }
        await context.SaveChangesAsync();
        return materialIds;
    }

    private async Task<IReadOnlyDictionary<WriteScaleInventoryKey, decimal>> CaptureBalanceBaselineAsync(
        WarehouseDocument document)
    {
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Guid[] materialIds = await context.DocumentLines
            .Where(item => item.DocumentId == document.Id)
            .Select(item => item.MaterialId)
            .Distinct()
            .ToArrayAsync();
        Dictionary<WriteScaleInventoryKey, decimal> existingBalances = await context.InventoryBalances
            .Where(item => item.WarehouseId == document.WarehouseId && materialIds.Contains(item.MaterialId))
            .ToDictionaryAsync(
                item => new WriteScaleInventoryKey(item.WarehouseId, item.MaterialId),
                item => item.Quantity);

        return materialIds.ToDictionary(
            materialId => new WriteScaleInventoryKey(document.WarehouseId, materialId),
            materialId => existingBalances.TryGetValue(
                new WriteScaleInventoryKey(document.WarehouseId, materialId),
                out decimal balance)
                ? balance
                : 0m);
    }

    private async Task<bool> VerifyPostedResultAsync(
        Guid documentId,
        WriteScaleScenario scenario,
        IReadOnlyDictionary<WriteScaleInventoryKey, decimal> balanceBaseline)
    {
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        WarehouseDocument document = await context.WarehouseDocuments.SingleAsync(item => item.Id == documentId);
        int movements = await context.StockMovements.CountAsync(item => item.DocumentId == documentId);
        int movementAudits = await context.AuditLogs.CountAsync(item =>
            item.EntityType == "StockMovement" && item.AggregateId == documentId);
        int assets = await (
            from asset in context.Assets
            join line in context.DocumentLines on asset.ReceiptLineId equals line.Id
            where line.DocumentId == documentId
            select asset.Id).CountAsync();
        Dictionary<WriteScaleInventoryKey, decimal> movementTotals = await context.StockMovements
            .Where(item => item.DocumentId == documentId)
            .GroupBy(item => new { item.WarehouseId, item.MaterialId })
            .ToDictionaryAsync(
                group => new WriteScaleInventoryKey(group.Key.WarehouseId, group.Key.MaterialId),
                group => group.Sum(item => item.QuantityDelta));
        Dictionary<WriteScaleInventoryKey, decimal> finalBalances = await context.InventoryBalances
            .Where(item => item.WarehouseId == document.WarehouseId)
            .ToDictionaryAsync(
                item => new WriteScaleInventoryKey(item.WarehouseId, item.MaterialId),
                item => item.Quantity);
        bool balancesChangedExactly = WriteScaleBalanceVerification.HasExactPostingEffects(
            balanceBaseline, finalBalances, movementTotals, scenario.InventoryKeyCount);
        return document.DocumentStatus == DocumentStatus.Posted &&
               movements == scenario.LineCount &&
               movementAudits == scenario.LineCount &&
               assets == 0 &&
               balancesChangedExactly;
    }
}

internal sealed record WriteScaleBenchmarkResult(
    string Scenario,
    int LineCount,
    int InventoryKeyCount,
    double TransactionWallClockMilliseconds,
    SqlCommandDurationSnapshot Sql,
    ApiBenchmarkProcessDelta ProcessDelta,
    PostgreSqlLockWaitSnapshot SampledLockWaitOccupancy,
    bool ResultVerificationPassed);

internal readonly record struct WriteScaleInventoryKey(Guid WarehouseId, Guid MaterialId);

internal static class WriteScaleBalanceVerification
{
    internal static bool HasExactPostingEffects(
        IReadOnlyDictionary<WriteScaleInventoryKey, decimal> baseline,
        IReadOnlyDictionary<WriteScaleInventoryKey, decimal> final,
        IReadOnlyDictionary<WriteScaleInventoryKey, decimal> movementDeltas,
        int expectedInventoryKeyCount)
    {
        ArgumentNullException.ThrowIfNull(baseline);
        ArgumentNullException.ThrowIfNull(final);
        ArgumentNullException.ThrowIfNull(movementDeltas);

        return expectedInventoryKeyCount > 0 &&
               baseline.Count == expectedInventoryKeyCount &&
               movementDeltas.Count == expectedInventoryKeyCount &&
               movementDeltas.All(pair =>
                   baseline.TryGetValue(pair.Key, out decimal before) &&
                   final.TryGetValue(pair.Key, out decimal after) &&
                   after - before == pair.Value);
    }
}

internal sealed record WriteScaleBenchmarkArtifact(
    string Status,
    string WorkloadClass,
    string Profile,
    WriteScaleBenchmarkSafetyBudget SafetyBudget,
    WriteScaleBenchmarkScope Scope,
    IReadOnlyList<WriteScaleBenchmarkResult> Results,
    string DataHandling);

internal sealed record WriteScaleBenchmarkSafetyBudget(
    int MaximumTotalDocumentLines,
    int MaximumTotalInventoryKeys,
    int MaximumLinesPerScenario,
    int MaximumInventoryKeysPerScenario,
    string EvidenceLimit);

internal sealed record WriteScaleBenchmarkScope(
    string MeasuredPath,
    string DatabaseSafety,
    string Exclusions,
    string LockInterpretation,
    string ResourceInterpretation);

[AttributeUsage(AttributeTargets.Method)]
internal sealed class ExplicitWriteScaleBenchmarkFactAttribute : FactAttribute
{
    public ExplicitWriteScaleBenchmarkFactAttribute()
    {
        if (!string.Equals(Environment.GetEnvironmentVariable(WriteScaleBenchmarkConfiguration.GateEnvironmentVariable), "1", StringComparison.Ordinal))
        {
            Skip = "Explicit write-scale benchmark. Set RUN_WRITE_SCALE_BENCHMARK=1; Scale additionally requires EIAMS_ALLOW_LONG_WRITE_SCALE_BENCHMARK=1.";
        }
    }
}
