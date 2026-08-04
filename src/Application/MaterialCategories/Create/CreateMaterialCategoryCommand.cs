using Application.Abstractions.Messaging;

namespace Application.MaterialCategories.Create;

public sealed record CreateMaterialCategoryCommand(Guid MaterialDomainId, Guid? ParentCategoryId, string Name, string Code)
    : ICommand<Guid>;
