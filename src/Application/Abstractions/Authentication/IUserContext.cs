namespace Application.Abstractions.Authentication;

public interface IUserContext
{
    /// <summary>
    /// The current user's id. Throws when there is no authenticated user - use this in handlers
    /// that require authentication.
    /// </summary>
    Guid UserId { get; }

    /// <summary>
    /// The current user's id, or <see langword="null"/> when there is no authenticated user
    /// (e.g. anonymous endpoints, background/seeding work). Use this for audit stamping.
    /// </summary>
    Guid? UserIdOrDefault { get; }
}
