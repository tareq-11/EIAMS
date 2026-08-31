using Application.Abstractions.Messaging;
using Application.Abstractions.Pagination;
using Domain.Common;

namespace Application.Custodies.GetCustodies;

public sealed record GetCustodiesQuery(
    PartyType? HolderType = null,
    Guid? HolderId = null,
    Guid? MaterialId = null,
    CustodySubjectType? SubjectType = null,
    string? Status = null,
    Guid? WarehouseId = null,
    int Page = 1,
    int PageSize = 20) : IQuery<PagedResult<CustodyResponse>>;
