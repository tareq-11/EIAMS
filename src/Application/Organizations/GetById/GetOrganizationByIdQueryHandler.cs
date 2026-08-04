using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.Organizations;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Organizations.GetById;

internal sealed class GetOrganizationByIdQueryHandler(IApplicationDbContext context)
    : IQueryHandler<GetOrganizationByIdQuery, OrganizationResponse>
{
    public async Task<Result<OrganizationResponse>> Handle(
        GetOrganizationByIdQuery query,
        CancellationToken cancellationToken)
    {
        OrganizationResponse? organization = await context.Organizations
            .Where(o => o.Id == query.OrganizationId)
            .Select(o => new OrganizationResponse
            {
                Id = o.Id,
                Name = o.Name,
                Code = o.Code,
                Status = o.Status.ToString()
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (organization is null)
        {
            return Result.Failure<OrganizationResponse>(OrganizationErrors.NotFound(query.OrganizationId));
        }

        return organization;
    }
}
