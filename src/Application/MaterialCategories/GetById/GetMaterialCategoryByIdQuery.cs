using Application.Abstractions.Messaging;

namespace Application.MaterialCategories.GetById;

public sealed record GetMaterialCategoryByIdQuery(Guid MaterialCategoryId) : IQuery<MaterialCategoryResponse>;
