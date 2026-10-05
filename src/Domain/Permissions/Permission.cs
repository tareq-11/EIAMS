using SharedKernel;

namespace Domain.Permissions;

/// <summary>
/// An atomic permission code. Permissions are compiled-in reference data (seeded via migration),
/// not user-manageable at runtime - new codes are added by developers as features gain endpoints.
/// </summary>
public sealed class Permission : Entity
{
    /// <summary>Maximum stored length of <see cref="NameAr"/>.</summary>
    public const int NameArMaxLength = 200;

    /// <summary>Maximum stored length of <see cref="DescriptionAr"/>.</summary>
    public const int DescriptionArMaxLength = 500;

    private Permission() { }

    /// <summary>The stable dotted permission code, for example <c>document.post</c>.</summary>
    public string Code { get; private set; }

    /// <summary>Required Arabic display label, because the catalogue is rendered in an Arabic UI.</summary>
    public string NameAr { get; private set; }

    /// <summary>Optional Arabic explanation shown beneath the label.</summary>
    public string? DescriptionAr { get; private set; }

    /// <summary>English description retained for audit and operator-facing diagnostics.</summary>
    public string? Description { get; private set; }

    public static Permission Create(Guid id, string code, string nameAr, string? descriptionAr, string? description)
    {
        return new Permission
        {
            Id = id,
            Code = code,
            NameAr = nameAr,
            DescriptionAr = descriptionAr,
            Description = description
        };
    }
}