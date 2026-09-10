using Infrastructure.Database;
using Infrastructure.DomainEvents;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using SharedKernel;

namespace IntegrationTests.Infrastructure;

/// <summary>
/// Exercises the deployment split on the same PostgreSQL Testcontainer used by the API suite.
/// The factory applies all EF migrations as the container owner first; this test then connects as
/// a deliberately constrained login role. No external database, Neon project, or real credential
/// is ever touched.
/// </summary>
[Collection(nameof(IntegrationTestCollection))]
public sealed class RuntimeDatabaseRoleIntegrationTests(IntegrationTestWebAppFactory factory)
{
    [Fact]
    public async Task RuntimeRole_ShouldRunRepresentativeDbContextWork_ButCannotPerformDdlOrTamperWithAudit()
    {
        string roleName = $"eiams_runtime_test_{Guid.NewGuid():N}";
        string rolePassword = Guid.NewGuid().ToString("N");
        string futureTable = $"runtime_grant_probe_{Guid.NewGuid():N}";
        string quotedRoleName = QuoteIdentifier(roleName);
        string quotedFutureTable = QuoteIdentifier(futureTable);
        string runtimeConnectionString = new NpgsqlConnectionStringBuilder(factory.DatabaseConnectionString)
        {
            Username = roleName,
            Password = rolePassword,
            Pooling = false
        }.ConnectionString;

        await using var ownerConnection = new NpgsqlConnection(factory.DatabaseConnectionString);
        await ownerConnection.OpenAsync();

        try
        {
            // Prove the deployment path also closes a grant inherited through PUBLIC, not merely
            // a role-specific ACL. The role begins intentionally over-privileged to prove ALTER
            // ROLE hardens a pre-existing identity rather than only a newly created one.
            await ExecuteAsync(ownerConnection, "GRANT CREATE ON SCHEMA public TO PUBLIC");
            await ExecuteAsync(ownerConnection,
                $"CREATE ROLE {quotedRoleName} LOGIN INHERIT SUPERUSER CREATEDB CREATEROLE REPLICATION BYPASSRLS PASSWORD {QuoteLiteral(rolePassword)}");
            (await ScalarAsync<bool>(ownerConnection,
                $"SELECT rolsuper AND rolinherit AND rolcreatedb AND rolcreaterole AND rolreplication AND rolbypassrls FROM pg_roles WHERE rolname = {QuoteLiteral(roleName)}"))
                .ShouldBeTrue();

            await ProvisionExistingRuntimeRoleAsync(ownerConnection, roleName, quotedRoleName);

            await using var runtimeConnection = new NpgsqlConnection(runtimeConnectionString);
            await runtimeConnection.OpenAsync();

            (await ScalarAsync<string>(runtimeConnection, "SELECT current_user")).ShouldBe(roleName);
            (await ScalarAsync<bool>(ownerConnection,
                $"SELECT rolcanlogin AND NOT rolinherit AND NOT rolsuper AND NOT rolcreatedb AND NOT rolcreaterole AND NOT rolreplication AND NOT rolbypassrls FROM pg_roles WHERE rolname = {QuoteLiteral(roleName)}"))
                .ShouldBeTrue();
            (await ScalarAsync<bool>(runtimeConnection,
                "SELECT has_schema_privilege(current_user, 'public', 'CREATE')")).ShouldBeFalse();
            (await ScalarAsync<bool>(runtimeConnection,
                "SELECT has_table_privilege(current_user, 'public.audit_logs', 'INSERT')")).ShouldBeTrue();
            (await ScalarAsync<bool>(runtimeConnection,
                "SELECT has_table_privilege(current_user, 'public.audit_logs', 'UPDATE')")).ShouldBeFalse();
            (await ScalarAsync<bool>(runtimeConnection,
                "SELECT has_table_privilege(current_user, 'public.audit_logs', 'DELETE')")).ShouldBeFalse();

            await VerifyRepresentativeDbContextWorkflowAsync(runtimeConnectionString);

            // Objects made by the migration owner after the initial provision inherit DML/sequence
            // rights from ALTER DEFAULT PRIVILEGES, without granting runtime any schema CREATE.
            await ExecuteAsync(ownerConnection,
                $"CREATE TABLE public.{quotedFutureTable} (id uuid PRIMARY KEY, value text NOT NULL)");
            await ExecuteAsync(runtimeConnection,
                $"INSERT INTO public.{quotedFutureTable} (id, value) VALUES ('{Guid.NewGuid()}', 'created-by-runtime')");
            await ExecuteAsync(runtimeConnection,
                $"UPDATE public.{quotedFutureTable} SET value = 'updated-by-runtime'");
            await ExecuteAsync(runtimeConnection, $"DELETE FROM public.{quotedFutureTable}");

            await AssertInsufficientPrivilegeAsync(
                runtimeConnection,
                $"CREATE TABLE public.{QuoteIdentifier($"runtime_ddl_probe_{Guid.NewGuid():N}")} (id integer)");
            await AssertInsufficientPrivilegeAsync(
                runtimeConnection,
                "ALTER TABLE public.audit_logs DISABLE TRIGGER trg_audit_logs_append_only");
            await AssertInsufficientPrivilegeAsync(runtimeConnection, "DELETE FROM public.audit_logs");
        }
        finally
        {
            await ExecuteAsync(ownerConnection, $"DROP TABLE IF EXISTS public.{quotedFutureTable}");
            await ExecuteAsync(ownerConnection,
                $"ALTER DEFAULT PRIVILEGES FOR ROLE postgres IN SCHEMA public REVOKE SELECT, INSERT, UPDATE, DELETE ON TABLES FROM {quotedRoleName}");
            await ExecuteAsync(ownerConnection,
                $"ALTER DEFAULT PRIVILEGES FOR ROLE postgres IN SCHEMA public REVOKE USAGE, SELECT ON SEQUENCES FROM {quotedRoleName}");
            await ExecuteAsync(ownerConnection, $"DROP OWNED BY {quotedRoleName}");
            await ExecuteAsync(ownerConnection, $"DROP ROLE IF EXISTS {quotedRoleName}");
        }
    }

