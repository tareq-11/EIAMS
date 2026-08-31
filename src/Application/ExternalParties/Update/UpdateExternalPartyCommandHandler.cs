using Application.Abstractions.Authentication;
using Application.Abstractions.Authorization;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Common;
using Domain.ExternalParties;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.ExternalParties.Update;

internal sealed class UpdateExternalPartyCommandHandler(
    IApplicationDbContext context,
    IUserContext userContext,
    IScopeAuthorizationService authorizationService,
    IDatabaseExceptionClassifier databaseExceptionClassifier)
    : ICommandHandler<UpdateExternalPartyCommand>
{
    public async Task<Result> Handle(UpdateExternalPartyCommand command, CancellationToken cancellationToken)
    {
        bool authorized = await authorizationService.HasPermissionInScopeAsync(
            userContext.UserId, PermissionCodes.Organizations.Manage,
            ScopeType.Enterprise, null, cancellationToken);

        if (!authorized)
        {
            return Result.Failure(ExternalPartyErrors.Forbidden);
        }

        ExternalParty? party = await context.ExternalParties.SingleOrDefaultAsync(
            item => item.Id == command.ExternalPartyId, cancellationToken);

        if (party is null)
        {
            return Result.Failure(ExternalPartyErrors.NotFound(command.ExternalPartyId));
        }

        if (party.RowVersion != command.ExpectedRowVersion)
        {
            return Result.Failure(ExternalPartyErrors.RowVersionMismatch(
                party.Id, command.ExpectedRowVersion, party.RowVersion));
        }

        string normalizedName = command.NameAr.Trim().ToUpperInvariant();
        string? normalizedCode = string.IsNullOrWhiteSpace(command.Code)
            ? null
            : command.Code.Trim().ToUpperInvariant();

        if (await context.ExternalParties.AnyAsync(
                item => item.Id != party.Id && item.NormalizedNameAr == normalizedName,
                cancellationToken))
        {
            return Result.Failure(ExternalPartyErrors.NameNotUnique);
        }

        if (normalizedCode is not null && await context.ExternalParties.AnyAsync(
                item => item.Id != party.Id && item.NormalizedCode == normalizedCode,
                cancellationToken))
        {
            return Result.Failure(ExternalPartyErrors.CodeNotUnique);
        }

        party.UpdateDetails(command.NameAr, command.Code, command.ContactInfo, command.Notes);

        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            int? current = await context.ExternalParties.AsNoTracking()
                .Where(item => item.Id == party.Id)
                .Select(item => (int?)item.RowVersion)
                .SingleOrDefaultAsync(cancellationToken);
            return Result.Failure(ExternalPartyErrors.RowVersionMismatch(
                party.Id, command.ExpectedRowVersion, current));
        }
        catch (DbUpdateException exception) when (databaseExceptionClassifier.IsUniqueConstraintViolation(
            exception, "ix_external_parties_normalized_name_ar"))
        {
            return Result.Failure(ExternalPartyErrors.NameNotUnique);
        }
        catch (DbUpdateException exception) when (databaseExceptionClassifier.IsUniqueConstraintViolation(
            exception, "ix_external_parties_normalized_code"))
        {
            return Result.Failure(ExternalPartyErrors.CodeNotUnique);
        }

        return Result.Success();
    }
}
