using Application.Abstractions.Audit;

namespace Infrastructure.AuditLogs;

internal sealed class AuditOperationContextAccessor : IAuditOperationContextAccessor
{
    public AuditOperationDescriptor? Current => AuditOperationStack.Peek();

    public IDisposable BeginScope(AuditOperationDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);

        return AuditOperationStack.Push(descriptor);
    }

    public void RecordSynthetic(AuditSyntheticSubject subject)
    {
        ArgumentNullException.ThrowIfNull(subject);

        AuditOperationStack.Record(subject);
    }
}

// Holds the per-async-flow audit frames. AsyncLocal gives every async flow (and any DI scope
// created inside it) its own copy of the current frame; mutations never leak across sibling
// or parent flows. All reads/writes go through static methods so no flow owns the storage.
internal static class AuditOperationStack
{
    private static readonly AsyncLocal<Frame?> CurrentFrame = new();

    private static readonly Frame EmptyFrame = new(null);

    public static AuditOperationDescriptor? Peek() => CurrentFrame.Value?.Descriptor;

    public static IDisposable Push(AuditOperationDescriptor descriptor)
    {
        Frame current = CurrentFrame.Value ?? EmptyFrame;

        Set(new Frame(descriptor));

        return new Scope(previous: current);
    }

    public static void Record(AuditSyntheticSubject subject) => CurrentFrame.Value?.Descriptor?.EnqueueSynthetic(subject);

    private static void Set(Frame frame) => CurrentFrame.Value = frame;

    private sealed class Frame(AuditOperationDescriptor? descriptor)
    {
        public AuditOperationDescriptor? Descriptor { get; } = descriptor;
    }

    private sealed class Scope(Frame previous) : IDisposable
    {
        private int _disposed;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, DisposedFlag) != 0)
            {
                return;
            }

            Set(previous);
        }
    }

    private const int DisposedFlag = 1;
}
