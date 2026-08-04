using Application.Abstractions.Authorization;
using Application.Abstractions.Messaging;
using Application.MaterialDomains.SetStatus;
using Domain.Common;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.MaterialDomains;

internal sealed class SetStatus : IEndpoint
{
    public sealed record Request(int Status);

    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPut("material-domains/{materialDomainId:guid}/status", async (
            Guid materialDomainId,
            Request request,
            ICommandHandler<SetMaterialDomainStatusCommand> handler,
            CancellationToken cancellationToken) =>
        {
            var command = new SetMaterialDomainStatusCommand(materialDomainId, (Status)request.Status);

            Result result = await handler.Handle(command, cancellationToken);

            return result.Match(Results.NoContent, CustomResults.Problem);
        })
        .HasPermission(PermissionCodes.MaterialDomains.Manage)
        .WithTags(Tags.MaterialDomains);
    }
}
