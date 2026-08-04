using Application.Abstractions.Authentication;
using Application.Abstractions.Authorization;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Common;
using Domain.MaterialUnitConversions;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.MaterialUnitConversions.Add;

internal sealed class AddMaterialUnitConversionCommandHandler(
    IApplicationDbContext context,
    IUserContext userContext,
    IScopeAuthorizationService scopeAuthorizationService)
    : ICommandHandler<AddMaterialUnitConversionCommand, Guid>
{
    public async Task<Result<Guid>> Handle(AddMaterialUnitConversionCommand command, CancellationToken cancellationToken)
    {
        bool authorized = await scopeAuthorizationService.HasPermissionInScopeAsync(
            userContext.UserId,
            PermissionCodes.Materials.Manage,
            ScopeType.Enterprise,
            scopeId: null,
            cancellationToken);

        if (!authorized)
        {
            return Result.Failure<Guid>(MaterialUnitConversionErrors.Forbidden);
        }

        Guid? baseUnitId = await (
                from material in context.Materials
                where material.Id == command.MaterialId
                join family in context.MaterialFamilies on material.FamilyId equals family.Id
                select (Guid?)family.BaseUnitId)
            .SingleOrDefaultAsync(cancellationToken);

        if (baseUnitId is null)
        {
            return Result.Failure<Guid>(MaterialUnitConversionErrors.MaterialNotFound(command.MaterialId));
        }

        string? fromUnitType = await context.UnitsOfMeasure
            .Where(unit => unit.Id == command.FromUnitId)
            .Select(unit => unit.UnitType)
            .SingleOrDefaultAsync(cancellationToken);

        if (fromUnitType is null)
        {
            return Result.Failure<Guid>(MaterialUnitConversionErrors.UnitNotFound(command.FromUnitId));
        }

        string? toUnitType = await context.UnitsOfMeasure
            .Where(unit => unit.Id == command.ToBaseUnitId)
            .Select(unit => unit.UnitType)
            .SingleOrDefaultAsync(cancellationToken);

        if (toUnitType is null)
        {
            return Result.Failure<Guid>(MaterialUnitConversionErrors.UnitNotFound(command.ToBaseUnitId));
        }

        if (command.ToBaseUnitId != baseUnitId.Value)
        {
            return Result.Failure<Guid>(MaterialUnitConversionErrors.BaseUnitMismatch);
        }

        if (command.FromUnitId == command.ToBaseUnitId)
        {
            return Result.Failure<Guid>(MaterialUnitConversionErrors.SameUnit);
        }

        if (!string.Equals(fromUnitType, toUnitType, StringComparison.OrdinalIgnoreCase))
        {
            return Result.Failure<Guid>(MaterialUnitConversionErrors.UnitTypeMismatch);
        }

        if (await context.MaterialUnitConversions.AnyAsync(
                conversion => conversion.MaterialId == command.MaterialId &&
                              conversion.FromUnitId == command.FromUnitId,
                cancellationToken))
        {
            return Result.Failure<Guid>(MaterialUnitConversionErrors.AlreadyExists);
        }

        var conversion = MaterialUnitConversion.Create(
            Guid.NewGuid(),
            command.MaterialId,
            command.FromUnitId,
            command.ToBaseUnitId,
            command.Factor);

        context.MaterialUnitConversions.Add(conversion);

        await context.SaveChangesAsync(cancellationToken);

        return conversion.Id;
    }
}
