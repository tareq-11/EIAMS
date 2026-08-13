using Application.Abstractions.Authentication;
using Application.Abstractions.Authorization;
using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Application.Abstractions.Numbering;
using Domain.Common;
using Domain.DocumentLines;
using Domain.Warehouses;
using Domain.WarehouseDocuments;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.WarehouseDocuments.CreateReversal;

internal sealed class CreateReversalDocumentCommandHandler(
    IApplicationDbContext context,
    IUserContext userContext,
    IScopeAuthorizationService scopeAuthorizationService,
    IReferenceNumberGenerator referenceNumberGenerator,
    IDatabaseExceptionClassifier databaseExceptionClassifier)
    : ICommandHandler<CreateReversalDocumentCommand, Guid>
{
    public async Task<Result<Guid>> Handle(CreateReversalDocumentCommand command, CancellationToken cancellationToken)
    {
        WarehouseDocument? source = await context.WarehouseDocuments
            .SingleOrDefaultAsync(d => d.Id == command.SourceDocumentId, cancellationToken);

        if (source is null)
        {
            return Result.Failure<Guid>(WarehouseDocumentErrors.NotFound(command.SourceDocumentId));
        }

        bool authorized = await scopeAuthorizationService.HasPermissionInScopeAsync(
            userContext.UserId,
            PermissionCodes.WarehouseDocuments.Create,
            ScopeType.Warehouse,
            source.WarehouseId,
            cancellationToken);

        if (!authorized)
        {
            return Result.Failure<Guid>(WarehouseDocumentErrors.NotFound(command.SourceDocumentId));
        }

        // Only a Posted, non-reversal document may be reversed (M3-PLAN.md §1.6): this single check
        // rules out Draft/Submitted/Rejected/Cancelled/Reversed sources and "reversing a reversal".
        if (source.DocumentStatus != DocumentStatus.Posted || source.ReversalOfDocumentId is not null)
        {
            return Result.Failure<Guid>(WarehouseDocumentErrors.NotEligibleForReversal(source.Id, source.DocumentStatus));
        }

        bool alreadyReversed = await context.WarehouseDocuments
            .AnyAsync(d => d.ReversalOfDocumentId == source.Id, cancellationToken);

        if (alreadyReversed)
        {
            return Result.Failure<Guid>(WarehouseDocumentErrors.AlreadyReversed(source.Id));
        }

        Warehouse warehouse = await context.Warehouses.SingleAsync(w => w.Id == source.WarehouseId, cancellationToken);

        Result<string> referenceResult = await referenceNumberGenerator.AllocateAsync(
            warehouse.SiteId,
            source.DocumentType,
            cancellationToken);

        if (referenceResult.IsFailure)
        {
            return Result.Failure<Guid>(referenceResult.Error);
        }

        var reversal = WarehouseDocument.CreateDraft(
            Guid.NewGuid(),
            source.WarehouseId,
            source.DocumentType,
            referenceResult.Value,
            source.Id);

        context.WarehouseDocuments.Add(reversal);

        List<DocumentLine> sourceLines = await context.DocumentLines
            .Where(l => l.DocumentId == source.Id)
            .OrderBy(l => l.CreatedAtUtc)
            .ThenBy(l => l.Id)
            .ToListAsync(cancellationToken);

        foreach (DocumentLine sourceLine in sourceLines)
        {
            Result<DocumentLine> lineResult = DocumentLine.Create(
                Guid.NewGuid(),
                reversal.Id,
                sourceLine.MaterialId,
                sourceLine.LineType,
                sourceLine.Quantity,
                sourceLine.UnitId,
                sourceLine.BaseQuantity,
                sourceLine.UnitPrice,
                sourceLine.BatchNumber,
                sourceLine.ExpiryDate,
                sourceLine.Id);

            if (lineResult.IsFailure)
            {
                return Result.Failure<Guid>(lineResult.Error);
            }

            context.DocumentLines.Add(lineResult.Value);
        }

        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (databaseExceptionClassifier.IsUniqueConstraintViolation(
            exception,
            "ix_warehouse_documents_reversal_of_document_id"))
        {
            return Result.Failure<Guid>(WarehouseDocumentErrors.AlreadyReversed(source.Id));
        }

        return reversal.Id;
    }
}
