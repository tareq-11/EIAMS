using FluentValidation;
using Domain.Common;

namespace Application.InventoryCounts.Plan;

internal sealed class PlanInventoryCountCommandValidator : AbstractValidator<PlanInventoryCountCommand>
{
    public PlanInventoryCountCommandValidator()
    {
        RuleFor(command => command.WarehouseId).NotEmpty();
        RuleFor(command => command.CountType).IsInEnum();
        RuleFor(command => command.ScopeType).IsInEnum();
        RuleFor(command => command.FreezePolicy).IsInEnum();
        RuleForEach(command => command.MaterialIds).NotEmpty();
        RuleFor(command => command.MaterialIds)
            .NotEmpty()
            .When(command => command.ScopeType == InventoryCountScopeType.SelectedMaterials)
            .WithMessage("At least one material is required for a selected-materials count.");
        RuleFor(command => command.MaterialIds)
            .Must(ids => ids.Count <= 100)
            .WithMessage("A selected-materials count cannot contain more than 100 materials.");
        RuleFor(command => command.MaterialIds)
            .Must(ids => ids.Distinct().Count() == ids.Count)
            .WithMessage("Selected material ids must be unique.");
        RuleFor(command => command.MaterialIds)
            .Empty()
            .When(command => command.ScopeType != InventoryCountScopeType.SelectedMaterials);
        RuleFor(command => command.MaterialDomainId)
            .NotEmpty()
            .When(command => command.ScopeType == InventoryCountScopeType.MaterialDomain);
        RuleFor(command => command.MaterialDomainId)
            .Null()
            .When(command => command.ScopeType != InventoryCountScopeType.MaterialDomain);
    }
}
