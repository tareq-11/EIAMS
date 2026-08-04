using Application.Abstractions.Authorization;
using Application.Abstractions.Messaging;
using Application.UnitsOfMeasure.Update;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.UnitsOfMeasure;

internal sealed class Update : IEndpoint
{
    public sealed record Request(string Name, string Symbol, string UnitType);

    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPut("units-of-measure/{unitOfMeasureId:guid}", async (
            Guid unitOfMeasureId,
            Request request,
            ICommandHandler<UpdateUnitOfMeasureCommand> handler,
            CancellationToken cancellationToken) =>
        {
            var command = new UpdateUnitOfMeasureCommand(unitOfMeasureId, request.Name, request.Symbol, request.UnitType);

            Result result = await handler.Handle(command, cancellationToken);

            return result.Match(Results.NoContent, CustomResults.Problem);
        })
        .HasPermission(PermissionCodes.UnitsOfMeasure.Manage)
        .WithTags(Tags.UnitsOfMeasure);
    }
}
