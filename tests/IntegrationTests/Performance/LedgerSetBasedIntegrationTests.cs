using Application.Abstractions.Ledger;
using Domain.Common;
using Domain.DocumentAttachments;
using Domain.DocumentLines;
using Domain.Materials;
using Domain.WarehouseDocuments;
using Infrastructure.Database;
using IntegrationTests.Regression;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using SharedKernel;

namespace IntegrationTests.Performance;

[Collection(nameof(IntegrationTestCollection))]
public sealed class LedgerSetBasedIntegrationTests : BaseIntegrationTest
{
    private readonly IntegrationTestWebAppFactory factory;

    public LedgerSetBasedIntegrationTests(IntegrationTestWebAppFactory factory) : base(factory)
    {
        this.factory = factory;
    }

    [Fact]
    public async Task LedgerAppend_ShouldUseExactPairsAndOneSetBasedBalanceCommandPerPipelineStep()
    {
        // Arrange
        RegressionSeedData seed = await RegressionTestHelper.SeedAsync(factory.Services);
        const int pairCount = 100;
        IReadOnlyList<(Guid WarehouseId, Guid MaterialId)> requestedKeys;

        await using (AsyncServiceScope scope = factory.Services.CreateAsyncScope())
        {
            ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var document = WarehouseDocument.CreateDraft(
                Guid.NewGuid(),
                seed.WarehouseId,
                DocumentType.Receiving,
                $"LEDGER-{Guid.NewGuid():N}"[..15]);
            context.WarehouseDocuments.Add(document);

            var drafts = new List<MovementDraft>(pairCount);
            var keys = new List<(Guid WarehouseId, Guid MaterialId)>(pairCount);

            for (int index = 0; index < pairCount; index++)
            {
                var material = Material.Create(
                    Guid.NewGuid(),
                    seed.FamilyId,
                    seed.UnitOfMeasureId,
                    $"Ledger material {index}",
                    $"Ledger material {index}",
                    $"L{index:D11}",
                    MaterialKind.Consumable,
                    TrackingType.Quantity,
                    false,
                    null);
                context.Materials.Add(material);

                Result<DocumentLine> lineResult = DocumentLine.Create(
                    Guid.NewGuid(),
                    document.Id,
                    material.Id,
                    DocumentLineType.Normal,
                    1m,
                    null,
                    1m,
                    null,
                    null,
                    null,
                    null);
                lineResult.IsSuccess.ShouldBeTrue();
                context.DocumentLines.Add(lineResult.Value);

                Guid warehouseId = index % 2 == 0 ? seed.WarehouseId : seed.DestinationWarehouseId;
                keys.Add((warehouseId, material.Id));
                drafts.Add(new MovementDraft(
                    warehouseId,
                    material.Id,
                    document.Id,
                    lineResult.Value.Id,
                    MovementType.Receipt,
                    1m));
            }

            await context.SaveChangesAsync();
            var signedAttachment = DocumentAttachment.Create(
                Guid.NewGuid(),
                document.Id,
                AttachmentType.SignedOriginal,
                $"/ledger/{Guid.NewGuid():N}.pdf",
                "ledger-signed.pdf",
                "application/pdf",
                1024,
                "ledger-hash",
                seed.KeeperUserId,
                DateTime.UtcNow);
            context.DocumentAttachments.Add(signedAttachment);
            document.SetSignedCopy(signedAttachment.Id).IsSuccess.ShouldBeTrue();
            document.UpdatePaperReference("LEDGER-PAPER", DateTime.UtcNow.Year).IsSuccess.ShouldBeTrue();
            document.Submit().IsSuccess.ShouldBeTrue();
            await context.SaveChangesAsync();
            document.MarkPosted(seed.ManagerUserId, DateTime.UtcNow).IsSuccess.ShouldBeTrue();
            await context.SaveChangesAsync();
            requestedKeys = keys;

            SqlCommandCounterInterceptor commandCounter = factory.Services
                .GetRequiredService<SqlCommandCounterInterceptor>();
            commandCounter.Reset();

            await using IDbContextTransaction transaction = await context.Database.BeginTransactionAsync();
            IInventoryLedgerWriter ledgerWriter = scope.ServiceProvider
                .GetRequiredService<IInventoryLedgerWriter>();
            Result appendResult = await ledgerWriter.AppendAsync(
                drafts,
                seed.ManagerUserId,
                DateTime.UtcNow,
                CancellationToken.None);
            appendResult.IsSuccess.ShouldBeTrue();
            await transaction.CommitAsync();

            IReadOnlyList<string> commands = commandCounter.GetCommandTexts();
            AssertSetBasedPipeline(commands);
        }

        // Assert
        await using AsyncServiceScope verificationScope = factory.Services.CreateAsyncScope();
        ApplicationDbContext verificationContext = verificationScope.ServiceProvider
            .GetRequiredService<ApplicationDbContext>();
        Guid[] materialIds = requestedKeys.Select(key => key.MaterialId).ToArray();
        List<(Guid WarehouseId, Guid MaterialId, decimal Quantity)> balances = await verificationContext
            .InventoryBalances
            .AsNoTracking()
            .Where(balance =>
                (balance.WarehouseId == seed.WarehouseId ||
                 balance.WarehouseId == seed.DestinationWarehouseId) &&
                materialIds.Contains(balance.MaterialId))
            .Select(balance => new ValueTuple<Guid, Guid, decimal>(
                balance.WarehouseId,
                balance.MaterialId,
                balance.Quantity))
            .ToListAsync();

        balances.Count.ShouldBe(pairCount);
        balances.Select(balance => (balance.WarehouseId, balance.MaterialId))
            .ToHashSet()
            .SetEquals(requestedKeys)
            .ShouldBeTrue();
        balances.ShouldAllBe(balance => balance.Quantity == 1m);
    }

    private static void AssertSetBasedPipeline(IReadOnlyList<string> commands)
    {
        string balanceCreation = GetSingleCommand(
            commands,
            command => command.Contains("INSERT INTO public.inventory_balances", StringComparison.OrdinalIgnoreCase));
        string balanceLock = GetSingleCommand(
            commands,
            command => command.Contains("FROM public.inventory_balances", StringComparison.OrdinalIgnoreCase) &&
                       command.Contains("FOR UPDATE", StringComparison.OrdinalIgnoreCase));
        string balanceTotals = GetSingleCommand(
            commands,
            command => command.Contains("COALESCE(SUM(movement.quantity_delta), 0)", StringComparison.OrdinalIgnoreCase));

        balanceCreation.ShouldContain("unnest", Case.Insensitive);
        balanceLock.ShouldContain("unnest", Case.Insensitive);
        balanceTotals.ShouldContain("unnest", Case.Insensitive);
        balanceLock.ShouldNotContain("= ANY", Case.Insensitive);
        balanceTotals.ShouldNotContain("= ANY", Case.Insensitive);
    }

    private static string GetSingleCommand(
        IEnumerable<string> commands,
        Func<string, bool> predicate)
    {
        var matches = commands.Where(predicate).ToList();
        matches.Count.ShouldBe(1);
        return matches.Single();
    }
}
