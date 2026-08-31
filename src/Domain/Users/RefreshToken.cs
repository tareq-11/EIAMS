namespace Domain.Users;

public sealed class RefreshToken
{
    private RefreshToken() { }

    public Guid Id { get; private set; }
    public string Token { get; private set; }
    public Guid UserId { get; private set; }
    public DateTime CreatedOnUtc { get; private set; }
    public DateTime ExpiresOnUtc { get; private set; }
    public DateTime? RevokedOnUtc { get; private set; }
    public string? ReplacedByToken { get; private set; }
    public User User { get; init; }

    public static RefreshToken Create(
        Guid id,
        string token,
        Guid userId,
        DateTime expiresOnUtc,
        DateTime createdOnUtc)
    {
        return new RefreshToken
        {
            Id = id,
            Token = token,
            UserId = userId,
            CreatedOnUtc = createdOnUtc,
            ExpiresOnUtc = expiresOnUtc
        };
    }

    public void Revoke(DateTime revokedOnUtc)
    {
        RevokedOnUtc = revokedOnUtc;
    }

    public void RevokeAndRotate(string newToken, DateTime revokedOnUtc)
    {
        RevokedOnUtc = revokedOnUtc;
        ReplacedByToken = newToken;
    }
}
