using Application.Abstractions.Authentication;
using Application.Abstractions.Assets;
using Application.Abstractions.Authorization;
using Application.Abstractions.Data;
using Application.Abstractions.InventoryCounts;
using Application.Abstractions.Messaging;
using Application.Abstractions.Posting;
using Application.Abstractions.Warehouses;
using Application.DocumentLines;
using Domain.Common;
using Domain.DocumentAttachments;
using Domain.DocumentLines;
using Domain.WarehouseDocuments;
using Domain.Warehouses;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SharedKernel;

namespace Application.WarehouseDocuments.GetPolicy;

internal sealed class GetWarehouseDocumentPolicyQueryHandler(
    IApplicationDbContext context,
    IUserContext userContext,
    IScopeAuthorizationService scopeAuthorizationService,
    IDocumentPostingScopeResolver postingScopeResolver,
    IInventoryFreezePolicyService freezePolicyService,
    ICapabilityCheckService capabilityCheckService,
    IEnumerable<IDocumentSubmissionValidator> submissionValidators,
    IOptions<AssetCreationOptions> assetCreationOptions,
    IDateTimeProvider dateTimeProvider)
    : IQueryHandler<GetWarehouseDocumentPolicyQuery, WarehouseDocumentPolicyResponse>
{
    private const string Edit = "Edit";
    private const string Submit = "Submit";
    private const string Post = "Post";
    private const string Reject = "Reject";
    private const string Revise = "Revise";
    private const string Cancel = "Cancel";
    private const string Reverse = "Reverse";
    private const string UploadAttachment = "UploadAttachment";
    private const string DeleteAttachment = "DeleteAttachment";

    public async Task<Result<WarehouseDocumentPolicyResponse>> Handle(
        GetWarehouseDocumentPolicyQuery query,
        CancellationToken cancellationToken)
    {
        WarehouseDocument? document = await context.WarehouseDocuments
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == query.DocumentId, cancellationToken);

        if (document is null || !await HasPermissionAsync(document, PermissionCodes.WarehouseDocuments.View, cancellationToken))
        {
            return Result.Failure<WarehouseDocumentPolicyResponse>(WarehouseDocumentErrors.NotFound(query.DocumentId));
        }

        var blockers = new List<DocumentPolicyBlockerResponse>();
        var warnings = new List<DocumentPolicyAdvisoryResponse>();
        var actions = new List<DocumentActionAvailabilityResponse>();

        bool canEdit = await HasPermissionAsync(document, PermissionCodes.WarehouseDocuments.Edit, cancellationToken);
        bool canSubmit = await HasPermissionAsync(document, PermissionCodes.WarehouseDocuments.Submit, cancellationToken);
        bool canCancel = await HasPermissionAsync(document, PermissionCodes.WarehouseDocuments.Cancel, cancellationToken);
        bool canReview = await HasPermissionAsync(document, PermissionCodes.WarehouseDocuments.Review, cancellationToken);
        bool canCreate = await HasPermissionAsync(document, PermissionCodes.WarehouseDocuments.Create, cancellationToken);
        bool canReverse = await HasPermissionAsync(document, PermissionCodes.WarehouseDocuments.Reverse, cancellationToken);

        AddSimpleAction(actions, Edit, document.DocumentStatus == DocumentStatus.Draft, canEdit);
        AddSimpleAction(actions, UploadAttachment, document.DocumentStatus == DocumentStatus.Draft, canEdit);
        AddSimpleAction(actions, DeleteAttachment, document.DocumentStatus == DocumentStatus.Draft, canEdit, confirmation: true);
        AddSimpleAction(actions, Reject, document.DocumentStatus == DocumentStatus.Submitted, canReview, confirmation: true, reasonRequired: true);
        AddSimpleAction(actions, Revise, document.DocumentStatus == DocumentStatus.Rejected, canEdit, reasonRequired: true);
        AddSimpleAction(
            actions,
            Cancel,
            document.DocumentStatus is DocumentStatus.Draft or DocumentStatus.Submitted or DocumentStatus.Rejected,
            canCancel,
            confirmation: true,
            reasonRequired: true);

        List<DocumentLine> lines = await context.DocumentLines
            .AsNoTracking()
            .Where(line => line.DocumentId == document.Id)
            .OrderBy(line => line.CreatedAtUtc)
            .ThenBy(line => line.Id)
            .ToListAsync(cancellationToken);

        bool submitReady = document.DocumentStatus == DocumentStatus.Draft && canSubmit;
        if (submitReady)
        {
            if (string.IsNullOrWhiteSpace(document.PaperDocumentNumber) || document.PaperDocumentYear is null)
            {
                AddBlocker(blockers, Submit, WarehouseDocumentErrors.PaperReferenceRequired(document.Id));
            }

            Result lineValidation = await DocumentLineSubmissionValidator.ValidateAsync(
                context,
                document,
                lines,
                assetCreationOptions.Value,
                submissionValidators,
                cancellationToken);
            if (lineValidation.IsFailure)
            {
                AddBlocker(blockers, Submit, lineValidation.Error);
            }
        }

        AddEvaluatedAction(actions, blockers, Submit, submitReady);

        bool signedOriginalSatisfied = document.SignedCopyAttachmentId is not null &&
            await context.DocumentAttachments.AsNoTracking().AnyAsync(
                item => item.Id == document.SignedCopyAttachmentId &&
                        item.DocumentId == document.Id &&
                        item.AttachmentType == AttachmentType.SignedOriginal &&
                        item.IsActive,
                cancellationToken);

        bool postCandidate = document.DocumentStatus == DocumentStatus.Submitted &&
                             canReview &&
                             (document.ReversalOfDocumentId is null || canReverse);
        if (postCandidate)
        {
            await EvaluatePostingReadinessAsync(document, lines, signedOriginalSatisfied, blockers, warnings, cancellationToken);
        }

        AddEvaluatedAction(actions, blockers, Post, postCandidate, confirmation: true);

        bool reversalAlreadyExists = await context.WarehouseDocuments.AsNoTracking()
            .AnyAsync(item => item.ReversalOfDocumentId == document.Id, cancellationToken);
        bool reverseCandidate = document.DocumentStatus == DocumentStatus.Posted &&
                                document.ReversalOfDocumentId is null &&
                                !reversalAlreadyExists &&
                                canCreate && canReverse;
        AddSimpleAction(actions, Reverse, reverseCandidate, permissionSatisfied: canCreate && canReverse, confirmation: true, reasonRequired: true);

        return new WarehouseDocumentPolicyResponse(
            document.Id,
            document.DocumentStatus.ToString(),
            document.RowVersion,
            dateTimeProvider.UtcNow,
            signedOriginalSatisfied,
            actions,
            blockers,
            warnings);
    }

    private async Task EvaluatePostingReadinessAsync(
        WarehouseDocument document,
        List<DocumentLine> lines,
        bool signedOriginalSatisfied,
        List<DocumentPolicyBlockerResponse> blockers,
        List<DocumentPolicyAdvisoryResponse> warnings,
        CancellationToken cancellationToken)
    {
        if (!signedOriginalSatisfied)
        {
            AddBlocker(blockers, Post, WarehouseDocumentErrors.SignedCopyRequired(document.Id));
        }

        Warehouse? warehouse = await context.Warehouses.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == document.WarehouseId, cancellationToken);
        if (warehouse is null)
        {
            AddBlocker(blockers, Post, WarehouseErrors.NotFound(document.WarehouseId));
        }
        else if (warehouse.Status != Status.Active)
        {
            AddBlocker(blockers, Post, WarehouseErrors.Inactive(warehouse.Id));
        }
        else if (!warehouse.CanHoldStock)
        {
            AddBlocker(blockers, Post, WarehouseErrors.CannotHoldStock(warehouse.Id));
        }

        Result<IReadOnlyCollection<Guid>> scopeResult = await postingScopeResolver.ResolveAsync(document, cancellationToken);
        if (scopeResult.IsFailure)
        {
            AddBlocker(blockers, Post, scopeResult.Error);
            return;
        }

        foreach (Guid warehouseId in scopeResult.Value)
        {
            bool authorized = await scopeAuthorizationService.HasPermissionInScopeAsync(
                userContext.UserId,
                PermissionCodes.WarehouseDocuments.Review,
                ScopeType.Warehouse,
                warehouseId,
                cancellationToken);
            if (!authorized)
            {
                blockers.Add(new DocumentPolicyBlockerResponse(Post, "WarehouseDocuments.PostScopeDenied", "Review permission is required in every affected warehouse."));
                break;
            }

            if (document.ReversalOfDocumentId is not null)
            {
                bool reverseAuthorized = await scopeAuthorizationService.HasPermissionInScopeAsync(
                    userContext.UserId,
                    PermissionCodes.WarehouseDocuments.Reverse,
                    ScopeType.Warehouse,
                    warehouseId,
                    cancellationToken);
                if (!reverseAuthorized)
                {
                    blockers.Add(new DocumentPolicyBlockerResponse(
                        Post,
                        "WarehouseDocuments.ReverseScopeDenied",
                        "Reverse permission is required in every affected warehouse."));
                    break;
                }
            }
        }

        InventoryFreezeEvaluation freeze = await freezePolicyService.EvaluateAsync(scopeResult.Value, cancellationToken);
        if (freeze.BlockingError is not null)
        {
            AddBlocker(blockers, Post, freeze.BlockingError);
        }
        warnings.AddRange(freeze.Warnings.Select(item => new DocumentPolicyAdvisoryResponse(
            item.Code, item.Message, item.CountId, item.WarehouseId)));

        Result validation = await DocumentLineSubmissionValidator.ValidateAsync(
            context, document, lines, assetCreationOptions.Value, submissionValidators, cancellationToken);
        if (validation.IsFailure)
        {
            AddBlocker(blockers, Post, validation.Error);
        }

        if (lines.Count == 0)
        {
            return;
        }

        Guid[] domainIds = await (
            from line in context.DocumentLines.AsNoTracking()
            join material in context.Materials.AsNoTracking() on line.MaterialId equals material.Id
            join family in context.MaterialFamilies.AsNoTracking() on material.FamilyId equals family.Id
            join category in context.MaterialCategories.AsNoTracking() on family.CategoryId equals category.Id
            where line.DocumentId == document.Id
            select category.MaterialDomainId)
            .Distinct()
            .ToArrayAsync(cancellationToken);

        OperationType? operation = document.DocumentType switch
        {
            DocumentType.Receiving => OperationType.Receiving,
            DocumentType.Issue => OperationType.Issue,
            DocumentType.Transfer => OperationType.Transfer,
            DocumentType.Return => OperationType.Return,
            DocumentType.Adjustment => OperationType.Adjustment,
            DocumentType.Opening => null,
            _ => null
        };

        if (operation.HasValue && document.ReversalOfDocumentId is null)
        {
            foreach (Guid warehouseId in scopeResult.Value)
            {
                Result capability = await capabilityCheckService.EnsureAllowedBatchAsync(
                    warehouseId, domainIds, operation.Value, cancellationToken);
                if (capability.IsFailure)
                {
                    AddBlocker(blockers, Post, capability.Error);
                }
            }
        }

        if (document.ReversalOfDocumentId is null &&
            document.DocumentType is DocumentType.Issue or DocumentType.Transfer)
        {
            var required = lines.GroupBy(line => line.MaterialId)
                .ToDictionary(group => group.Key, group => group.Sum(line => line.BaseQuantity));
            Dictionary<Guid, decimal> balances = await context.InventoryBalances.AsNoTracking()
                .Where(item => item.WarehouseId == document.WarehouseId && required.Keys.Contains(item.MaterialId))
                .ToDictionaryAsync(item => item.MaterialId, item => item.Quantity, cancellationToken);

            foreach ((Guid materialId, decimal quantity) in required)
            {
                if (!balances.TryGetValue(materialId, out decimal available) || available < quantity)
                {
                    blockers.Add(new DocumentPolicyBlockerResponse(
                        Post,
                        "InventoryBalances.InsufficientQuantity",
                        $"Material '{materialId}' requires {quantity} but only {available} is currently available."));
                }
            }
        }
    }

    private Task<bool> HasPermissionAsync(WarehouseDocument document, string permission, CancellationToken cancellationToken) =>
        scopeAuthorizationService.HasPermissionInScopeAsync(
            userContext.UserId, permission, ScopeType.Warehouse, document.WarehouseId, cancellationToken);

    private static void AddBlocker(List<DocumentPolicyBlockerResponse> blockers, string action, Error error) =>
        blockers.Add(new DocumentPolicyBlockerResponse(action, error.Code, error.Description));

    private static void AddEvaluatedAction(
        List<DocumentActionAvailabilityResponse> actions,
        IReadOnlyCollection<DocumentPolicyBlockerResponse> blockers,
        string action,
        bool candidate,
        bool confirmation = false)
    {
        DocumentPolicyBlockerResponse? blocker = blockers.FirstOrDefault(item => item.Action == action);
        bool allowed = candidate && blocker is null;
        string? reasonCode = blocker?.Code;
        if (reasonCode is null && !candidate)
        {
            reasonCode = "STATE_OR_PERMISSION_NOT_ALLOWED";
        }

        actions.Add(new DocumentActionAvailabilityResponse(
            action, allowed, confirmation, false, allowed ? "Enabled" : "Disabled",
            reasonCode,
            blocker?.Message));
    }

    private static void AddSimpleAction(
        List<DocumentActionAvailabilityResponse> actions,
        string action,
        bool stateSatisfied,
        bool permissionSatisfied,
        bool confirmation = false,
        bool reasonRequired = false)
    {
        bool allowed = stateSatisfied && permissionSatisfied;
        string presentation = allowed ? "Enabled" : "Disabled";
        string? reasonCode = null;
        string? reason = null;
        if (!permissionSatisfied)
        {
            presentation = "Hidden";
            reasonCode = "PERMISSION_DENIED";
            reason = "The current user does not have this permission.";
        }
        else if (!stateSatisfied)
        {
            reasonCode = "STATE_NOT_ALLOWED";
            reason = "The document state does not allow this action.";
        }

        actions.Add(new DocumentActionAvailabilityResponse(
            action,
            allowed,
            confirmation,
            reasonRequired,
            presentation,
            reasonCode,
            reason));
    }
}
