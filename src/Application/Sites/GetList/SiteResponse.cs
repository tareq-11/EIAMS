namespace Application.Sites.GetList;

public sealed class SiteResponse
{
    public Guid Id { get; init; }

    public Guid OrganizationId { get; init; }

    public string Name { get; init; }

    public string Code { get; init; }

    public string? Location { get; init; }

    public string Status { get; init; }
}
