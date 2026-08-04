using Application.Abstractions.Authorization;
using Application.Abstractions.Messaging;
using Application.UnitsOfMeasure.Create;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.UnitsOfMeasure;

internal sealed class Create : IEndpoint
{
    public sealed record Request(string Name, string Symbol, string UnitType);

    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("units-of-measure", async (
            Request request,
            ICommandHandler<CreateUnitOfMeasureCommand, Guid> handler,
            CancellationToken cancellationToken) =>
        {
            var command = new CreateUnitOfMeasureCommand(request.Name, request.Symbol, request.UnitType);

            Result<Guid> result = await handler.Handle(command, cancellationToken);

            return result.Match(Results.Ok, CustomResults.Problem);
        })
        .HasPermission(PermissionCodes.UnitsOfMeasure.Manage)
        .WithTags(Tags.UnitsOfMeasure);
    }
}
