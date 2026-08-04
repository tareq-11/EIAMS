using Application.Abstractions.Messaging;
using Domain.Common;

namespace Application.MaterialCategories.SetStatus;

public sealed record SetMaterialCategoryStatusCommand(Guid MaterialCategoryId, Status Status) : ICommand;
