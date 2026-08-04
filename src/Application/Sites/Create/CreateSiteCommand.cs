using Application.Abstractions.Messaging;

namespace Application.Sites.Create;

public sealed record CreateSiteCommand(Guid OrganizationId, string Name, string Code, string? Location) : ICommand<Guid>;
