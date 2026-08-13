namespace Domain.Common;

/// <summary>
/// The lifecycle state of a WarehouseDocument (Ch. 8, D-WF-01):
/// Draft -&gt; Submitted -&gt; Posted -&gt; Reversed, with Submitted -&gt; Rejected -&gt; Draft, and
/// Draft/Submitted/Rejected -&gt; Cancelled. Posted and Reversed are terminal except that Posted can
/// become Reversed via a separate reversing document.
/// </summary>
public enum DocumentStatus
{
    Draft,
    Submitted,
    Posted,
    Reversed,
    Cancelled,
    Rejected
}
