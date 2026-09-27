using Application.Abstractions.Pagination;
using Domain.Common;
using Domain.Custodies;
using Domain.DurableCustodyAllocations;
using Domain.TrackedMaterialUnits;
using FluentValidation;

namespace Application.Custodies.GetCustodies;

public sealed class GetCustodiesQueryValidator : AbstractValidator<GetCustodiesQuery>
{
    public GetCustodiesQueryValidator()
    {
        RuleFor(query => query.HolderId).NotEqual(Guid.Empty).When(query => query.HolderId.HasValue);
        RuleFor(query => query.MaterialId).NotEqual(Guid.Empty).When(query => query.MaterialId.HasValue);
        RuleFor(query => query.WarehouseId).NotEqual(Guid.Empty).When(query => query.WarehouseId.HasValue);
        RuleFor(query => query.HolderType).IsInEnum().When(query => query.HolderType.HasValue);
        RuleFor(query => query.SubjectType).IsInEnum().When(query => query.SubjectType.HasValue);
        RuleFor(query => query.Status)
            .Must(BeKnownStatus)
            .When(query => !string.IsNullOrWhiteSpace(query.Status))
            .WithMessage("Status must be a known value.");
        RuleFor(query => query.Page)
            .InclusiveBetween(PaginationDefaults.DefaultPage, PaginationDefaults.MaximumPage);
        RuleFor(query => query.PageSize)
            .InclusiveBetween(1, PaginationDefaults.MaximumPageSize);
    }

    private static bool BeKnownStatus(string? value) =>
        IsDefined<CustodyStatus>(value) ||
        IsDefined<TrackedMaterialUnitStatus>(value) ||
        IsDefined<DurableCustodyAllocationStatus>(value);

    private static bool IsDefined<TEnum>(string? value)
        where TEnum : struct, Enum =>
        Enum.TryParse<TEnum>(value, true, out TEnum parsed) && Enum.IsDefined(parsed);
}
