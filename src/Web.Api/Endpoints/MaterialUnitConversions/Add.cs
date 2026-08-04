using Application.Abstractions.Authorization;
using Application.Abstractions.Messaging;
using Application.MaterialUnitConversions.Add;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.MaterialUnitConversions;

internal sealed class Add : IEndpoint
{
    public sealed record Request(Guid FromUnitId, Guid ToBaseUnitId, decimal Factor);

    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("materials/{materialId:guid}/unit-conversions", async (
            Guid materialId,
            Request request,
            ICommandHandler<AddMaterialUnitConversionCommand, Guid> handler,
            CancellationToken cancellationToken) =>
        {
            var command = new AddMaterialUnitConversionCommand(
                materialId,
                request.FromUnitId,
                request.ToBaseUnitId,
                request.Factor);

            Result<Guid> result = await handler.Handle(command, cancellationToken);

            return result.Match(Results.Ok, CustomResults.Problem);
        })
        .HasPermission(PermissionCodes.Materials.Manage)
        .WithTags(Tags.MaterialUnitConversions);
    }
}
