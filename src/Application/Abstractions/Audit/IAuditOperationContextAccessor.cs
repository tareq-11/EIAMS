namespace Application.Abstractions.Audit;

/// <summary>
/// Tracks the audit descriptor of the operation executing in the current async flow.
/// Handlers (or decorators) open a scope via <see cref="BeginScope"/>; anything running
/// inside the scope - including nested DI scopes and background flows - observes the
/// same descriptor through <see cref="Current"/>.
/// </summary>
public interface IAuditOperationContextAccessor
{
    AuditOperationDescriptor? Current { get; }

    /// <summary>Pushes the descriptor as the current audit frame until the returned scope is disposed.</summary>
    IDisposable BeginScope(AuditOperationDescriptor descriptor);

    /// <summary>Enqueues a synthetic subject onto the current descriptor. No-op when no scope is open.</summary>
    void RecordSynthetic(AuditSyntheticSubject subject);
}
