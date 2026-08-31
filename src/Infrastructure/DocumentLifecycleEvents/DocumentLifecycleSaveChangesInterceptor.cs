using System.Runtime.CompilerServices;
using Application.Abstractions.Audit;
using Application.Abstractions.Authentication;
using Domain.Common;
using Domain.DocumentLifecycleEvents;
using Domain.WarehouseDocuments;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using SharedKernel;

namespace Infrastructure.DocumentLifecycleEvents;

/// <summary>Adds immutable lifecycle evidence to the same unit of work as the state transition.</summary>
internal sealed class DocumentLifecycleSaveChangesInterceptor(
    IAuditOperationContextAccessor auditContext,
    IUserContext userContext,
    IDateTimeProvider dateTimeProvider) : SaveChangesInterceptor
{
    private readonly ConditionalWeakTable<DbContext, AttemptState> attempts = new();

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        if (eventData.Context is not null)
        {
            Capture(eventData.Context);
        }
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context is not null)
        {
            Capture(eventData.Context);
        }
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    public override int SavedChanges(SaveChangesCompletedEventData eventData, int result)
    {
        Complete(eventData.Context);
        return base.SavedChanges(eventData, result);
    }

    public override ValueTask<int> SavedChangesAsync(
        SaveChangesCompletedEventData eventData,
        int result,
        CancellationToken cancellationToken = default)
    {
        Complete(eventData.Context);
        return base.SavedChangesAsync(eventData, result, cancellationToken);
    }

    public override void SaveChangesFailed(DbContextErrorEventData eventData)
    {
        RollBackAttempt(eventData.Context);
        base.SaveChangesFailed(eventData);
    }

    public override Task SaveChangesFailedAsync(DbContextErrorEventData eventData, CancellationToken cancellationToken = default)
    {
        RollBackAttempt(eventData.Context);
        return base.SaveChangesFailedAsync(eventData, cancellationToken);
    }

    public override void SaveChangesCanceled(DbContextEventData eventData)
    {
        RollBackAttempt(eventData.Context);
        base.SaveChangesCanceled(eventData);
    }

    public override Task SaveChangesCanceledAsync(DbContextEventData eventData, CancellationToken cancellationToken = default)
    {
        RollBackAttempt(eventData.Context);
        return base.SaveChangesCanceledAsync(eventData, cancellationToken);
    }

    private void Capture(DbContext context)
    {
        RejectLifecycleMutations(context);
        if (attempts.TryGetValue(context, out _))
        {
            return;
        }

        context.ChangeTracker.DetectChanges();
        AuditOperationDescriptor? operation = auditContext.Current;
        var generated = new List<EntityEntry>();
        var documents = new List<WarehouseDocument>();

        var documentEntries = context.ChangeTracker
            .Entries<WarehouseDocument>()
            .ToList();

        foreach (EntityEntry<WarehouseDocument> entry in documentEntries)
        {
            DocumentStatus? fromStatus;
            string action;

            if (entry.State == EntityState.Added)
            {
                fromStatus = null;
                action = "Created";
            }
            else if (entry.State == EntityState.Modified && entry.Property(item => item.DocumentStatus).IsModified)
            {
                fromStatus = entry.Property(item => item.DocumentStatus).OriginalValue;
                action = MapAction(fromStatus.Value, entry.Entity.DocumentStatus);
            }
            else
            {
                continue;
            }

            Guid operationId = operation?.OperationId ?? Guid.NewGuid();
            var lifecycleEvent = DocumentLifecycleEvent.Create(
                Guid.NewGuid(),
                entry.Entity.Id,
                fromStatus,
                entry.Entity.DocumentStatus,
                action,
                entry.Entity.PendingLifecycleReason,
                operation?.UserId ?? userContext.UserIdOrDefault,
                userContext.ActorDisplayName,
                dateTimeProvider.UtcNow,
                entry.Entity.RowVersion,
                operation?.RequestId,
                operationId);

            generated.Add(context.Add(lifecycleEvent));
            documents.Add(entry.Entity);
        }

        if (generated.Count > 0)
        {
            attempts.Add(context, new AttemptState(generated, documents));
        }
    }

    private static void RejectLifecycleMutations(DbContext context)
    {
        if (context.ChangeTracker.Entries<DocumentLifecycleEvent>()
            .Any(entry => entry.State is EntityState.Modified or EntityState.Deleted))
        {
            throw new InvalidOperationException("Document lifecycle events are append-only and cannot be updated or deleted.");
        }
    }

    private static string MapAction(DocumentStatus from, DocumentStatus to) => (from, to) switch
    {
        (DocumentStatus.Draft, DocumentStatus.Submitted) => "Submitted",
        (DocumentStatus.Submitted, DocumentStatus.Posted) => "Posted",
        (DocumentStatus.Submitted, DocumentStatus.Rejected) => "Rejected",
        (DocumentStatus.Rejected, DocumentStatus.Draft) => "RevisionStarted",
        (_, DocumentStatus.Cancelled) => "Cancelled",
        (DocumentStatus.Posted, DocumentStatus.Reversed) => "Reversed",
        _ => throw new InvalidOperationException($"Unsupported document lifecycle transition {from} -> {to}.")
    };

    private void Complete(DbContext? context)
    {
        if (context is null || !attempts.TryGetValue(context, out AttemptState? attempt))
        {
            return;
        }

        foreach (WarehouseDocument document in attempt.Documents)
        {
            document.ClearPendingLifecycleReason();
        }

        attempts.Remove(context);
    }

    private void RollBackAttempt(DbContext? context)
    {
        if (context is null || !attempts.TryGetValue(context, out AttemptState? attempt))
        {
            return;
        }

        foreach (EntityEntry entry in attempt.GeneratedEntries)
        {
            entry.State = EntityState.Detached;
        }

        attempts.Remove(context);
    }

    private sealed record AttemptState(List<EntityEntry> GeneratedEntries, List<WarehouseDocument> Documents);
}
