using Application.Abstractions.Messaging;
using Domain.Materials;

namespace Application.Materials.Update;

public sealed record UpdateMaterialCommand(
    Guid MaterialId,
    string NameAr,
    string? NameEn,
    MaterialKind MaterialKind,
    TrackingType TrackingType,
    bool HasExpiry,
    string? Attributes) : ICommand;
