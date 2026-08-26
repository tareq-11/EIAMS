using Domain.AuditLogs;

namespace Infrastructure.AuditLogs;

/// <summary>
/// Resolves a workflow action from a truly-changed status column. Pure and database-free: the
/// interceptor feeds it serialized original/current values and it either maps the transition to a
/// canonical audit action or returns null so the caller falls back to Create/Update/Delete.
/// </summary>
internal static class AuditTransitionResolver
{
    public static string? Resolve(
        string canonicalEntityType,
        string storeFieldName,
        string? originalValue,
        string? currentValue)
    {
        if (originalValue is null || currentValue is null)
        {
            return null;
        }

        return (canonicalEntityType, storeFieldName) switch
        {
            ("WarehouseDocument", "document_status") => ResolveDocumentStatus(originalValue, currentValue),
            ("InventoryCount", "status") => ResolveCountStatus(originalValue, currentValue),
            _ => null
        };
    }

    private static string? ResolveDocumentStatus(string fromStatus, string toStatus) =>
        (fromStatus, toStatus) switch
        {
            ("Draft", "Submitted") => AuditActions.Submit,
            ("Submitted", "Posted") => AuditActions.Post,
            ("Posted", "Reversed") => AuditActions.Reverse,
            ("Submitted", "Rejected") => AuditActions.Reject,
            ("Draft" or "Submitted" or "Rejected", "Cancelled") => AuditActions.Cancel,
            ("Rejected", "Draft") => AuditActions.ReturnToDraft,
            _ => null
        };

    private static string? ResolveCountStatus(string fromStatus, string toStatus) =>
        (fromStatus, toStatus) switch
        {
            ("Planned", "InProgress") => AuditActions.Start,
            ("InProgress", "Completed") => AuditActions.Complete,
            ("Completed", "Closed") => AuditActions.Close,
            _ => null
        };
}
