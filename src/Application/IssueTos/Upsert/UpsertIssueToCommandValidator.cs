using FluentValidation;

namespace Application.IssueTos.Upsert;

internal sealed class UpsertIssueToCommandValidator : AbstractValidator<UpsertIssueToCommand>
{
    public UpsertIssueToCommandValidator()
    {
        RuleFor(command => command.DocumentId).NotEmpty();
        RuleFor(command => command.RecipientType).IsInEnum();
        RuleFor(command => command.RecipientId).NotEmpty();
        RuleFor(command => command.IssueReason).NotEmpty().MaximumLength(200);
        RuleFor(command => command.ExpectedRowVersion).GreaterThan(0);
    }
}
