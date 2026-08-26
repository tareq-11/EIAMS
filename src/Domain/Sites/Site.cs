using Domain.Common;
using SharedKernel;

namespace Domain.Sites;

public sealed class Site : Entity, IAuditableEntity
{
    public const int MaxGovernorateCodeLength = 20;

    private Site() { }

    public Guid OrganizationId { get; private set; }
    public string Name { get; private set; }
    public string Code { get; private set; }
    public string? Location { get; private set; }
    public string? GovernorateCode { get; private set; }
    public Status Status { get; private set; }

    public DateTime CreatedAtUtc { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
    public Guid? CreatedBy { get; set; }
    public Guid? UpdatedBy { get; set; }

    public static Site Create(Guid id, Guid organizationId, string name, string code, string? location)
    {
        var site = new Site
        {
            Id = id,
            OrganizationId = organizationId,
            Name = name,
            Code = code,
            Location = location,
            Status = Status.Active
        };

        site.Raise(new SiteCreatedDomainEvent(site.Id, site.OrganizationId));

        return site;
    }

    public static Result<Site> Create(
        Guid id,
        Guid organizationId,
        string name,
        string code,
        string? location,
        string? governorateCode)
    {
        Result<string?> governorateResult = NormalizeGovernorateCode(governorateCode);

        if (governorateResult.IsFailure)
        {
            return Result.Failure<Site>(governorateResult.Error);
        }

        Site site = Create(id, organizationId, name, code, location);
        site.GovernorateCode = governorateResult.Value;

        return site;
    }

    public void UpdateDetails(string name, string? location)
    {
        Name = name;
        Location = location;
        Raise(new SiteUpdatedDomainEvent(Id));
    }

    public Result UpdateDetails(string name, string? location, string? governorateCode)
    {
        Result<string?> governorateResult = NormalizeGovernorateCode(governorateCode);

        if (governorateResult.IsFailure)
        {
            return governorateResult;
        }

        string? normalizedCode = governorateResult.Value;

        if (Name == name && Location == location && GovernorateCode == normalizedCode)
        {
            return Result.Success();
        }

        Name = name;
        Location = location;
        GovernorateCode = normalizedCode;
        Raise(new SiteUpdatedDomainEvent(Id));

        return Result.Success();
    }

    public void SetStatus(Status status)
    {
        if (Status == status)
        {
            return;
        }

        Status = status;

        Raise(new SiteStatusChangedDomainEvent(Id, status));
    }

    private static Result<string?> NormalizeGovernorateCode(string? governorateCode)
    {
        if (string.IsNullOrWhiteSpace(governorateCode))
        {
            return Result.Success<string?>(null);
        }

        string normalized = governorateCode.Trim().ToUpperInvariant();
        bool valid = normalized.Length <= MaxGovernorateCodeLength &&
            normalized.All(character =>
                character is >= 'A' and <= 'Z' or >= '0' and <= '9' or '_' or '-');

        return valid
            ? Result.Success<string?>(normalized)
            : Result.Failure<string?>(SiteErrors.GovernorateCodeInvalid);
    }
}
