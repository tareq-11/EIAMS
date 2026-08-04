using Application.Abstractions.Authorization;
using Application.Abstractions.Messaging;
using Application.MaterialDomains.Update;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.MaterialDomains;

internal sealed class Update : IEndpoint
{
    public sealed record Request(string Name);

    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPut("material-domains/{materialDomainId:guid}", async (
            Guid materialDomainId,
            Request request,
            ICommandHandler<UpdateMaterialDomainCommand> handler,
            CancellationToken cancellationToken) =>
        {
            var command = new UpdateMaterialDomainCommand(materialDomainId, request.Name);

            Result result = await handler.Handle(command, cancellationToken);

            return result.Match(Results.NoContent, CustomResults.Problem);
        })
        .HasPermission(PermissionCodes.MaterialDomains.Manage)
        .WithTags(Tags.MaterialDomains);
    }
}
