using System.Data.Common;
using Application.Abstractions.PolymorphicReferences;
using Domain.Common;
using Domain.Employees;
using Domain.IssueTos;
using Domain.OrganizationalUnits;
using Domain.Organizations;
using Domain.Sites;
using Domain.WarehouseDocuments;
using Domain.Warehouses;
using Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using SharedKernel;

namespace IntegrationTests.M9;

[Collection(nameof(IntegrationTestCollection))]
public sealed class M9DatabaseGuardTests : BaseIntegrationTest
{
    private readonly IntegrationTestWebAppFactory factory;

    public M9DatabaseGuardTests(IntegrationTestWebAppFactory factory) : base(factory)
    {
        this.factory = factory;
    }

    [Fact]
    public async Task IssueToTrigger_Should_RejectMissingRecipient()
    {
        // Arrange
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        WarehouseDocument document = await SeedDocumentAsync(context);
        var missingRecipientId = Guid.NewGuid();
        const string employeeType = "Employee";
        const string issueReason = "M9 invalid recipient";

        // Act
        PostgresException exception = await Should.ThrowAsync<PostgresException>(() =>
            context.Database.ExecuteSqlInterpolatedAsync(
                $"""
                INSERT INTO public.issue_to
                    (document_id, recipient_type, recipient_id, issue_reason, created_at_utc)
                VALUES
                    ({document.Id}, {employeeType}, {missingRecipientId}, {issueReason}, {DateTime.UtcNow})
                """));

        // Assert
        exception.SqlState.ShouldBe(PostgresErrorCodes.CheckViolation);
        exception.ConstraintName.ShouldBe("trg_validate_recipient");
        (await context.IssueTos.AnyAsync(item => item.Id == document.Id)).ShouldBeFalse();
    }

    [Fact]
    public async Task CustodyTrigger_Should_RejectUnsupportedExternalHolder()
    {
        // Arrange
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var custodyId = Guid.NewGuid();
        const string externalType = "External";
        const string operationalKind = "Operational";
        const string activeStatus = "Active";

        // Act
        PostgresException exception = await Should.ThrowAsync<PostgresException>(() =>
            context.Database.ExecuteSqlInterpolatedAsync(
                $"""
                INSERT INTO public.custodies
                    (id, asset_id, holder_type, holder_id, custody_kind, issue_document_id,
                     status, from_utc, row_version, created_at_utc)
                VALUES
                    ({custodyId}, {Guid.NewGuid()}, {externalType}, {Guid.NewGuid()}, {operationalKind},
                     {Guid.NewGuid()}, {activeStatus}, {DateTime.UtcNow}, {1}, {DateTime.UtcNow})
                """));

        // Assert
        exception.SqlState.ShouldBe(PostgresErrorCodes.CheckViolation);
        exception.ConstraintName.ShouldBe("trg_validate_custody_holder");
        (await context.Custodies.AnyAsync(item => item.Id == custodyId)).ShouldBeFalse();
    }

    [Fact]
    public async Task Auditor_Should_ReportRecipientThatBecameInactiveAfterWrite()
    {
        // Arrange
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        (Employee employee, WarehouseDocument document) = await SeedValidIssueAsync(context);
        employee.SetStatus(Status.Inactive);
        await context.SaveChangesAsync();
        IPolymorphicReferenceAuditor auditor =
            scope.ServiceProvider.GetRequiredService<IPolymorphicReferenceAuditor>();

        // Act
        PolymorphicReferenceAuditResult result = await auditor.AuditAsync(10);

        // Assert
        result.SkippedBecauseLockUnavailable.ShouldBeFalse();
        result.TotalFindings.ShouldBeGreaterThanOrEqualTo(1);
        PolymorphicReferenceFinding finding = result.Findings
            .Where(item => item.SourceType == "IssueTo" && item.SourceId == document.Id)
            .ShouldHaveSingleItem();
        finding.PartyId.ShouldBe(employee.Id);
        finding.Reason.ShouldBe("Inactive");
    }

