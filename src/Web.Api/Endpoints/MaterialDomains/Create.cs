using Application.Abstractions.Authorization;
using Application.Abstractions.Messaging;
using Application.MaterialDomains.Create;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.MaterialDomains;

internal sealed class Create : IEndpoint
{
    public sealed record Request(string Name, string Code);

    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("material-domains", async (
            Request request,
            ICommandHandler<CreateMaterialDomainCommand, Guid> handler,
            CancellationToken cancellationToken) =>
        {
            var command = new CreateMaterialDomainCommand(request.Name, request.Code);

            Result<Guid> result = await handler.Handle(command, cancellationToken);

            return result.Match(Results.Ok, CustomResults.Problem);
        })
        .HasPermission(PermissionCodes.MaterialDomains.Manage)
        .WithTags(Tags.MaterialDomains);
    }
}
