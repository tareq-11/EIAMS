using Application.Abstractions.Messaging;
using Domain.Common;

namespace Application.Sites.SetStatus;

public sealed record SetSiteStatusCommand(Guid SiteId, Status Status) : ICommand;
