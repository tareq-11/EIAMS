using Application.Abstractions.Messaging;

namespace Application.Custodies.Assign;

public sealed record AssignAssetCustodyCommand(
    Guid AssetId,
    Guid EmployeeId,
    int ExpectedCustodyRowVersion,
    string? Note) : ICommand<Guid>;
