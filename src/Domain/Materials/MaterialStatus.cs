namespace Domain.Materials;

/// <summary>
/// Material's own lifecycle (PRD Ch. 10.4) - a superset of the shared Active/Inactive
/// (<see cref="Domain.Common.Status"/>) with a terminal Archived state, so it gets its own enum
/// rather than reusing the shared one and allowing an invalid value on every other master entity.
/// </summary>
public enum MaterialStatus
{
    Active,
    Inactive,
    Archived
}
