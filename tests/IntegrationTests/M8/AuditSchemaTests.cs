using Application.Abstractions.Authorization;
using Domain.AuditLogs;
using Domain.Permissions;
using Domain.Roles;
using Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using SharedKernel;

namespace IntegrationTests.M8;

[Collection(nameof(IntegrationTestCollection))]
public sealed class AuditSchemaTests : BaseIntegrationTest
{
    private static readonly string[] ExpectedAuditLogColumns =
    [
        "id",
        "operation_id",
        "request_id",
        "user_id",
        "entity_type",
        "entity_id",
        "aggregate_type",
        "aggregate_id",
        "action",
        "command_name",
        "summary",
        "ip_address",
        "created_at_utc"
    ];

    private static readonly string[] ExpectedAuditLogEntryColumns =
    [
        "id",
        "audit_log_id",
        "field_name",
        "old_value",
        "new_value"
    ];

    private static readonly string[] ExpectedAuditLogIndexes =
    [
        "ix_audit_logs_entity_type_entity_id_created_at_utc_id",
        "ix_audit_logs_aggregate_type_aggregate_id_created_at_utc_id",
        "ix_audit_logs_user_id_created_at_utc_id",
        "ix_audit_logs_operation_id_created_at_utc_id",
        "ix_audit_logs_request_id",
        "ix_audit_logs_created_at_utc_id"
    ];

    private static readonly string[] ExpectedAuditLogEntryIndexes =
    [
        "ix_audit_log_entries_audit_log_id_field_name_id",
        "ix_audit_log_entries_field_name_audit_log_id"
    ];

    private readonly IntegrationTestWebAppFactory factory;

    public AuditSchemaTests(IntegrationTestWebAppFactory factory)
        : base(factory)
    {
        this.factory = factory;
    }

    [Fact]
    public async Task AuditTables_Should_ExposeContractColumns_WhenMigrationsApplied()
    {
        // Arrange
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        // Act
        List<string> auditLogColumns = await context.Database.SqlQuery<string>(
            $"""
             SELECT column_name AS "Value"
             FROM information_schema.columns
             WHERE table_schema = 'public' AND table_name = 'audit_logs'
             ORDER BY ordinal_position
             """).ToListAsync();
        List<string> auditLogEntryColumns = await context.Database.SqlQuery<string>(
            $"""
             SELECT column_name AS "Value"
             FROM information_schema.columns
             WHERE table_schema = 'public' AND table_name = 'audit_log_entries'
             ORDER BY ordinal_position
             """).ToListAsync();
        List<string> summaryDataTypes = await context.Database.SqlQuery<string>(
            $"""
             SELECT data_type AS "Value"
             FROM information_schema.columns
             WHERE table_schema = 'public' AND table_name = 'audit_logs' AND column_name = 'summary'
             """).ToListAsync();

        // Assert
        auditLogColumns.Sort();
        auditLogEntryColumns.Sort();
        auditLogColumns.ShouldBe(Sorted(ExpectedAuditLogColumns));
        auditLogEntryColumns.ShouldBe(Sorted(ExpectedAuditLogEntryColumns));
        summaryDataTypes.ShouldHaveSingleItem().ShouldBe("jsonb");
    }

    [Fact]
    public async Task AuditTables_Should_HaveEveryQueryIndex_WhenMigrationsApplied()
    {
        // Arrange
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        // Act
        List<string> indexNames = await context.Database.SqlQuery<string>(
            $"""
             SELECT indexname AS "Value"
             FROM pg_indexes
             WHERE schemaname = 'public' AND tablename IN ('audit_logs', 'audit_log_entries')
             """).ToListAsync();

        // Assert
        foreach (string expectedIndex in ExpectedAuditLogIndexes.Concat(ExpectedAuditLogEntryIndexes))
        {
            indexNames.ShouldContain(expectedIndex);
        }
    }

    [Fact]
    public async Task AuditInsert_Should_RejectNonObjectSummary_WhenJsonCheckIsViolated()
    {
        // Arrange
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        // Act
        PostgresException exception = await Should.ThrowAsync<PostgresException>(
            () => context.Database.ExecuteSqlInterpolatedAsync(
                $"""
                 INSERT INTO public.audit_logs
                     (id, operation_id, request_id, user_id, entity_type, entity_id,
                      aggregate_type, aggregate_id, action, command_name, summary, ip_address,
                      created_at_utc)
                 VALUES
                     ({Guid.NewGuid()}, {Guid.NewGuid()}, NULL, NULL, {"User"}, {Guid.NewGuid()},
                      NULL, NULL, {"Create"}, NULL, '[1,2]'::jsonb, NULL, {DateTime.UtcNow})
                 """));

        // Assert
        exception.SqlState.ShouldBe(PostgresErrorCodes.CheckViolation);
        exception.ConstraintName.ShouldBe("ck_audit_logs_summary_is_json_object");
    }

    [Fact]
    public async Task AuditEntries_Should_RejectEqualValuesAndBlankFieldName_WhenChecksAreViolated()
    {
        // Arrange
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var parentId = Guid.NewGuid();
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"""
             INSERT INTO public.audit_logs
                 (id, operation_id, request_id, user_id, entity_type, entity_id,
                  aggregate_type, aggregate_id, action, command_name, summary, ip_address,
                  created_at_utc)
             VALUES
                 ({parentId}, {Guid.NewGuid()}, NULL, NULL, {"User"}, {Guid.NewGuid()},
                  NULL, NULL, {"Create"}, NULL, NULL, NULL, {DateTime.UtcNow})
             """);

