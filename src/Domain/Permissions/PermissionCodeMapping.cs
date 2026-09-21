using SharedKernel;

namespace Domain.Permissions;

/// <summary>
/// Immutable, auditable evidence for a legacy-to-target permission transformation.
/// It is migration history, not an authorization grant.
/// </summary>
public sealed class PermissionCodeMapping : Entity
{
    private PermissionCodeMapping() { }

    public string OldCode { get; private set; } = null!;
    public string NewCode { get; private set; } = null!;
    public int MappingVersion { get; private set; }
    public string Approver { get; private set; } = null!;
    public DateTime ApprovedAtUtc { get; private set; }
    public string Rationale { get; private set; } = null!;
    public DateTime CreatedAtUtc { get; private set; }

    public static PermissionCodeMapping Create(
        Guid id,
        string oldCode,
        string newCode,
        int mappingVersion,
        string approver,
        DateTime approvedAtUtc,
        string rationale,
        DateTime createdAtUtc)
    {
        return new PermissionCodeMapping
        {
            Id = id,
            OldCode = oldCode,
            NewCode = newCode,
            MappingVersion = mappingVersion,
            Approver = approver,
            ApprovedAtUtc = approvedAtUtc,
            Rationale = rationale,
            CreatedAtUtc = createdAtUtc
        };
    }
}