    [Fact]
    public async Task RuntimeRolePreflight_ShouldRejectOwnerNameMembershipAndApplicationOwnership()
    {
        string memberRole = $"eiams_runtime_member_{Guid.NewGuid():N}";
        string groupRole = $"eiams_runtime_group_{Guid.NewGuid():N}";
        string ownedRole = $"eiams_runtime_owner_{Guid.NewGuid():N}";
        string functionOwnerRole = $"eiams_runtime_function_owner_{Guid.NewGuid():N}";
        string ownedTable = $"runtime_owned_probe_{Guid.NewGuid():N}";
        string ownedFunction = $"runtime_owned_function_{Guid.NewGuid():N}";
        string quotedMemberRole = QuoteIdentifier(memberRole);
        string quotedGroupRole = QuoteIdentifier(groupRole);
        string quotedOwnedRole = QuoteIdentifier(ownedRole);
        string quotedFunctionOwnerRole = QuoteIdentifier(functionOwnerRole);
        string quotedOwnedTable = QuoteIdentifier(ownedTable);
        string quotedOwnedFunction = QuoteIdentifier(ownedFunction);

        await using var ownerConnection = new NpgsqlConnection(factory.DatabaseConnectionString);
        await ownerConnection.OpenAsync();

        try
        {
            await Should.ThrowAsync<InvalidOperationException>(() =>
                ValidateRuntimeRolePreflightAsync(ownerConnection, "postgres", "postgres"));

            await ExecuteAsync(ownerConnection, $"CREATE ROLE {quotedMemberRole} NOLOGIN");
            await ExecuteAsync(ownerConnection, $"CREATE ROLE {quotedGroupRole} NOLOGIN");
            await ExecuteAsync(ownerConnection, $"GRANT {quotedGroupRole} TO {quotedMemberRole}");
            await Should.ThrowAsync<InvalidOperationException>(() =>
                ValidateRuntimeRolePreflightAsync(ownerConnection, memberRole, "postgres"));

            await ExecuteAsync(ownerConnection, $"CREATE ROLE {quotedOwnedRole} NOLOGIN");
            await ExecuteAsync(ownerConnection, $"CREATE TABLE public.{quotedOwnedTable} (id integer PRIMARY KEY)");
            await ExecuteAsync(ownerConnection, $"ALTER TABLE public.{quotedOwnedTable} OWNER TO {quotedOwnedRole}");
            await Should.ThrowAsync<InvalidOperationException>(() =>
                ValidateRuntimeRolePreflightAsync(ownerConnection, ownedRole, "postgres"));

            await ExecuteAsync(ownerConnection, $"CREATE ROLE {quotedFunctionOwnerRole} NOLOGIN");
            await ExecuteAsync(ownerConnection,
                $"CREATE FUNCTION public.{quotedOwnedFunction}() RETURNS integer LANGUAGE sql AS 'SELECT 1'");
            await ExecuteAsync(ownerConnection,
                $"ALTER FUNCTION public.{quotedOwnedFunction}() OWNER TO {quotedFunctionOwnerRole}");
            await Should.ThrowAsync<InvalidOperationException>(() =>
                ValidateRuntimeRolePreflightAsync(ownerConnection, functionOwnerRole, "postgres"));

            // Do not change ownership of the shared test database. This direct catalog assertion
            // proves the same preflight condition would reject a role that owned it.
            (await OwnsCurrentDatabaseAsync(ownerConnection, "postgres")).ShouldBeTrue();
            (await OwnsCurrentDatabaseAsync(ownerConnection, memberRole)).ShouldBeFalse();
        }
        finally
        {
            await ExecuteAsync(ownerConnection, $"ALTER FUNCTION public.{quotedOwnedFunction}() OWNER TO postgres");
            await ExecuteAsync(ownerConnection, $"DROP FUNCTION IF EXISTS public.{quotedOwnedFunction}()");
            await ExecuteAsync(ownerConnection, $"ALTER TABLE IF EXISTS public.{quotedOwnedTable} OWNER TO postgres");
            await ExecuteAsync(ownerConnection, $"DROP TABLE IF EXISTS public.{quotedOwnedTable}");
            await ExecuteAsync(ownerConnection, $"REVOKE {quotedGroupRole} FROM {quotedMemberRole}");
            await ExecuteAsync(ownerConnection, $"DROP ROLE IF EXISTS {quotedGroupRole}");
            await ExecuteAsync(ownerConnection, $"DROP ROLE IF EXISTS {quotedMemberRole}");
            await ExecuteAsync(ownerConnection, $"DROP ROLE IF EXISTS {quotedOwnedRole}");
            await ExecuteAsync(ownerConnection, $"DROP ROLE IF EXISTS {quotedFunctionOwnerRole}");
        }
    }

