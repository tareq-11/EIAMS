using Application.Abstractions.Authorization;
using Application.Abstractions.Messaging;
using Application.Materials.Update;
using Domain.Materials;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Materials;

internal sealed class Update : IEndpoint
{
    public sealed record Request(
        string NameAr,
        string? NameEn,
        int MaterialKind,
        int TrackingType,
        bool HasExpiry,
        bool RequiresAssetNumber,
        string? Attributes);

    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPut("materials/{materialId:guid}", async (
            Guid materialId,
            Request request,
            ICommandHandler<UpdateMaterialCommand> handler,
            CancellationToken cancellationToken) =>
        {
            var command = new UpdateMaterialCommand(
                materialId,
                request.NameAr,
                request.NameEn,
                (MaterialKind)request.MaterialKind,
                (TrackingType)request.TrackingType,
                request.HasExpiry,
                request.RequiresAssetNumber,
                request.Attributes);

            Result result = await handler.Handle(command, cancellationToken);

            return result.Match(Results.NoContent, CustomResults.Problem);
        })
        .HasPermission(PermissionCodes.Materials.Manage)
        .WithTags(Tags.Materials);
    }
}
