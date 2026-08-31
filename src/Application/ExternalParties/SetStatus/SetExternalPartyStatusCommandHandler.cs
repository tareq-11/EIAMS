using Application.Abstractions.Authentication;
using Application.Abstractions.Authorization;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Common;
using Domain.ExternalParties;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.ExternalParties.SetStatus;

internal sealed class SetExternalPartyStatusCommandHandler(
    IApplicationDbContext context,
    IUserContext userContext,
    IScopeAuthorizationService authorizationService)
    : ICommandHandler<SetExternalPartyStatusCommand>
{
    public async Task<Result> Handle(SetExternalPartyStatusCommand command, CancellationToken cancellationToken)
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

        party.SetStatus(command.Status);

        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result.Failure(ExternalPartyErrors.RowVersionMismatch(
                party.Id, command.ExpectedRowVersion, null));
        }

        return Result.Success();
    }
}
