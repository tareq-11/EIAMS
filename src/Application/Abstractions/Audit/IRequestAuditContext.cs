namespace Application.Abstractions.Audit;

/// <summary>
/// Provides HTTP request metadata (request id, client IP) captured by the host,
/// safe to call from non-HTTP contexts (returns <see langword="null"/>).
/// </summary>
public interface IRequestAuditContext
{
    string? GetRequestId();

    string? GetClientIpAddress();
}
