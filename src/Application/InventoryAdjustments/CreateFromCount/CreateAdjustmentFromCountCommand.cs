using Application.Abstractions.Messaging;

namespace Application.InventoryAdjustments.CreateFromCount;

public sealed record CreateAdjustmentFromCountCommand(Guid CountId) : ICommand<Guid>;
