namespace Domain.Common;

/// <summary>
/// Discriminator for unified custody read model and transfer operations.
/// </summary>
public enum CustodySubjectType
{
    Asset,
    TrackedUnit,
    MaterialQuantity
}
