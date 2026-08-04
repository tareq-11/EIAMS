using Application.Abstractions.Messaging;

namespace Application.MaterialCategories.Move;

public sealed record MoveMaterialCategoryCommand(Guid MaterialCategoryId, Guid? ParentCategoryId) : ICommand;
