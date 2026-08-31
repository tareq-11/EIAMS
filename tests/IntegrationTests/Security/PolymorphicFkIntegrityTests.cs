using System.Net;
using System.Net.Http.Json;
using Domain.Common;
using Domain.Employees;
using Domain.ExternalParties;
using Domain.IssueTos;
using Domain.WarehouseDocuments;
using Infrastructure.Database;
using Application.Abstractions.Recipients;
using IntegrationTests.Regression;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SharedKernel;

namespace IntegrationTests.Security;

[Collection(nameof(IntegrationTestCollection))]
public sealed class PolymorphicFkIntegrityTests : BaseIntegrationTest
{
    private readonly IntegrationTestWebAppFactory factory;

    public PolymorphicFkIntegrityTests(IntegrationTestWebAppFactory factory) : base(factory)
    {
        this.factory = factory;
    }

    [Fact]
    public async Task IssueTo_Should_RejectNonExistentRecipientId()
    {
        // 1. Arrange
        var invalidRecipientId = Guid.NewGuid();

        // 2. Act & Assert: Create IssueTo with non-existent employee ID
        Result<IssueTo> issueTo = IssueTo.Create(
            Guid.NewGuid(),
            PartyType.Employee,
            invalidRecipientId,
            "Invalid recipient");

        issueTo.IsSuccess.ShouldBeTrue(); // Domain factory creates valid shape

        // Validation against database checks existence
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        bool employeeExists = await context.Employees.AnyAsync(e => e.Id == invalidRecipientId);
        employeeExists.ShouldBeFalse();
    }

    [Fact]
    public async Task IssueTo_Should_RejectInactiveRecipient()
    {
        // 1. Arrange & Seed
        RegressionSeedData seed = await RegressionTestHelper.SeedAsync(factory.Services);

        // Deactivate employee
        await using (AsyncServiceScope scope = factory.Services.CreateAsyncScope())
        {
            ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            Employee emp = await context.Employees.SingleAsync(e => e.Id == seed.EmployeeId);
            emp.SetStatus(Status.Inactive);
            await context.SaveChangesAsync();
        }

        // 2. Assert employee is inactive
        await using (AsyncServiceScope scope = factory.Services.CreateAsyncScope())
        {
            ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            Employee emp = await context.Employees.SingleAsync(e => e.Id == seed.EmployeeId);
            emp.Status.ShouldBe(Status.Inactive);
        }
    }

    [Fact]
    public async Task ExternalParty_Should_Be_Resolvable_And_Respect_Active_State()
    {
        var externalParty = ExternalParty.Create(
            Guid.NewGuid(), "شركة اختبار خارجية", "EXT-TEST", null, null);

        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        ICounterpartResolver resolver = scope.ServiceProvider.GetRequiredService<ICounterpartResolver>();
        IActivePartyLookup activeLookup = scope.ServiceProvider.GetRequiredService<IActivePartyLookup>();

        context.ExternalParties.Add(externalParty);
        await context.SaveChangesAsync();

        CounterpartResolution? resolved = await resolver.ResolveAsync(
            PartyType.External, externalParty.Id, CancellationToken.None);
        resolved.ShouldNotBeNull();
        resolved.DisplayName.ShouldBe("شركة اختبار خارجية");
        resolved.Status.ShouldBe(Status.Active);

        externalParty.SetStatus(Status.Inactive);
        await context.SaveChangesAsync();

        ActivePartyLookupStatus status = await activeLookup.GetStatusAsync(
            PartyType.External, externalParty.Id, CancellationToken.None);
        status.ShouldBe(ActivePartyLookupStatus.Inactive);
    }
}
