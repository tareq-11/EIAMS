using Domain.Common;
using Domain.Organizations;
using Domain.Sites;
using Domain.TransferInfos;
using Domain.Warehouses;
using Domain.WarehouseDocuments;
using Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using SharedKernel;

namespace IntegrationTests.M5;

[Collection(nameof(IntegrationTestCollection))]
public sealed class M5TransferInfoDatabaseTests(IntegrationTestWebAppFactory factory)
{
    [Fact]
    public async Task TransferInfoConstraint_Should_RejectUnknownDestinationWarehouse()
    {
        // Arrange
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        ApplicationDbContext dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        TransferInfoDatabaseSeed seed = await SeedDocumentAsync(dbContext);
        Result<TransferInfo> transferInfo = TransferInfo.Create(
            seed.DocumentId,
            Guid.NewGuid(),
            "Warehouse replenishment");
        transferInfo.IsSuccess.ShouldBeTrue();
        dbContext.TransferInfos.Add(transferInfo.Value);

        // Act
        DbUpdateException exception = await Should.ThrowAsync<DbUpdateException>(
            () => dbContext.SaveChangesAsync());

        // Assert
        PostgresException postgresException = exception.InnerException.ShouldBeOfType<PostgresException>();
        postgresException.SqlState.ShouldBe(PostgresErrorCodes.ForeignKeyViolation);
        postgresException.ConstraintName.ShouldBe("fk_transfer_info_warehouses_destination_warehouse_id");
    }

    [Fact]
    public async Task TransferInfoConstraint_Should_RejectBlankTransferReason()
    {
        // Arrange
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        ApplicationDbContext dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        TransferInfoDatabaseSeed seed = await SeedDocumentAsync(dbContext);

        // Act
        PostgresException exception = await Should.ThrowAsync<PostgresException>(
            () => dbContext.Database.ExecuteSqlInterpolatedAsync(
                $"""
                  INSERT INTO public.transfer_info
                      (document_id, destination_warehouse_id, transfer_reason, created_at_utc)
                  VALUES
                      ({seed.DocumentId}, {seed.DestinationWarehouseId}, {"   "}, {DateTime.UtcNow})
                  """));

        // Assert
        exception.SqlState.ShouldBe(PostgresErrorCodes.CheckViolation);
        exception.ConstraintName.ShouldBe("ck_transfer_info_transfer_reason_not_blank");
    }

    private static async Task<TransferInfoDatabaseSeed> SeedDocumentAsync(ApplicationDbContext dbContext)
    {
        string suffix = Guid.NewGuid().ToString("N")[..10];
        var organization = Organization.Create(Guid.NewGuid(), $"Organization {suffix}", $"O{suffix}");
        var site = Site.Create(Guid.NewGuid(), organization.Id, $"Site {suffix}", $"S{suffix}", null);
        var source = Warehouse.Create(Guid.NewGuid(), site.Id, $"Source {suffix}", $"WS{suffix}", "Main", true);
        var destination = Warehouse.Create(Guid.NewGuid(), site.Id, $"Destination {suffix}", $"WD{suffix}", "Main", true);
        var document = WarehouseDocument.CreateDraft(
            Guid.NewGuid(),
            source.Id,
            DocumentType.Transfer,
            $"TR-{suffix}");
        dbContext.AddRange(organization, site, source, destination, document);
        await dbContext.SaveChangesAsync();

        return new TransferInfoDatabaseSeed(document.Id, destination.Id);
    }

    private sealed record TransferInfoDatabaseSeed(Guid DocumentId, Guid DestinationWarehouseId);
}
