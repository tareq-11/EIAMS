using FluentValidation;

namespace Application.WarehouseDocuments.CompleteDraft;

internal sealed class CompleteWarehouseDocumentDraftCommandValidator
    : AbstractValidator<CompleteWarehouseDocumentDraftCommand>
{
    public CompleteWarehouseDocumentDraftCommandValidator()
    {
        RuleFor(command => command.WarehouseId).NotEmpty();
        RuleFor(command => command.DocumentType).IsInEnum();
        RuleFor(command => command.IdempotencyKey).Must(key => !key.HasValue || key.Value != Guid.Empty);
        RuleFor(command => command.Lines).NotNull().NotEmpty().Must(lines => lines.Count <= 250)
            .WithMessage("A complete draft may contain at most 250 lines.");
        RuleForEach(command => command.Lines).ChildRules(line =>
        {
            line.RuleFor(item => item.MaterialId).NotEmpty();
            line.RuleFor(item => item.Quantity).GreaterThan(0).PrecisionScale(18, 3, false).When(item => item.Quantity.HasValue);
            line.RuleFor(item => item.Difference).PrecisionScale(18, 3, false).When(item => item.Difference.HasValue);
            line.RuleFor(item => item.UnitPrice).GreaterThanOrEqualTo(0).When(item => item.UnitPrice.HasValue);
            line.RuleFor(item => item.UnitPrice!.Value).PrecisionScale(18, 2, false)
                .When(item => item.UnitPrice.HasValue);
            line.RuleFor(item => item.BatchNumber).MaximumLength(100);
            line.RuleFor(item => item.OpeningType)
                .Must(value => value is null || Enum.IsDefined(value.Value));
            line.RuleFor(item => item.AssetIds).Must(ids => ids is null || ids.All(id => id != Guid.Empty) && ids.Distinct().Count() == ids.Count);
            line.RuleFor(item => item.AdjustmentReason).MaximumLength(200);
        });
        RuleFor(command => command).Custom((command, context) =>
        {
            if (command.Lines is null)
            {
                return;
            }
            for (int i = 0; i < command.Lines.Count; i++)
            {
                CompleteWarehouseDocumentDraftLine line = command.Lines[i];
                string prefix = $"Lines[{i}]";
                if (command.DocumentType is not (Domain.Common.DocumentType.Issue or Domain.Common.DocumentType.Return) &&
                    line.AssetIds is { Count: > 0 })
                {
                    context.AddFailure($"{prefix}.AssetIds", "Asset selections are only supported for Issue and Return documents.");
                }
                if (command.DocumentType == Domain.Common.DocumentType.Adjustment)
                {
                    if (line.Quantity.HasValue || !line.Difference.HasValue || line.Difference.Value == 0 ||
                        line.UnitPrice.HasValue || line.BatchNumber is not null || line.ExpiryDate.HasValue ||
                        line.OpeningType.HasValue || line.AssetIds is { Count: > 0 })
                    {
                        context.AddFailure(prefix, "Adjustment lines require a non-zero difference and cannot specify quantity, asset, or receiving fields.");
                    }
                }
                else if (!line.Quantity.HasValue || line.Difference.HasValue || line.AdjustmentReason is not null)
                {
                    context.AddFailure(prefix, "Non-adjustment lines require quantity and cannot specify difference or adjustment reason.");
                }
            }
        });
        When(command => command.ReceivingInfo is not null, () =>
        {
            RuleFor(command => command.ReceivingInfo!.SupplierPartyId).NotEmpty();
            RuleFor(command => command.ReceivingInfo!.SupplierInvoiceRef).MaximumLength(100);
        });
        When(command => command.IssueTo is not null, () =>
        {
            RuleFor(command => command.IssueTo!.RecipientType).IsInEnum();
            RuleFor(command => command.IssueTo!.RecipientId).NotEmpty();
            RuleFor(command => command.IssueTo!.IssueReason).NotEmpty().MaximumLength(500);
        });
        When(command => command.TransferInfo is not null, () =>
        {
            RuleFor(command => command.TransferInfo!.DestinationWarehouseId).NotEmpty();
            RuleFor(command => command.TransferInfo!.TransferReason).NotEmpty().MaximumLength(500);
        });
        When(command => command.ReturnInfo is not null, () =>
        {
            RuleFor(command => command.ReturnInfo!.OriginalIssueDocumentId).NotEmpty();
            RuleFor(command => command.ReturnInfo!.ReturnReason).NotEmpty().MaximumLength(500);
        });
        When(command => command.AdjustmentInfo is not null, () =>
        {
            RuleFor(command => command.AdjustmentInfo!.AdjustmentKind).IsInEnum();
            RuleFor(command => command.AdjustmentInfo!.Reason).NotEmpty().MaximumLength(500);
        });
    }
}
