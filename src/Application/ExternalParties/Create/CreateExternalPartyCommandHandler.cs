using Application.Abstractions.Authentication;
using Application.Abstractions.Authorization;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Common;
using Domain.ExternalParties;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.ExternalParties.Create;

internal sealed class CreateExternalPartyCommandHandler(
    IApplicationDbContext context,
    IUserContext userContext,
    IScopeAuthorizationService authorizationService,
    IDatabaseExceptionClassifier databaseExceptionClassifier)
    : ICommandHandler<CreateExternalPartyCommand, Guid>
{
    public async Task<Result<Guid>> Handle(CreateExternalPartyCommand command, CancellationToken cancellationToken)
    {
        bool authorized = await authorizationService.HasPermissionInScopeAsync(
            userContext.UserId,
            PermissionCodes.Organizations.Manage,
            ScopeType.Enterprise,
            null,
            cancellationToken);

        if (!authorized)
        {
            return Result.Failure<Guid>(ExternalPartyErrors.Forbidden);
        }

        string normalizedName = command.NameAr.Trim().ToUpperInvariant();
        string? normalizedCode = string.IsNullOrWhiteSpace(command.Code)
            ? null
            : command.Code.Trim().ToUpperInvariant();

        if (await context.ExternalParties.AnyAsync(
                party => party.NormalizedNameAr == normalizedName,
                cancellationToken))
        {
            return Result.Failure<Guid>(ExternalPartyErrors.NameNotUnique);
        }

        if (normalizedCode is not null && await context.ExternalParties.AnyAsync(
                party => party.NormalizedCode == normalizedCode,
                cancellationToken))
        {
            return Result.Failure<Guid>(ExternalPartyErrors.CodeNotUnique);
        }

        var party = ExternalParty.Create(
            Guid.NewGuid(), command.NameAr, command.Code, command.ContactInfo, command.Notes);

        context.ExternalParties.Add(party);

        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (databaseExceptionClassifier.IsUniqueConstraintViolation(
            exception, "ix_external_parties_normalized_name_ar"))
        {
            return Result.Failure<Guid>(ExternalPartyErrors.NameNotUnique);
        }
        catch (DbUpdateException exception) when (databaseExceptionClassifier.IsUniqueConstraintViolation(
            exception, "ix_external_parties_normalized_code"))
        {
            return Result.Failure<Guid>(ExternalPartyErrors.CodeNotUnique);
        }

        return party.Id;
    }
}
