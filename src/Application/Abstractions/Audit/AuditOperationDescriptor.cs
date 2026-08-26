namespace Application.Abstractions.Audit;

/// <summary>
/// Describes the operation (command execution) currently running, for later audit persistence.
/// Created per command by the audit decorator; mutated through its methods as handlers
/// register entity overrides and synthetic subjects. Thread-safe.
/// </summary>
/// <summary>
/// Describes the operation (command execution) currently running, for later audit persistence.
/// Created per command by the audit decorator; mutated through its methods as handlers
/// register entity overrides and synthetic subjects. Thread-safe.
/// </summary>
public sealed class AuditOperationDescriptor(
    Guid operationId,
    string? requestId,
    Guid? userId,
    string? ipAddress,
    string? commandName,
    AuditOperationKind kind)
{
    private readonly Lock _lock = new();
    private readonly Dictionary<string, string> _actionOverrides = new(StringComparer.Ordinal);
    private readonly Dictionary<string, AggregateOverride> _aggregateOverrides = new(StringComparer.Ordinal);
    private readonly List<AuditSyntheticSubject> _syntheticSubjects = [];

    public Guid OperationId { get; } = operationId;

    public string? RequestId { get; } = requestId;

    public Guid? UserId { get; } = userId;

    public string? IpAddress { get; } = ipAddress;

    public string? CommandName { get; } = commandName;

    public AuditOperationKind Kind { get; } = kind;

    /// <summary>Snapshot of the per-entity action overrides (canonical entity-type name → action).</summary>
    public IReadOnlyDictionary<string, string> ActionOverrides
    {
        get
        {
            lock (_lock)
            {
                return new Dictionary<string, string>(_actionOverrides, StringComparer.Ordinal);
            }
        }
    }

    /// <summary>Snapshot of the per-entity aggregate overrides (canonical entity-type name → owning aggregate).</summary>
    public IReadOnlyDictionary<string, AggregateOverride> AggregateOverrides
    {
        get
        {
            lock (_lock)
            {
                return new Dictionary<string, AggregateOverride>(_aggregateOverrides, StringComparer.Ordinal);
            }
        }
    }

    /// <summary>Snapshot of the queued synthetic subjects.</summary>
    public IReadOnlyList<AuditSyntheticSubject> SyntheticSubjects
    {
        get
        {
            lock (_lock)
            {
                return [.. _syntheticSubjects];
            }
        }
    }

    public void AddActionOverride(string entityType, string action)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(entityType);
        ArgumentException.ThrowIfNullOrWhiteSpace(action);

        lock (_lock)
        {
            _actionOverrides[entityType] = action;
        }
    }

    public void AddAggregateOverride(string entityType, string aggregateType, Guid aggregateId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(entityType);
        ArgumentException.ThrowIfNullOrWhiteSpace(aggregateType);
        ArgumentOutOfRangeException.ThrowIfEqual(aggregateId, Guid.Empty);

        lock (_lock)
        {
            _aggregateOverrides[entityType] = new AggregateOverride(aggregateType, aggregateId);
        }
    }

    public void EnqueueSynthetic(AuditSyntheticSubject subject)
    {
        ArgumentNullException.ThrowIfNull(subject);

        lock (_lock)
        {
            _syntheticSubjects.Add(subject);
        }
    }

    public readonly record struct AggregateOverride(string AggregateType, Guid AggregateId);
}
