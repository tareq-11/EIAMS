using Application.Abstractions.Messaging;
using Application.Abstractions.Pagination;
using Domain.Common;

namespace Application.MaterialFamilies.GetList;

public sealed record GetMaterialFamiliesQuery(Guid? CategoryId, Status? Status, int Page, int PageSize)
    : IQuery<PagedResult<MaterialFamilyResponse>>;
