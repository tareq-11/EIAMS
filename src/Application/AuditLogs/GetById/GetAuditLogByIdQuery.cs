using Application.Abstractions.Messaging;

namespace Application.AuditLogs.GetById;

public sealed record GetAuditLogByIdQuery(Guid AuditLogId) : IQuery<AuditLogDetailsResponse>;
