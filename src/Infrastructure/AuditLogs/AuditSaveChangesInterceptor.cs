using System.Runtime.CompilerServices;
using Application.Abstractions.Audit;
using Domain.AuditLogs;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Metadata;
using SharedKernel;

namespace Infrastructure.AuditLogs;

/// <summary>
/// Captures field-level audit rows for tracked business entities during SaveChanges, appending
/// <see cref="AuditLog"/>/<see cref="AuditLogEntry"/> rows to the SAME context so business data
/// and its audit trail commit atomically. The interceptor never calls SaveChanges itself; audit
/// types are excluded unconditionally (recursion guard); generated graphs are tracked per save
/// attempt so a failed save detaches exactly what it generated and a retry regenerates without
/// duplicating rows.
/// </summary>
internal sealed class AuditSaveChangesInterceptor(
    IAuditOperationContextAccessor auditContext,
    AuditEntityRegistry registry,
    IAuditValuePolicy valuePolicy,
    IDateTimeProvider dateTimeProvider)
    : SaveChangesInterceptor
{
    private readonly ConditionalWeakTable<DbContext, List<EntityEntry>> pendingGraphs = new();

    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
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
        ClearAttemptState(eventData.Context);

        return base.SavedChanges(eventData, result);
    }

    public override ValueTask<int> SavedChangesAsync(
        SaveChangesCompletedEventData eventData,
        int result,
        CancellationToken cancellationToken = default)
    {
        ClearAttemptState(eventData.Context);

        return base.SavedChangesAsync(eventData, result, cancellationToken);
    }

    public override void SaveChangesFailed(DbContextErrorEventData eventData)
    {
        DetachGeneratedGraph(eventData.Context);

        base.SaveChangesFailed(eventData);
    }

    public override Task SaveChangesFailedAsync(
        DbContextErrorEventData eventData,
        CancellationToken cancellationToken = default)
    {
        DetachGeneratedGraph(eventData.Context);

        return base.SaveChangesFailedAsync(eventData, cancellationToken);
    }

    public override void SaveChangesCanceled(DbContextEventData eventData)
    {
        DetachGeneratedGraph(eventData.Context);

        base.SaveChangesCanceled(eventData);
    }

    public override Task SaveChangesCanceledAsync(
        DbContextEventData eventData,
        CancellationToken cancellationToken = default)
    {
        DetachGeneratedGraph(eventData.Context);

        return base.SaveChangesCanceledAsync(eventData, cancellationToken);
    }

    private void Capture(DbContext context)
    {
        // EF may invoke interception more than once while resolving a failed/concurrent save.
        // A save attempt owns exactly one generated graph; success clears it and failure detaches
        // it before a caller retries the same business changes.
        if (pendingGraphs.TryGetValue(context, out _))
        {
            return;
        }

        context.ChangeTracker.DetectChanges();

        AuditOperationDescriptor descriptor =
            auditContext.Current ?? CreateSystemDescriptor();

        Dictionary<object, HeaderDraft> draftsByInstance = new(ReferenceEqualityComparer.Instance);

        foreach (EntityEntry entry in context.ChangeTracker.Entries())
        {
            EntityState state = entry.State;

            if (state is not (EntityState.Added or EntityState.Modified or EntityState.Deleted))
            {
                continue;
            }

            // Unmapped types - including AuditLog/AuditLogEntry - are skipped unconditionally.
            if (!registry.TryGet(entry.Entity.GetType(), out AuditEntityRegistry.Mapping? mapping))
            {
                continue;
            }

            CaptureEntry(entry, state, mapping, descriptor, draftsByInstance);
        }

        List<EntityEntry> generated = [];

        foreach (AuditSyntheticSubject subject in descriptor.SyntheticSubjects)
        {
            generated.Add(context.Add(CreateHeader(
                descriptor,
                subject.EntityType,
                subject.EntityId,
                aggregateType: null,
                aggregateId: null,
                action: subject.Action,
                commandName: subject.CommandName,
                summary: subject.Summary,
                actorUserId: subject.UserId)));
        }

        foreach (HeaderDraft draft in draftsByInstance.Values)
        {
            EntityEntry header = context.Add(CreateHeader(
                descriptor,
                draft.EntityType,
                draft.EntityId,
                draft.AggregateType,
                draft.AggregateId,
                draft.Action,
                descriptor.CommandName,
                summary: null));

            generated.Add(header);

            foreach ((string fieldName, string? oldValue, string? newValue) in draft.Changes)
            {
                generated.Add(context.Add(CreateEntry(header, fieldName, oldValue, newValue)));
            }
        }

        if (generated.Count > 0)
        {
            pendingGraphs.AddOrUpdate(context, generated);
        }
        else
        {
            pendingGraphs.Remove(context);
        }
    }

    private void CaptureEntry(
        EntityEntry entry,
        EntityState state,
        AuditEntityRegistry.Mapping mapping,
        AuditOperationDescriptor descriptor,
        Dictionary<object, HeaderDraft> draftsByInstance)
    {
        string entityType = mapping.EntityType;

        Guid? entityId = ResolveEntityId(mapping, entry, state);

        if (entityId is null || entityId.Value == Guid.Empty)
        {
            return;
        }

        (string? AggregateType, Guid? AggregateId) aggregate =
            ResolveAggregate(mapping, descriptor, entityType, entry.Entity);

        StoreObjectIdentifier? table =
            StoreObjectIdentifier.Create(entry.Metadata, StoreObjectType.Table);

        if (table is null)
        {
            return;
        }

        var changes = new List<(string FieldName, string? OldValue, string? NewValue)>();
        string? transitionAction = null;

        foreach (PropertyEntry property in entry.Properties)
        {
            IProperty metadata = property.Metadata;

            if (metadata.IsShadowProperty())
            {
                continue;
            }

            string? fieldName = metadata.GetColumnName(table.Value);

            if (fieldName is null ||
                valuePolicy.IsExcludedField(entityType, fieldName) ||
                valuePolicy.IsForbidden(entityType, fieldName))
            {
                continue;
            }

            object? currentValue = property.CurrentValue;

            if (currentValue is byte[])
            {
                continue;
            }

            string? newValue = valuePolicy.Serialize(entityType, fieldName, currentValue);

            string? oldValue;

            if (state == EntityState.Added)
            {
                oldValue = null;
            }
            else
            {
                object? originalValue = property.OriginalValue;

                if (originalValue is byte[])
                {
                    continue;
                }

                oldValue = valuePolicy.Serialize(entityType, fieldName, originalValue);
            }

            switch (state)
            {
                case EntityState.Added when newValue is not null:
                    changes.Add((fieldName, null, newValue));
                    break;

                case EntityState.Deleted when oldValue is not null:
                    changes.Add((fieldName, oldValue, null));
                    break;

                case EntityState.Added or EntityState.Deleted:
                    break;

                default:
                    if (!string.Equals(oldValue, newValue, StringComparison.Ordinal))
                    {
                        changes.Add((fieldName, oldValue, newValue));

                        transitionAction ??= AuditTransitionResolver.Resolve(
                            entityType,
                            fieldName,
                            oldValue,
                            newValue);
                    }

                    break;
            }
        }

        // An update that produced no non-excluded differences carries no evidence: skip it.
        if (state == EntityState.Modified && changes.Count == 0)
        {
            return;
        }

        string action =
            descriptor.ActionOverrides.TryGetValue(entityType, out string? overridden)
                ? overridden
                : transitionAction ?? BaselineAction(state);

        if (!draftsByInstance.TryGetValue(entry.Entity, out HeaderDraft? draft))
        {
            draft = new HeaderDraft(
                entityType,
                entityId.Value,
                aggregate.AggregateType,
                aggregate.AggregateId,
                action);

            draftsByInstance.Add(entry.Entity, draft);
        }

        draft.Changes.AddRange(changes);
    }

    private static Guid? ResolveEntityId(
        AuditEntityRegistry.Mapping mapping,
        EntityEntry entry,
        EntityState state)
    {
        Guid? selected = SelectSafely(mapping.EntityIdSelector, entry.Entity);

        if (selected.HasValue && selected.Value != Guid.Empty)
        {
            return selected;
        }

        IProperty? singleKeyProperty = entry.Metadata.FindPrimaryKey()?.Properties is { Count: 1 } properties
            ? properties[0]
            : null;

        if (singleKeyProperty is null)
        {
            return null;
        }

        PropertyEntry keyProperty = entry.Property(singleKeyProperty.Name);

        object? keyValue =
            state == EntityState.Deleted ? keyProperty.OriginalValue : keyProperty.CurrentValue;

        return keyValue as Guid?;
    }

    private static Guid? SelectSafely(Func<object, Guid?> selector, object entity)
    {
        try
        {
            return selector(entity);
        }
        catch (InvalidCastException)
        {
            return null;
        }
    }

    private static (string? AggregateType, Guid? AggregateId) ResolveAggregate(
        AuditEntityRegistry.Mapping mapping,
        AuditOperationDescriptor descriptor,
        string entityType,
        object entity)
    {
        if (descriptor.AggregateOverrides.TryGetValue(
                entityType,
                out AuditOperationDescriptor.AggregateOverride overrideValue))
        {
            return (overrideValue.AggregateType, overrideValue.AggregateId);
        }

        if (mapping.AggregateType is null || mapping.AggregateIdSelector is null)
        {
            return (null, null);
        }

        return (mapping.AggregateType, SelectSafely(mapping.AggregateIdSelector, entity));
    }

    private static string BaselineAction(EntityState state) => state switch
    {
        EntityState.Added => AuditActions.Create,
        EntityState.Deleted => AuditActions.Delete,
        _ => AuditActions.Update
    };

    private AuditOperationDescriptor CreateSystemDescriptor() =>
        new(Guid.NewGuid(), null, null, null, null, AuditOperationKind.System);

    private AuditLog CreateHeader(
        AuditOperationDescriptor descriptor,
        string entityType,
        Guid entityId,
        string? aggregateType,
        Guid? aggregateId,
        string action,
        string? commandName,
        string? summary,
        Guid? actorUserId = null)
    {
        Result<AuditLog> creation = AuditLog.Create(
            Guid.NewGuid(),
            descriptor.OperationId,
            descriptor.RequestId,
            actorUserId ?? descriptor.UserId,
            entityType,
            entityId,
            aggregateType,
            aggregateId,
            action,
            commandName,
            valuePolicy.SanitizeSummary(summary),
            descriptor.IpAddress,
            dateTimeProvider.UtcNow);

        if (creation.IsFailure)
        {
            throw new InvalidOperationException(
                $"Audit capture produced an invalid header for {entityType}: {creation.Error.Code} {creation.Error.Description}.");
        }

        return creation.Value;
    }

    private AuditLogEntry CreateEntry(EntityEntry header, string fieldName, string? oldValue, string? newValue)
    {
        Result<AuditLogEntry> creation = AuditLogEntry.Create(
            Guid.NewGuid(),
            ((AuditLog)header.Entity).Id,
            fieldName,
            oldValue,
            newValue);

        if (creation.IsFailure)
        {
            throw new InvalidOperationException(
                $"Audit capture produced an invalid entry for field {fieldName}: {creation.Error.Code} {creation.Error.Description}.");
        }

        return creation.Value;
    }

    private void ClearAttemptState(DbContext? context)
    {
        if (context is not null)
        {
            pendingGraphs.Remove(context);
        }
    }

    private void DetachGeneratedGraph(DbContext? context)
    {
        if (context is null || !pendingGraphs.TryGetValue(context, out List<EntityEntry>? generated))
        {
            return;
        }

        pendingGraphs.Remove(context);

        foreach (EntityEntry entry in generated)
        {
            if (entry.State is not EntityState.Detached)
            {
                entry.State = EntityState.Detached;
            }
        }
    }

    private sealed class HeaderDraft(
        string entityType,
        Guid entityId,
        string? aggregateType,
        Guid? aggregateId,
        string action)
    {
        public string EntityType { get; } = entityType;

        public Guid EntityId { get; } = entityId;

        public string? AggregateType { get; } = aggregateType;

        public Guid? AggregateId { get; } = aggregateId;

        public string Action { get; } = action;

        public List<(string FieldName, string? OldValue, string? NewValue)> Changes { get; } = [];
    }
}
