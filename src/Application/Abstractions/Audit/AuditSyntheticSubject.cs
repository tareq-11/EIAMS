namespace Application.Abstractions.Audit;

/// <summary>
/// A coarse-grained, secret-free audit subject describing an effect that cannot be expressed as a
/// per-entity diff (e.g. "user X authenticated successfully"). Enqueued onto the current operation.
/// </summary>
public sealed record AuditSyntheticSubject(
    Guid EntityId,
    string EntityType,
    Guid? UserId,
    string Action,
    string? CommandName,
    string? Summary);
