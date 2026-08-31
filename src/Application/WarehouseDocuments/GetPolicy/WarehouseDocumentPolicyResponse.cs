namespace Application.WarehouseDocuments.GetPolicy;

public sealed record WarehouseDocumentPolicyResponse(
    Guid DocumentId,
    string Status,
    int RowVersion,
    DateTime EvaluatedAtUtc,
    bool SignedOriginalSatisfied,
    IReadOnlyList<DocumentActionAvailabilityResponse> Actions,
    IReadOnlyList<DocumentPolicyBlockerResponse> Blockers,
    IReadOnlyList<DocumentPolicyAdvisoryResponse> Warnings);

public sealed record DocumentActionAvailabilityResponse(
    string Action,
    bool Allowed,
    bool ConfirmationRequired,
    bool ReasonRequired,
    string Presentation,
    string? ReasonCode,
    string? Reason);

public sealed record DocumentPolicyBlockerResponse(string Action, string Code, string Message);

public sealed record DocumentPolicyAdvisoryResponse(
    string Code,
    string Message,
    Guid? CountId,
    Guid? WarehouseId);
