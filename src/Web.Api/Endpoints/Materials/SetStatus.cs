using Application.Abstractions.Authorization;
using Application.Abstractions.Messaging;
using Application.Materials.SetStatus;
using Domain.Materials;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Materials;

internal sealed class SetStatus : IEndpoint
{
    public sealed record Request(int Status);

    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPut("materials/{materialId:guid}/status", async (
            Guid materialId,
            Request request,
            ICommandHandler<SetMaterialStatusCommand> handler,
            CancellationToken cancellationToken) =>
        {
            var command = new SetMaterialStatusCommand(materialId, (MaterialStatus)request.Status);

            Result result = await handler.Handle(command, cancellationToken);

            return result.Match(Results.NoContent, CustomResults.Problem);
        })
        .HasPermission(PermissionCodes.Materials.Manage)
        .WithTags(Tags.Materials);
    }
}