    [Fact]
    public async Task SiteConstraint_Should_RejectInvalidGovernorateCodeFromDirectSql()
    {
        // Arrange
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var organization = Organization.Create(Guid.NewGuid(), "M9 organization", $"O-{Guid.NewGuid():N}");
        var site = Site.Create(Guid.NewGuid(), organization.Id, "M9 site", $"S-{Guid.NewGuid():N}", null);
        const string invalidGovernorateCode = "invalid value";
        context.AddRange(organization, site);
        await context.SaveChangesAsync();

        // Act
        PostgresException exception = await Should.ThrowAsync<PostgresException>(() =>
            context.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE public.sites SET governorate_code = {invalidGovernorateCode} WHERE id = {site.Id}"));

        // Assert
        exception.SqlState.ShouldBe(PostgresErrorCodes.CheckViolation);
        exception.ConstraintName.ShouldBe("ck_sites_governorate_code_valid");
    }

    [Fact]
    public async Task DatabaseCatalog_Should_ContainCriticalIntegrityGuards()
    {
        // Arrange
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await context.Database.OpenConnectionAsync();
        DbConnection connection = context.Database.GetDbConnection();
        await using DbCommand command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT conname FROM pg_constraint WHERE connamespace = 'public'::regnamespace
            UNION ALL
            SELECT indexname FROM pg_indexes WHERE schemaname = 'public'
            UNION ALL
            SELECT tgname FROM pg_trigger
            WHERE NOT tgisinternal AND tgrelid IN ('public.issue_to'::regclass, 'public.custodies'::regclass)
            """;

        // Act
        var names = new HashSet<string>(StringComparer.Ordinal);
        await using DbDataReader reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            names.Add(reader.GetString(0));
        }

        // Assert
        string[] requiredNames =
        [
            "trg_validate_recipient",
            "trg_validate_custody_holder",
            "ck_inventory_balances_quantity_non_negative",
            "ck_warehouse_documents_posted_metadata",
            "ix_custodies_asset_id",
            "ix_inventory_balances_warehouse_id_material_id",
            "ix_stock_movements_document_id_line_id_movement_type",
            "ix_warehouse_capability_operations_capability_id_operation_type",
            "ix_assets_asset_number",
            "ix_warehouse_documents_system_reference_number"
        ];

        foreach (string requiredName in requiredNames)
        {
            names.ShouldContain(requiredName);
        }
    }

    private static async Task<WarehouseDocument> SeedDocumentAsync(ApplicationDbContext context)
    {
        string suffix = Guid.NewGuid().ToString("N");
        var organization = Organization.Create(Guid.NewGuid(), $"Organization {suffix}", $"O{suffix}");
        var site = Site.Create(Guid.NewGuid(), organization.Id, $"Site {suffix}", $"S{suffix}", null);
        var warehouse = Warehouse.Create(
            Guid.NewGuid(), site.Id, $"Warehouse {suffix}", $"W{suffix}", "Main", true);
        var document = WarehouseDocument.CreateDraft(
            Guid.NewGuid(), warehouse.Id, DocumentType.Issue, $"ISS-{suffix}");
        context.AddRange(organization, site, warehouse, document);
        await context.SaveChangesAsync();
        return document;
    }

    private static async Task<(Employee Employee, WarehouseDocument Document)> SeedValidIssueAsync(
        ApplicationDbContext context)
    {
        string suffix = Guid.NewGuid().ToString("N");
        var organization = Organization.Create(Guid.NewGuid(), $"Organization {suffix}", $"O{suffix}");
        var site = Site.Create(Guid.NewGuid(), organization.Id, $"Site {suffix}", $"S{suffix}", null);
        var organizationalUnit = OrganizationalUnit.Create(
            Guid.NewGuid(), site.Id, null, $"Unit {suffix}", "Department");
        var employee = Employee.Create(
            Guid.NewGuid(), organizationalUnit.Id, $"Employee {suffix}", $"E{suffix}", null);
        context.AddRange(organization, site, organizationalUnit, employee);
        await context.SaveChangesAsync();

        var warehouse = Warehouse.Create(
            Guid.NewGuid(), site.Id, $"Warehouse {suffix}", $"W{suffix}", "Main", true);
        var document = WarehouseDocument.CreateDraft(
            Guid.NewGuid(), warehouse.Id, DocumentType.Issue, $"ISS-{suffix}");
        Result<IssueTo> issueTo = IssueTo.Create(
            document.Id, PartyType.Employee, employee.Id, "Operational issue");
        issueTo.IsSuccess.ShouldBeTrue();
        context.AddRange(warehouse, document, issueTo.Value);
        await context.SaveChangesAsync();
        return (employee, document);
    }
}
