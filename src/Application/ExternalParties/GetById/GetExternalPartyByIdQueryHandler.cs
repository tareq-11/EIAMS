using Application.Abstractions.Data;
using Application.Abstractions.Messaging;
using Domain.ExternalParties;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Application.ExternalParties.GetById;

internal sealed class GetExternalPartyByIdQueryHandler(IApplicationDbContext context)
    : IQueryHandler<GetExternalPartyByIdQuery, ExternalPartyResponse>
{
    public async Task<Result<ExternalPartyResponse>> Handle(
        GetExternalPartyByIdQuery query,
        CancellationToken cancellationToken)
    {
        ExternalPartyResponse? party = await context.ExternalParties
            .AsNoTracking()
            .Where(item => item.Id == query.ExternalPartyId)
            .Select(item => new ExternalPartyResponse(
                item.Id,
                item.NameAr,
                item.Code,
                item.ContactInfo,
                item.Notes,
                item.Status.ToString(),
                item.RowVersion,
                item.CreatedAtUtc,
                item.UpdatedAtUtc))
            .SingleOrDefaultAsync(cancellationToken);

        return party is null
            ? Result.Failure<ExternalPartyResponse>(ExternalPartyErrors.NotFound(query.ExternalPartyId))
            : party;
    }
}
