using Application.Abstractions.Authorization;
using Application.Abstractions.Messaging;
using Application.MaterialUnitConversions.Remove;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.MaterialUnitConversions;

internal sealed class Remove : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapDelete("material-unit-conversions/{materialUnitConversionId:guid}", async (
            Guid materialUnitConversionId,
            ICommandHandler<RemoveMaterialUnitConversionCommand> handler,
            CancellationToken cancellationToken) =>
        {
            var command = new RemoveMaterialUnitConversionCommand(materialUnitConversionId);

            Result result = await handler.Handle(command, cancellationToken);

            return result.Match(Results.NoContent, CustomResults.Problem);
        })
        .HasPermission(PermissionCodes.Materials.Manage)
        .WithTags(Tags.MaterialUnitConversions);
    }
}
