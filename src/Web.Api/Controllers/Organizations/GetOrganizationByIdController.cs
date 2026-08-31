using Application.Abstractions.Authorization;
using Application.Abstractions.Messaging;
using Application.Organizations.GetById;
using Microsoft.AspNetCore.Mvc;
using SharedKernel;
using Web.Api.Infrastructure;

namespace Web.Api.Controllers.Organizations;

[ApiController]
[Route("organizations")]
[Tags(Tags.Organizations)]
public sealed class GetOrganizationByIdController(IQueryHandler<GetOrganizationByIdQuery, OrganizationResponse> handler)
    : ControllerBase
{
    [HttpGet("{organizationId:guid}")]
    [ProducesResponseType<ApiResponse<OrganizationResponse>>(StatusCodes.Status200OK)]
    [HasPermission(PermissionCodes.Organizations.View)]
    public async Task<IResult> Handle(Guid organizationId, CancellationToken cancellationToken)
    {
        var query = new GetOrganizationByIdQuery(organizationId);

        Result<OrganizationResponse> result = await handler.Handle(query, cancellationToken);

        return result.ToApiResponse(HttpContext);
    }
}
