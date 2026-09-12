using Application.Abstractions.Idempotency;
using Application.Abstractions.Posting;
using Domain.Common;
using Domain.DocumentLines;
using Domain.WarehouseDocuments;
using IntegrationTests.Regression;
using Microsoft.Extensions.DependencyInjection;
using SharedKernel;

namespace IntegrationTests.Performance;

/// <summary>
/// Keeps the intentional ordering barriers in the real receiving-post transaction observable.
/// This is not a latency benchmark: command text remains inside the test process and is never
/// emitted into an artifact or log.
/// </summary>
[Collection(nameof(IntegrationTestCollection))]
public sealed class PostingTransactionEvidenceTests(IntegrationTestWebAppFactory factory)
{
    [Fact]
    public async Task ReceivingPost_Should_PreserveRequiredLockAndPersistenceBarriers()
    {
        RegressionSeedData seed = await RegressionTestHelper.SeedAsync(factory.Services);
        WarehouseDocument document = await RegressionTestHelper.CreateAndSubmitDocumentAsync(
            factory.Services,
            seed.WarehouseId,
            DocumentType.Receiving,
            seed.KeeperUserId,
            [(seed.NormalMaterialId, DocumentLineType.Normal, 1m)]);

        SqlCommandCounterInterceptor counter = factory.Services.GetRequiredService<SqlCommandCounterInterceptor>();
        counter.Reset();

        await using (AsyncServiceScope scope = factory.Services.CreateAsyncScope())
        {
            IDocumentPostingCoordinator coordinator = scope.ServiceProvider.GetRequiredService<IDocumentPostingCoordinator>();
            Result<PostingOutcome> result = await coordinator.PostAsync(
                document.Id,
                document.RowVersion,
                seed.ManagerUserId,
                IdempotencyRequest.Create(Guid.NewGuid(), "posting-transaction-evidence", "receiving-1-1"),
                CancellationToken.None);

            result.IsSuccess.ShouldBeTrue();
        }

        IReadOnlyList<string> commands = counter.GetCommandTexts();
        SqlCommandDurationSnapshot measurements = counter.Snapshot();

        // The current Quick harness observed 23 commands for this path. Do not make that an
        // invariant: provider batching and harmless validation reads may legitimately change it.
        // The barriers below are the correctness contract that permits a future evidence-based
        // SaveChanges reduction review.
        measurements.Count.ShouldBeGreaterThan(0);

        int documentRowLock = FindCommand(commands, "warehouse_documents", "FOR UPDATE");
        // Parameter values are deliberately not retained by the test interceptor, so the first
        // advisory statement after the document row lock is the warehouse-operation lock.
        int warehouseOperationLock = FindCommandAfter(commands, documentRowLock, "pg_advisory_xact_lock");
        int inventoryKeyLock = FindCommandAfter(commands, warehouseOperationLock, "pg_advisory_xact_lock");
        int movementInsert = FindCommandAfter(commands, inventoryKeyLock, "INSERT INTO", "stock_movements");
        int ledgerTotalsRead = FindCommandAfter(commands, movementInsert, "SUM(movement.quantity_delta)");
        int balanceUpdate = FindCommandAfter(commands, ledgerTotalsRead, "UPDATE", "inventory_balances");
        int finalDocumentWrite = FindCommandAfter(commands, balanceUpdate, "UPDATE", "warehouse_documents");
        // EF may batch the final document/lifecycle/audit/idempotency graph into one database
        // command, so their relative positions inside that final SaveChanges round are not a
        // contract. They must both occur after the persisted balance round.
        int idempotencyWrite = FindCommandAfter(commands, balanceUpdate, "idempotency_records");

        documentRowLock.ShouldBeLessThan(warehouseOperationLock);
        warehouseOperationLock.ShouldBeLessThan(inventoryKeyLock);
        inventoryKeyLock.ShouldBeLessThan(movementInsert);
        movementInsert.ShouldBeLessThan(ledgerTotalsRead);
        ledgerTotalsRead.ShouldBeLessThan(balanceUpdate);
        balanceUpdate.ShouldBeLessThan(finalDocumentWrite);
        balanceUpdate.ShouldBeLessThan(idempotencyWrite);
    }

    private static int FindCommand(IReadOnlyList<string> commands, params string[] fragments) =>
        FindCommandAfter(commands, -1, fragments);

    private static int FindCommandAfter(IReadOnlyList<string> commands, int afterIndex, params string[] fragments)
    {
        for (int index = afterIndex + 1; index < commands.Count; index++)
        {
            if (fragments.All(fragment => commands[index].Contains(fragment, StringComparison.OrdinalIgnoreCase)))
            {
                return index;
            }
        }

        throw new ShouldAssertException(
            "The posting SQL evidence no longer contains an expected correctness barrier. " +
            "Review the transaction/lock/SaveChanges dependency trace before accepting the change.");
    }
}
