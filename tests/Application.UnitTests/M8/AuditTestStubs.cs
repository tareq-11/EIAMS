using Application.Abstractions.Audit;
using Application.Abstractions.Authentication;
using SharedKernel;

namespace Application.UnitTests.M8;

internal sealed class FakeAuditOperationContextAccessor : IAuditOperationContextAccessor
{
    public AuditOperationDescriptor? Current { get; internal set; }

    public int BeginScopeCallCount { get; private set; }

    public IDisposable BeginScope(AuditOperationDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);

        BeginScopeCallCount++;
        AuditOperationDescriptor? previous = Current;
        Current = descriptor;

        return new FakeScope(this, previous);
    }

    public void RecordSynthetic(AuditSyntheticSubject subject)
    {
    }

    private sealed class FakeScope(FakeAuditOperationContextAccessor accessor, AuditOperationDescriptor? previous)
        : IDisposable
    {
        public void Dispose() => accessor.Current = previous;
    }
}

internal sealed class StubRequestAuditContext(string? requestId) : IRequestAuditContext
{
    public string? GetClientIpAddress() => "192.168.1.10";

    public string? GetRequestId() => requestId;
}

internal sealed class StubUserContext(Guid? userId) : IUserContext
{
    public Guid UserId => userId ?? Guid.Empty;

    public Guid? UserIdOrDefault => userId;
}
