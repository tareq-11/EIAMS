using Domain.Common;
using Domain.DocumentLifecycleEvents;
using Domain.Organizations;
using Domain.Sites;
using Domain.WarehouseDocuments;
using Domain.Warehouses;
using Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace IntegrationTests.Phase6;

[Collection(nameof(IntegrationTestCollection))]
public sealed class DocumentLifecyclePersistenceTests(IntegrationTestWebAppFactory factory)
{
    [Fact]
    public async Task SavingDocumentTransitions_Should_AppendActualEventsWithResultingVersions()
    {
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        WarehouseDocument document = AddDraftGraph(context);

        await context.SaveChangesAsync();
        document.UpdatePaperReference("P-6", 2026).IsSuccess.ShouldBeTrue();
        await context.SaveChangesAsync();
        document.Submit().IsSuccess.ShouldBeTrue();
        await context.SaveChangesAsync();
        document.Reject("بيانات المستند بحاجة إلى مراجعة").IsSuccess.ShouldBeTrue();
        await context.SaveChangesAsync();

        List<DocumentLifecycleEvent> events = await context.DocumentLifecycleEvents.AsNoTracking()
            .Where(item => item.DocumentId == document.Id)
            .OrderBy(item => item.OccurredAtUtc)
            .ThenBy(item => item.Id)
            .ToListAsync();

        events.Select(item => item.Action).ShouldBe(["Created", "Submitted", "Rejected"]);
        events.Select(item => item.ResultingRowVersion).ShouldBe([1, 3, 4]);
        events[^1].Reason.ShouldBe("بيانات المستند بحاجة إلى مراجعة");
        events.ShouldAllBe(item => item.OperationId != Guid.Empty);
    }

    [Fact]
    public async Task LifecycleEvent_Should_RejectDelete_InApplicationAndDatabase()
    {
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        WarehouseDocument document = AddDraftGraph(context);
        await context.SaveChangesAsync();

        DocumentLifecycleEvent lifecycleEvent = await context.DocumentLifecycleEvents
            .SingleAsync(item => item.DocumentId == document.Id);
        context.DocumentLifecycleEvents.Remove(lifecycleEvent);

        InvalidOperationException applicationException = await Should.ThrowAsync<InvalidOperationException>(
            () => context.SaveChangesAsync());
        applicationException.Message.ShouldContain("append-only");
        context.Entry(lifecycleEvent).State = EntityState.Unchanged;

        PostgresException databaseException = await Should.ThrowAsync<PostgresException>(
            () => context.Database.ExecuteSqlInterpolatedAsync(
                $"DELETE FROM public.document_lifecycle_events WHERE id = {lifecycleEvent.Id}"));
        databaseException.SqlState.ShouldBe(PostgresErrorCodes.RaiseException);
    }

    [Fact]
    public async Task LifecycleTable_Should_HaveIdempotencyAndVersionUniquenessIndexes()
    {
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        List<string> indexNames = await context.Database.SqlQuery<string>(
            $"""
             SELECT indexname AS "Value"
             FROM pg_indexes
             WHERE schemaname = 'public' AND tablename = 'document_lifecycle_events'
             """).ToListAsync();

        indexNames.ShouldContain("ix_document_lifecycle_events_document_id_action_operation_id");
        indexNames.ShouldContain("ix_document_lifecycle_events_document_id_resulting_row_version");
    }

    private static WarehouseDocument AddDraftGraph(ApplicationDbContext context)
    {
        string suffix = Guid.NewGuid().ToString("N")[..8];
        var organization = Organization.Create(Guid.NewGuid(), $"Organization {suffix}", $"O{suffix}");
        var site = Site.Create(Guid.NewGuid(), organization.Id, $"Site {suffix}", $"S{suffix}", null);
        var warehouse = Warehouse.Create(Guid.NewGuid(), site.Id, $"Warehouse {suffix}", $"W{suffix}", "Central", true);
        var document = WarehouseDocument.CreateDraft(
            Guid.NewGuid(), warehouse.Id, DocumentType.Receiving, $"REC-2026-{suffix}");

        context.Organizations.Add(organization);
        context.Sites.Add(site);
        context.Warehouses.Add(warehouse);
        context.WarehouseDocuments.Add(document);
        return document;
    }
}
