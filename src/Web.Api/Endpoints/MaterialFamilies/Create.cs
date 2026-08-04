using Application.Abstractions.Authorization;
using Application.Abstractions.Messaging;
using Application.MaterialFamilies.Create;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.MaterialFamilies;

internal sealed class Create : IEndpoint
{
    public sealed record Request(Guid CategoryId, string Name, string Code, Guid BaseUnitId);

    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("material-families", async (
            Request request,
            ICommandHandler<CreateMaterialFamilyCommand, Guid> handler,
            CancellationToken cancellationToken) =>
        {
            var command = new CreateMaterialFamilyCommand(
                request.CategoryId,
                request.Name,
                request.Code,
                request.BaseUnitId);

            Result<Guid> result = await handler.Handle(command, cancellationToken);

            return result.Match(Results.Ok, CustomResults.Problem);
        })
        .HasPermission(PermissionCodes.MaterialFamilies.Manage)
        .WithTags(Tags.MaterialFamilies);
    }
}
