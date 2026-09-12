namespace Infrastructure.Authentication;

internal sealed class AdministratorRecoveryOptions
{
    internal const string SectionName = "AdministratorRecovery";
    internal const int TokenBytes = 32;

    public bool Enabled { get; init; }

    public string Token { get; init; } = string.Empty;

    public DateTimeOffset ExpiresAtUtc { get; init; }

    internal static bool TryDecodeToken(string? value, out byte[] tokenBytes)
    {
        tokenBytes = [];
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        try
        {
            tokenBytes = Convert.FromBase64String(value);
            return tokenBytes.Length == TokenBytes;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
