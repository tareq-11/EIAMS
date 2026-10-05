using SharedKernel;

namespace Domain.Roles;

public sealed class Role : Entity, IAuditableEntity
{
    /// <summary>Maximum stored length of <see cref="NameAr"/>.</summary>
    public const int NameArMaxLength = 200;

    private Role() { }

    /// <summary>
    /// Stable role code (for example <c>WH_MGR</c>). It is an identifier, not a display
    /// label; the Arabic-first UI renders <see cref="NameAr"/> instead.
    /// </summary>
    public string Name { get; private set; }

    /// <summary>Required Arabic display label for the role.</summary>
    public string NameAr { get; private set; }

    public string? Description { get; private set; }

    /// <summary>
    /// Optimistic concurrency stamp for the whole role aggregate: metadata and permission
    /// membership both advance it, so a client can never save metadata against a version that
    /// predates a permission change, or the reverse.
    /// </summary>
    public int RowVersion { get; private set; } = 1;

    public DateTime CreatedAtUtc { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
    public Guid? CreatedBy { get; set; }
    public Guid? UpdatedBy { get; set; }

    public static Role Create(Guid id, string name, string nameAr, string? description)
    {
        var role = new Role
        {
            Id = id,
            Name = name,
            NameAr = nameAr,
            Description = description,
            RowVersion = 1
        };

        role.Raise(new RoleCreatedDomainEvent(role.Id));

        return role;
    }

    public void UpdateDetails(string name, string nameAr, string? description)
    {
        Name = name;
        NameAr = nameAr;
        Description = description;
        RowVersion++;
        Raise(new RoleUpdatedDomainEvent(Id));
    }

    /// <summary>
    /// Advances the aggregate version for a change that does not touch the role metadata, which
    /// today means replacing the role's permission set. Exactly one bump per replacement, so a
    /// caller that read the role and submits the version it saw cannot silently overwrite a
    /// concurrent permission change.
    /// <para>
    /// This raises no domain event on purpose. The grant/revoke diff is already audited by
    /// <c>AuditSaveChangesInterceptor</c>, which captures the added and removed
    /// <see cref="RolePermission"/> rows against this aggregate via <c>AuditEntityRegistry</c>. A
    /// second event would record the same change twice.
    /// </para>
    /// </summary>
    public void BumpAggregateVersion() => RowVersion++;
}