using Application.Abstractions.Authorization;
using Application.Abstractions.Messaging;
using Application.MaterialFamilies.SetStatus;
using Domain.Common;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.MaterialFamilies;

internal sealed class SetStatus : IEndpoint
{
    public sealed record Request(int Status);

    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPut("material-families/{materialFamilyId:guid}/status", async (
            Guid materialFamilyId,
            Request request,
            ICommandHandler<SetMaterialFamilyStatusCommand> handler,
            CancellationToken cancellationToken) =>
        {
            var command = new SetMaterialFamilyStatusCommand(materialFamilyId, (Status)request.Status);

            Result result = await handler.Handle(command, cancellationToken);

            return result.Match(Results.NoContent, CustomResults.Problem);
        })
        .HasPermission(PermissionCodes.MaterialFamilies.Manage)
        .WithTags(Tags.MaterialFamilies);
    }
}
