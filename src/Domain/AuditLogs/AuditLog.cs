using System.Text;
using System.Text.Json;
using SharedKernel;

namespace Domain.AuditLogs;

/// <summary>
/// One immutable audit record of a system operation. Audit rows are append-only by design: they
/// raise no domain events, carry no audit-of-audit columns, and the database rejects UPDATE/DELETE
/// via trigger (see the M8 migration).
/// </summary>
public sealed class AuditLog : Entity
{
    public const int MaxRequestIdLength = 64;
    public const int MaxEntityTypeLength = 100;
    public const int MaxActionLength = 50;
    public const int MaxCommandNameLength = 150;
    public const int MaxIpAddressLength = 45;
    public const int MaxSummaryBytes = 16384;

    private AuditLog() { }

    public Guid OperationId { get; private set; }

    public string? RequestId { get; private set; }

    public Guid? UserId { get; private set; }

    public string EntityType { get; private set; }

    public Guid EntityId { get; private set; }

    public string? AggregateType { get; private set; }

    public Guid? AggregateId { get; private set; }

    public string Action { get; private set; }

    public string? CommandName { get; private set; }

    public string? Summary { get; private set; }

    public string? IpAddress { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    public static Result<AuditLog> Create(
        Guid id,
        Guid operationId,
        string? requestId,
        Guid? userId,
        string entityType,
        Guid entityId,
        string? aggregateType,
        Guid? aggregateId,
        string action,
        string? commandName,
        string? summary,
        string? ipAddress,
        DateTime createdAtUtc)
    {
        if (id == Guid.Empty || operationId == Guid.Empty)
        {
            return Result.Failure<AuditLog>(AuditLogErrors.OperationIdRequired);
        }

        if (entityId == Guid.Empty)
        {
            return Result.Failure<AuditLog>(AuditLogErrors.EntityIdRequired);
        }

        if (requestId?.Length > MaxRequestIdLength)
        {
            return Result.Failure<AuditLog>(AuditLogErrors.RequestIdTooLong);
        }

        if (!IsCanonicalName(entityType, MaxEntityTypeLength))
        {
            return Result.Failure<AuditLog>(AuditLogErrors.EntityTypeInvalid);
        }

        if (userId == Guid.Empty)
        {
            return Result.Failure<AuditLog>(AuditLogErrors.UserIdInvalid);
        }

        if (aggregateType is not null && !IsCanonicalName(aggregateType, MaxEntityTypeLength))
        {
            return Result.Failure<AuditLog>(AuditLogErrors.AggregateTypeInvalid);
        }

        if (string.IsNullOrWhiteSpace(action) || action.Length > MaxActionLength || !AuditActions.All.Contains(action))
        {
            return Result.Failure<AuditLog>(AuditLogErrors.ActionInvalid);
        }

        if (commandName?.Length > MaxCommandNameLength)
        {
            return Result.Failure<AuditLog>(AuditLogErrors.CommandNameTooLong);
        }

        if (summary is not null)
        {
            if (!IsValidJsonObject(summary))
            {
                return Result.Failure<AuditLog>(AuditLogErrors.SummaryNotJsonObject);
            }

            if (Encoding.UTF8.GetByteCount(summary) > MaxSummaryBytes)
            {
                return Result.Failure<AuditLog>(AuditLogErrors.SummaryTooLarge);
            }
        }

        if (ipAddress?.Length > MaxIpAddressLength)
        {
            return Result.Failure<AuditLog>(AuditLogErrors.IpAddressTooLong);
        }

        if (createdAtUtc.Kind != DateTimeKind.Utc)
        {
            return Result.Failure<AuditLog>(AuditLogErrors.TimestampNotUtc);
        }

        var auditLog = new AuditLog
        {
            Id = id,
            OperationId = operationId,
            RequestId = requestId,
            UserId = userId,
            EntityType = entityType,
            EntityId = entityId,
            AggregateType = aggregateType,
            AggregateId = aggregateId,
            Action = action,
            CommandName = commandName,
            Summary = summary,
            IpAddress = ipAddress,
            CreatedAtUtc = createdAtUtc
        };

        return auditLog;
    }

    private static bool IsCanonicalName(string candidate, int maxLength) =>
        !string.IsNullOrWhiteSpace(candidate) &&
        candidate.Length <= maxLength &&
        KnownAuditEntityTypes.IsKnown(candidate);

    private static bool IsValidJsonObject(string candidate)
    {
        try
        {
            using var document = JsonDocument.Parse(candidate);

            return document.RootElement.ValueKind == JsonValueKind.Object;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
