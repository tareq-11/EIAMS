using Application.Abstractions.Authentication;
using Application.Abstractions.Authorization;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Abstractions.WarehouseDocuments;
using Domain.Common;
using Domain.WarehouseDocuments;
using SharedKernel;

namespace Application.WarehouseDocuments.Create;

internal sealed class CreateWarehouseDocumentCommandHandler(
    IApplicationDbContext context,
    IUserContext userContext,
    IScopeAuthorizationService scopeAuthorizationService,
    IWarehouseDocumentDraftFactory draftFactory)
    : ICommandHandler<CreateWarehouseDocumentCommand, Guid>
{
    public async Task<Result<Guid>> Handle(CreateWarehouseDocumentCommand command, CancellationToken cancellationToken)
    {
        bool authorized = await scopeAuthorizationService.HasPermissionInScopeAsync(
            userContext.UserId,
            PermissionCodes.WarehouseDocuments.Create,
            ScopeType.Warehouse,
            command.WarehouseId,
            cancellationToken);

        if (!authorized)
        {
            return Result.Failure<Guid>(WarehouseDocumentErrors.Forbidden);
        }

        Result<WarehouseDocument> documentResult = await draftFactory.CreateAsync(
            command.WarehouseId, command.DocumentType, cancellationToken);
        if (documentResult.IsFailure)
        {
            return Result.Failure<Guid>(documentResult.Error);
        }

        WarehouseDocument document = documentResult.Value;

        context.WarehouseDocuments.Add(document);

        await context.SaveChangesAsync(cancellationToken);

        return document.Id;
    }
}