    private async Task VerifyRepresentativeDbContextWorkflowAsync(string runtimeConnectionString)
    {
        DbContextOptions<ApplicationDbContext> options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(runtimeConnectionString)
            .UseSnakeCaseNamingConvention()
            .Options;

        await using var context = new ApplicationDbContext(options, new NoOpDomainEventsDispatcher());

        Guid userId = await context.Users
            .AsNoTracking()
            .Where(user => user.Email == IntegrationTestWebAppFactory.AdministratorEmail)
            .Select(user => user.Id)
            .SingleAsync();

        await using IDbContextTransaction transaction = await context.Database.BeginTransactionAsync();
        int affected = await context.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE public.users SET first_name = first_name WHERE id = {userId}");
        affected.ShouldBe(1);
        await transaction.RollbackAsync();
    }

    private static async Task ProvisionExistingRuntimeRoleAsync(
        NpgsqlConnection ownerConnection,
        string roleName,
        string quotedRoleName,
        string migrationOwner = "postgres")
    {
        // This is the executable permission equivalent of scripts/provision-runtime-role.sql.
        // The script creates a missing role without a password; this test instead begins with a
        // deliberately unsafe existing role, then proves hardening is applied on every run.
        await ValidateRuntimeRolePreflightAsync(ownerConnection, roleName, migrationOwner);
        await ExecuteAsync(ownerConnection,
            $"ALTER ROLE {quotedRoleName} LOGIN NOINHERIT NOSUPERUSER NOCREATEDB NOCREATEROLE NOREPLICATION NOBYPASSRLS");
        await ExecuteAsync(ownerConnection, $"GRANT CONNECT ON DATABASE clean_architecture_integration_test TO {quotedRoleName}");
        await ExecuteAsync(ownerConnection, $"GRANT USAGE ON SCHEMA public TO {quotedRoleName}");
        await ExecuteAsync(ownerConnection, "REVOKE CREATE ON SCHEMA public FROM PUBLIC");
        await ExecuteAsync(ownerConnection, $"REVOKE CREATE ON SCHEMA public FROM {quotedRoleName}");
        await ExecuteAsync(ownerConnection,
            $"GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA public TO {quotedRoleName}");
        await ExecuteAsync(ownerConnection,
            $"GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA public TO {quotedRoleName}");
        await ExecuteAsync(ownerConnection,
            $"REVOKE ALL ON TABLE public.\"__EFMigrationsHistory\" FROM {quotedRoleName}");
        await ExecuteAsync(ownerConnection,
            $"GRANT SELECT ON TABLE public.\"__EFMigrationsHistory\" TO {quotedRoleName}");

        foreach (string table in ImmutableTables)
        {
            await ExecuteAsync(ownerConnection,
                $"REVOKE UPDATE, DELETE ON TABLE public.{QuoteIdentifier(table)} FROM {quotedRoleName}");
        }

        await ExecuteAsync(ownerConnection,
            $"ALTER DEFAULT PRIVILEGES FOR ROLE postgres IN SCHEMA public GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO {quotedRoleName}");
        await ExecuteAsync(ownerConnection,
            $"ALTER DEFAULT PRIVILEGES FOR ROLE postgres IN SCHEMA public GRANT USAGE, SELECT ON SEQUENCES TO {quotedRoleName}");
    }

