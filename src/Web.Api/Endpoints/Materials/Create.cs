using Application.Abstractions.Authorization;
using Application.Abstractions.Messaging;
using Application.Materials.Create;
using Domain.Materials;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.Materials;

internal sealed class Create : IEndpoint
{
    public sealed record Request(
        Guid FamilyId,
        string NameAr,
        string? NameEn,
        string Code,
        int MaterialKind,
        int TrackingType,
        bool HasExpiry,
        bool RequiresAssetNumber,
        string? Attributes);

    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("materials", async (
            Request request,
            ICommandHandler<CreateMaterialCommand, Guid> handler,
            CancellationToken cancellationToken) =>
        {
            var command = new CreateMaterialCommand(
                request.FamilyId,
                request.NameAr,
                request.NameEn,
                request.Code,
                (MaterialKind)request.MaterialKind,
                (TrackingType)request.TrackingType,
                request.HasExpiry,
                request.RequiresAssetNumber,
                request.Attributes);

            Result<Guid> result = await handler.Handle(command, cancellationToken);

            return result.Match(Results.Ok, CustomResults.Problem);
        })
        .HasPermission(PermissionCodes.Materials.Manage)
        .WithTags(Tags.Materials);
    }
}
