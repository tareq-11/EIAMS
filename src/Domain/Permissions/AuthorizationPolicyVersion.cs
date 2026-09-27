using SharedKernel;

namespace Domain.Permissions;

/// <summary>
/// Expand-only authorization vocabulary state. Phase 1 seeds legacy-colon as active;
/// later cutover changes the marker atomically.
/// </summary>
public sealed class AuthorizationPolicyVersion : Entity
{
    private AuthorizationPolicyVersion() { }

    public string ActiveVocabulary { get; private set; } = null!;
    public int MappingVersion { get; private set; }
    public long ConcurrencyToken { get; private set; }
    public DateTime ActivatedAtUtc { get; private set; }
    public string ActivatedBy { get; private set; } = null!;
    public bool IsActive { get; private set; }

    public static AuthorizationPolicyVersion Create(
        Guid id,
        string activeVocabulary,
        int mappingVersion,
        long concurrencyToken,
        DateTime activatedAtUtc,
        string activatedBy,
        bool isActive)
    {
        return new AuthorizationPolicyVersion
        {
            Id = id,
            ActiveVocabulary = activeVocabulary,
            MappingVersion = mappingVersion,
            ConcurrencyToken = concurrencyToken,
            ActivatedAtUtc = activatedAtUtc,
            ActivatedBy = activatedBy,
            IsActive = isActive
        };
    }
}