    private static async Task ValidateRuntimeRolePreflightAsync(
        NpgsqlConnection ownerConnection,
        string roleName,
        string migrationOwner)
    {
        string currentUser = await ScalarAsync<string>(ownerConnection, "SELECT current_user");
        if (!IsSafeRuntimeRoleName(roleName) ||
            string.Equals(roleName, migrationOwner, StringComparison.Ordinal) ||
            string.Equals(roleName, currentUser, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("The runtime role name is unsafe or equals the migration owner.");
        }

        if (await ExistsAsync(ownerConnection, """
            SELECT EXISTS (
                SELECT 1
                FROM pg_auth_members AS membership
                INNER JOIN pg_roles AS member ON member.oid = membership.member
                WHERE member.rolname = @role_name)
            """, roleName))
        {
            throw new InvalidOperationException("The runtime role has memberships.");
        }

        if (await ExistsAsync(ownerConnection, """
            SELECT EXISTS (
                SELECT 1
                FROM pg_namespace AS schema
                INNER JOIN pg_roles AS owner ON owner.oid = schema.nspowner
                WHERE schema.nspname = 'public'
                  AND owner.rolname = @role_name

                UNION ALL

                SELECT 1
                FROM pg_class AS relation
                INNER JOIN pg_namespace AS schema ON schema.oid = relation.relnamespace
                INNER JOIN pg_roles AS owner ON owner.oid = relation.relowner
                WHERE schema.nspname = 'public'
                  AND owner.rolname = @role_name
                  AND relation.relkind IN ('r', 'p', 'v', 'm', 'S', 'f'))
            """, roleName))
        {
            throw new InvalidOperationException("The runtime role owns application schema objects.");
        }

        if (await ExistsAsync(ownerConnection, """
            SELECT EXISTS (
                SELECT 1
                FROM pg_proc AS routine
                INNER JOIN pg_namespace AS schema ON schema.oid = routine.pronamespace
                INNER JOIN pg_roles AS owner ON owner.oid = routine.proowner
                WHERE schema.nspname = 'public'
                  AND owner.rolname = @role_name

                UNION ALL

                SELECT 1
                FROM pg_type AS data_type
                INNER JOIN pg_namespace AS schema ON schema.oid = data_type.typnamespace
                INNER JOIN pg_roles AS owner ON owner.oid = data_type.typowner
                WHERE schema.nspname = 'public'
                  AND owner.rolname = @role_name
                  AND data_type.typrelid = 0
                  AND data_type.typelem = 0)
            """, roleName))
        {
            throw new InvalidOperationException("The runtime role owns application routines or standalone types.");
        }

        if (await OwnsCurrentDatabaseAsync(ownerConnection, roleName))
        {
            throw new InvalidOperationException("The runtime role owns the current database.");
        }
    }

    private static readonly string[] ImmutableTables =
    [
        "audit_logs",
        "audit_log_entries",
        "stock_movements",
        "asset_movement_history",
        "document_lifecycle_events"
    ];

    private static async Task AssertInsufficientPrivilegeAsync(NpgsqlConnection connection, string sql)
    {
        PostgresException exception = await Should.ThrowAsync<PostgresException>(() => ExecuteAsync(connection, sql));
        exception.SqlState.ShouldBe(PostgresErrorCodes.InsufficientPrivilege);
    }

    private static async Task ExecuteAsync(NpgsqlConnection connection, string sql)
    {
#pragma warning disable CA2100 // Test-only statements use fixed SQL or locally generated quoted identifiers.
        await using var command = new NpgsqlCommand(sql, connection);
#pragma warning restore CA2100
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<T> ScalarAsync<T>(NpgsqlConnection connection, string sql)
    {
#pragma warning disable CA2100 // Test-only statements use fixed SQL or locally generated quoted identifiers.
        await using var command = new NpgsqlCommand(sql, connection);
#pragma warning restore CA2100
        return (T)(await command.ExecuteScalarAsync())!;
    }

    private static async Task<bool> ExistsAsync(NpgsqlConnection connection, string sql, string roleName)
    {
#pragma warning disable CA2100 // Test-only fixed catalog query; role name remains a database parameter.
        await using var command = new NpgsqlCommand(sql, connection);
#pragma warning restore CA2100
        command.Parameters.AddWithValue("role_name", roleName);
        return (bool)(await command.ExecuteScalarAsync())!;
    }

    private static Task<bool> OwnsCurrentDatabaseAsync(NpgsqlConnection connection, string roleName) =>
        ExistsAsync(connection, """
            SELECT EXISTS (
                SELECT 1
                FROM pg_database AS database
                INNER JOIN pg_roles AS owner ON owner.oid = database.datdba
                WHERE database.datname = current_database()
                  AND owner.rolname = @role_name)
            """, roleName);

    private static bool IsSafeRuntimeRoleName(string roleName) =>
        System.Text.RegularExpressions.Regex.IsMatch(roleName, "^[a-z][a-z0-9_]{0,62}$") &&
        !roleName.Equals("postgres", StringComparison.Ordinal) &&
        !roleName.StartsWith("pg_", StringComparison.Ordinal);

    private static string QuoteIdentifier(string identifier) =>
        $"\"{identifier.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";

    private static string QuoteLiteral(string value) =>
        $"'{value.Replace("'", "''", StringComparison.Ordinal)}'";

    private sealed class NoOpDomainEventsDispatcher : IDomainEventsDispatcher
    {
        public Task DispatchAsync(
            IEnumerable<IDomainEvent> domainEvents,
            CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
