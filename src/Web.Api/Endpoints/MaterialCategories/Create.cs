using Application.Abstractions.Authorization;
using Application.Abstractions.Messaging;
using Application.MaterialCategories.Create;
using SharedKernel;
using Web.Api.Extensions;
using Web.Api.Infrastructure;

namespace Web.Api.Endpoints.MaterialCategories;

internal sealed class Create : IEndpoint
{
    public sealed record Request(Guid MaterialDomainId, Guid? ParentCategoryId, string Name, string Code);

    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("material-categories", async (
            Request request,
            ICommandHandler<CreateMaterialCategoryCommand, Guid> handler,
            CancellationToken cancellationToken) =>
        {
            var command = new CreateMaterialCategoryCommand(
                request.MaterialDomainId,
                request.ParentCategoryId,
                request.Name,
                request.Code);

            Result<Guid> result = await handler.Handle(command, cancellationToken);

            return result.Match(Results.Ok, CustomResults.Problem);
        })
        .HasPermission(PermissionCodes.MaterialCategories.Manage)
        .WithTags(Tags.MaterialCategories);
    }
}
