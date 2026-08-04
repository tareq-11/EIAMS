using Application.Abstractions.Messaging;

namespace Application.Organizations.Update;

public sealed record UpdateOrganizationCommand(Guid OrganizationId, string Name) : ICommand;
