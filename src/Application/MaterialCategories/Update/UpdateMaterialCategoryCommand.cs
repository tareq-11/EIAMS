using Application.Abstractions.Messaging;

namespace Application.MaterialCategories.Update;

public sealed record UpdateMaterialCategoryCommand(Guid MaterialCategoryId, string Name, string Code) : ICommand;
