namespace Domain.Users;

public sealed class RefreshToken
{
    private RefreshToken() { }

    public Guid Id { get; private set; }
    public string Token { get; private set; }
    public Guid UserId { get; private set; }
    public DateTime ExpiresOnUtc { get; private set; }
    public User User { get; init; }

    public static RefreshToken Create(Guid id, string token, Guid userId, DateTime expiresOnUtc)
    {
        return new RefreshToken
        {
            Id = id,
            Token = token,
            UserId = userId,
            ExpiresOnUtc = expiresOnUtc
        };
    }

    public void Rotate(string newToken, DateTime newExpiresOnUtc)
    {
        Token = newToken;
        ExpiresOnUtc = newExpiresOnUtc;
    }
}
