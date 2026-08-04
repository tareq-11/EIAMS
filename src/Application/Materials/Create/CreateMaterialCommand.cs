using Application.Abstractions.Messaging;
using Domain.Materials;

namespace Application.Materials.Create;

public sealed record CreateMaterialCommand(
    Guid FamilyId,
    string NameAr,
    string? NameEn,
    string Code,
    MaterialKind MaterialKind,
    TrackingType TrackingType,
    bool HasExpiry,
    bool RequiresAssetNumber,
    string? Attributes) : ICommand<Guid>;
