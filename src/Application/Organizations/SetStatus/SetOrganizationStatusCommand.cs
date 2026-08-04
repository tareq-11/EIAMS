using Application.Abstractions.Messaging;
using Domain.Common;

namespace Application.Organizations.SetStatus;

public sealed record SetOrganizationStatusCommand(Guid OrganizationId, Status Status) : ICommand;
