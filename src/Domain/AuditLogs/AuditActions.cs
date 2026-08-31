namespace Domain.AuditLogs;

/// <summary>
/// The closed vocabulary of audited actions. <c>Approve</c> is deliberately reserved and must not
/// appear here until an approval workflow exists that would emit it.
/// </summary>
public static class AuditActions
{
    public const string Create = "Create";
    public const string Update = "Update";
    public const string Delete = "Delete";
    public const string Submit = "Submit";
    public const string Post = "Post";
    public const string Reject = "Reject";
    public const string Cancel = "Cancel";
    public const string Reverse = "Reverse";
    public const string ReturnToDraft = "ReturnToDraft";
    public const string Move = "Move";
    public const string Activate = "Activate";
    public const string Deactivate = "Deactivate";
    public const string Add = "Add";
    public const string Remove = "Remove";
    public const string RecordActual = "RecordActual";
    public const string Start = "Start";
    public const string Complete = "Complete";
    public const string Close = "Close";
    public const string Assign = "Assign";
    public const string Grant = "Grant";
    public const string Revoke = "Revoke";
    public const string Authenticate = "Authenticate";
    public const string TokenRefresh = "TokenRefresh";
    public const string Logout = "Logout";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal)
    {
        Create,
        Update,
        Delete,
        Submit,
        Post,
        Reject,
        Cancel,
        Reverse,
        ReturnToDraft,
        Move,
        Activate,
        Deactivate,
        Add,
        Remove,
        RecordActual,
        Start,
        Complete,
        Close,
        Assign,
        Grant,
        Revoke,
        Authenticate,
        TokenRefresh,
        Logout
    };
}
