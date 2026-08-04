using Application.Abstractions.Messaging;

namespace Application.Sites.Update;

public sealed record UpdateSiteCommand(Guid SiteId, string Name, string? Location) : ICommand;
