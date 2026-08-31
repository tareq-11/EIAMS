using System.Text.RegularExpressions;
using Application.Abstractions.Pagination;
using Domain.AuditLogs;
using FluentValidation;

namespace Application.AuditLogs.GetAuditLogs;

public sealed partial class GetAuditLogsQueryValidator : AbstractValidator<GetAuditLogsQuery>
{
    [GeneratedRegex("^[a-z][a-z0-9_]{0,99}$", RegexOptions.CultureInvariant)]
    private static partial Regex FieldNamePattern();

    public GetAuditLogsQueryValidator()
    {
        RuleFor(q => q.Page)
            .GreaterThanOrEqualTo(PaginationDefaults.DefaultPage);

        RuleFor(q => q.PageSize)
            .InclusiveBetween(1, PaginationDefaults.MaximumPageSize);

        RuleFor(q => q.EntityType)
            .Must(entityType => entityType is null || KnownAuditEntityTypes.IsKnown(entityType))
            .WithMessage("The specified entity type is unknown or invalid.");

        RuleFor(q => q.Action)
            .Must(action => action is null || AuditActions.All.Contains(action))
            .WithMessage("The specified audit action is invalid.");

        RuleFor(q => q.FieldName)
            .Must(fieldName => fieldName is null || FieldNamePattern().IsMatch(fieldName))
            .WithMessage("The specified field name is invalid.");

        RuleFor(q => q)
            .Must(q => !q.FromUtc.HasValue || !q.ToUtc.HasValue || q.FromUtc.Value < q.ToUtc.Value)
            .WithMessage("FromUtc must be earlier than ToUtc.");
    }
}
