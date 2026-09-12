using Application.Abstractions.Searching;
using Domain.Common;
using Domain.MaterialCategories;
using Domain.MaterialDomains;
using Domain.MaterialFamilies;
using Domain.Materials;
using Domain.UnitsOfMeasure;
using Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace IntegrationTests.Performance;

[Collection(nameof(IntegrationTestCollection))]
public sealed class TrigramSearchIndexTests(IntegrationTestWebAppFactory factory)
{
    [Fact]
    public async Task PerformanceMigration_ShouldInstallPgTrgmAndExpectedHotSearchIndexes()
    {
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        int extensionCount = await context.Database
            .SqlQuery<int>($"SELECT COUNT(*)::int AS \"Value\" FROM pg_extension WHERE extname = 'pg_trgm'")
            .SingleAsync();

        string[] expectedIndexes =
        [
            "ix_materials_code_trgm",
            "ix_materials_name_ar_trgm",
            "ix_warehouses_code_trgm",
            "ix_warehouses_name_trgm",
            "ix_assets_asset_number_trgm",
            "ix_assets_serial_number_trgm",
            "ix_warehouse_documents_system_reference_number_trgm",
            "ix_users_email_trgm"
        ];

        string[] actualIndexes = await context.Database
            .SqlQuery<string>($"""
                SELECT indexname AS "Value"
                FROM pg_indexes
                WHERE schemaname = 'public'
                  AND indexname = ANY({expectedIndexes})
                """)
            .ToArrayAsync();

        extensionCount.ShouldBe(1);
        actualIndexes.OrderBy(value => value).ShouldBe(expectedIndexes.OrderBy(value => value));
    }

    [Fact]
    public async Task MaterialSearch_ShouldPreserveArabicCaseAndLiteralWildcards()
    {
        string token = $"ZXQ{Guid.NewGuid():N}"[..18].ToUpperInvariant();
        await SeedMaterialsAsync(token, 4);

        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        string literalPattern = SqlLikePattern.CreateContains($"{token}%_", normalizeToUpper: true)!;
        string lowerCasePrefixToken = string.Concat("zxq", token.AsSpan(3));
#pragma warning disable CA1304, CA1311 // The production queries deliberately translate UPPER to PostgreSQL.
        int literalCount = await context.Materials
            .Where(item => EF.Functions.Like(item.Code.ToUpper(), literalPattern, SqlLikePattern.EscapeCharacter))
            .CountAsync();
        int caseInsensitiveCount = await context.Materials
            .Where(item => EF.Functions.Like(
                item.Code.ToUpper(),
                SqlLikePattern.CreateContains(lowerCasePrefixToken, normalizeToUpper: true)!,
                SqlLikePattern.EscapeCharacter))
            .CountAsync();
        int arabicCount = await context.Materials
            .Where(item => EF.Functions.Like(
                item.NameAr.ToUpper(),
                SqlLikePattern.CreateContains("تجربة", normalizeToUpper: true)!,
                SqlLikePattern.EscapeCharacter))
            .CountAsync();
#pragma warning restore CA1304, CA1311

        literalCount.ShouldBe(1);
        caseInsensitiveCount.ShouldBe(1);
        arabicCount.ShouldBeGreaterThan(0);
    }

    [ExplicitPostgreSqlReadPlanAnalysisFact]
    public async Task MaterialCodeTrigramIndex_ShouldSupportProductionSearchExpression_WhenExplicitlyAnalyzed()
    {
        // This is explicit because its sole purpose is a test-only capability check and it seeds
        // 10k rows. It proves expression/operator compatibility, not production performance.
        string token = $"ZXQ{Guid.NewGuid():N}"[..18].ToUpperInvariant();
        await SeedMaterialsAsync(token, 10_000);

        PostgreSqlReadPlanAnalysis.ValidateTestOnlyConnection(factory.DatabaseConnectionString, "Test");
        await using var connection = new NpgsqlConnection(factory.DatabaseConnectionString);
        await connection.OpenAsync();

        string plan = await PostgreSqlReadPlanAnalysis.ExecuteReadOnlyTransactionAsync(connection, async (transaction, cancellationToken) =>
        {
            await using var setup = new NpgsqlCommand("SET LOCAL enable_seqscan = off", connection, transaction)
            {
                CommandTimeout = 5
            };
            await setup.ExecuteNonQueryAsync(cancellationToken);

            await using var command = new NpgsqlCommand(
                "EXPLAIN (ANALYZE FALSE, BUFFERS FALSE, FORMAT TEXT) " +
                "SELECT id FROM public.materials WHERE upper(code) LIKE @pattern ESCAPE '\\'",
                connection,
                transaction)
            {
                CommandTimeout = 5
            };
            command.Parameters.AddWithValue("pattern", $"%{token}%");
            await using NpgsqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
            var lines = new List<string>();
            while (await reader.ReadAsync(cancellationToken))
            {
                lines.Add(reader.GetString(0));
            }

            return string.Join(Environment.NewLine, lines);
        });

        plan.ShouldContain("ix_materials_code_trgm");
    }

    private async Task SeedMaterialsAsync(string token, int count)
    {
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        string suffix = Guid.NewGuid().ToString("N")[..12];
        var unit = UnitOfMeasure.Create(Guid.NewGuid(), $"Unit {suffix}", $"U{suffix[..6]}", "Quantity");
        var domain = MaterialDomain.Create(Guid.NewGuid(), $"Domain {suffix}", $"D{suffix[..8]}");
        var category = MaterialCategory.Create(Guid.NewGuid(), domain.Id, null, $"Category {suffix}", $"C{suffix[..8]}");
        var family = MaterialFamily.Create(Guid.NewGuid(), category.Id, $"Family {suffix}", $"F{suffix[..8]}", unit.Id);
        context.AddRange(unit, domain, category, family);

        for (int index = 0; index < count; index++)
        {
            string code = index == 0 ? $"MAT-{token}%_" : $"MAT-{suffix}-{index:D4}";
            context.Materials.Add(Material.Create(
                Guid.NewGuid(), family.Id, unit.Id, $"مادة تجربة {index}", null, code,
                MaterialKind.Consumable, TrackingType.Quantity, false, null));
        }

        await context.SaveChangesAsync();
        await context.Database.ExecuteSqlRawAsync("ANALYZE public.materials");
    }

}
