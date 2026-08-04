using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.Permissions.GetList;

internal sealed class GetPermissionsQueryHandler(IApplicationDbContext context)
    : IQueryHandler<GetPermissionsQuery, List<PermissionResponse>>
{
    public async Task<Result<List<PermissionResponse>>> Handle(
        GetPermissionsQuery query,
        CancellationToken cancellationToken)
    {
        List<PermissionResponse> permissions = await context.Permissions
            .Select(p => new PermissionResponse
            {
                Id = p.Id,
                Code = p.Code,
                Description = p.Description
            })
            .ToListAsync(cancellationToken);

        return permissions;
    }
}
