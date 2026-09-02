using Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

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
}