        // Act
        PostgresException equalValuesException = await Should.ThrowAsync<PostgresException>(
            () => context.Database.ExecuteSqlInterpolatedAsync(
                $"""
                 INSERT INTO public.audit_log_entries
                     (id, audit_log_id, field_name, old_value, new_value)
                 VALUES
                     ({Guid.NewGuid()}, {parentId}, {"status_code"}, {"same"}, {"same"})
                 """));
        PostgresException blankFieldNameException = await Should.ThrowAsync<PostgresException>(
            () => context.Database.ExecuteSqlInterpolatedAsync(
                $"""
                 INSERT INTO public.audit_log_entries
                     (id, audit_log_id, field_name, old_value, new_value)
                 VALUES
                     ({Guid.NewGuid()}, {parentId}, {"   "}, {null}, {"value"})
                 """));

        // Assert
        equalValuesException.SqlState.ShouldBe(PostgresErrorCodes.CheckViolation);
        equalValuesException.ConstraintName.ShouldBe("ck_audit_log_entries_values_distinct");
        blankFieldNameException.SqlState.ShouldBe(PostgresErrorCodes.CheckViolation);
        blankFieldNameException.ConstraintName.ShouldBe("ck_audit_log_entries_field_name_not_blank");
    }

    [Fact]
    public async Task AuditLogs_Should_RejectUpdateAndDelete_WhenAppendOnlyTriggerFires()
    {
        // Arrange
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Result<AuditLog> createResult = AuditLog.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            $"req-{Guid.NewGuid():N}",
            null,
            "User",
            Guid.NewGuid(),
            null,
            null,
            AuditActions.Create,
            null,
            """{"source":"m8-schema-test"}""",
            null,
            DateTime.UtcNow);
        createResult.IsSuccess.ShouldBeTrue();
        AuditLog auditLog = createResult.Value;
        context.AuditLogs.Add(auditLog);
        await context.SaveChangesAsync();

        // Act
        PostgresException updateException = await Should.ThrowAsync<PostgresException>(
            () => context.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE public.audit_logs SET ip_address = {"127.0.0.1"} WHERE id = {auditLog.Id}"));
        PostgresException deleteException = await Should.ThrowAsync<PostgresException>(
            () => context.Database.ExecuteSqlInterpolatedAsync(
                $"DELETE FROM public.audit_logs WHERE id = {auditLog.Id}"));

        // Assert
        updateException.SqlState.ShouldBe(PostgresErrorCodes.RaiseException);
        deleteException.SqlState.ShouldBe(PostgresErrorCodes.RaiseException);
    }

    [Fact]
    public async Task AuditEntries_Should_RejectUpdateAndDelete_WhenAppendOnlyTriggerFires()
    {
        // Arrange
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Result<AuditLog> createResult = AuditLog.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            $"req-{Guid.NewGuid():N}",
            null,
            "User",
            Guid.NewGuid(),
            null,
            null,
            AuditActions.Update,
            null,
            """{"source":"m8-schema-test"}""",
            null,
            DateTime.UtcNow);
        createResult.IsSuccess.ShouldBeTrue();
        Result<AuditLogEntry> entryResult = AuditLogEntry.Create(
            Guid.NewGuid(),
            createResult.Value.Id,
            "ip_address",
            null,
            "10.0.0.1");
        entryResult.IsSuccess.ShouldBeTrue();
        context.AuditLogs.Add(createResult.Value);
        context.AuditLogEntries.Add(entryResult.Value);
        await context.SaveChangesAsync();

        // Act
        PostgresException updateException = await Should.ThrowAsync<PostgresException>(
            () => context.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE public.audit_log_entries SET old_value = {"9"} WHERE id = {entryResult.Value.Id}"));
        PostgresException deleteException = await Should.ThrowAsync<PostgresException>(
            () => context.Database.ExecuteSqlInterpolatedAsync(
                $"DELETE FROM public.audit_log_entries WHERE id = {entryResult.Value.Id}"));

        // Assert
        updateException.SqlState.ShouldBe(PostgresErrorCodes.RaiseException);
        deleteException.SqlState.ShouldBe(PostgresErrorCodes.RaiseException);
    }

    [Fact]
    public async Task PermissionSeed_Should_BeIdempotentWithAdministratorMapping_WhenMigrationsApplied()
    {
        // Arrange
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        // Act
        List<Permission> viewPermissions = await context.Permissions
            .Where(permission => permission.Code == PermissionCodes.AuditLogs.View)
            .ToListAsync();
        bool administratorMappingExists = await context.RolePermissions.AnyAsync(
            rolePermission => rolePermission.PermissionId == WellKnownPermissions.AuditLogsViewId &&
                rolePermission.RoleId == WellKnownRoles.AdministratorId);

        // Assert
        Permission permission = viewPermissions.ShouldHaveSingleItem();
        permission.Id.ShouldBe(WellKnownPermissions.AuditLogsViewId);
        administratorMappingExists.ShouldBeTrue();
    }

    private static List<string> Sorted(IEnumerable<string> values) =>
        [.. values.OrderBy(value => value, StringComparer.Ordinal)];
}
