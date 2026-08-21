using FluentValidation;

namespace Application.InventoryAdjustments.CreateDisposal;

internal sealed class CreateDisposalCommandValidator : AbstractValidator<CreateDisposalCommand>
{
    public CreateDisposalCommandValidator()
    {
        RuleFor(command => command.WarehouseId).NotEmpty();
        RuleFor(command => command.AssetIds)
            .Cascade(CascadeMode.Stop)
            .NotNull()
            .Must(ids => ids.Count is > 0 and <= 100);
        RuleForEach(command => command.AssetIds).NotEmpty()
            .When(command => command.AssetIds is not null);
        RuleFor(command => command.AssetIds).Must(ids => ids is not null && ids.Distinct().Count() == ids.Count)
            .WithMessage("AssetIds must not contain duplicates.");
        RuleFor(command => command.Reason).NotEmpty().MaximumLength(200);
    }
}
