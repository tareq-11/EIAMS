using FluentValidation;

namespace Application.ReturnInfos.Upsert;

internal sealed class UpsertReturnInfoCommandValidator : AbstractValidator<UpsertReturnInfoCommand>
{
    public UpsertReturnInfoCommandValidator()
    {
        RuleFor(command => command.DocumentId).NotEmpty();
        RuleFor(command => command.OriginalIssueDocumentId).NotEmpty();
        RuleFor(command => command.ReturnReason).NotEmpty().MaximumLength(200);
        RuleFor(command => command.ExpectedRowVersion).GreaterThan(0);
    }
}
