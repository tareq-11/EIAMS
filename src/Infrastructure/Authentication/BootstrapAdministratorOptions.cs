namespace Infrastructure.Authentication;

internal sealed class BootstrapAdministratorOptions
{
    internal const string SectionName = "BootstrapAdministrator";
    internal const int TokenBytes = 32;

    public bool Enabled { get; init; }

    public string Token { get; init; } = string.Empty;

    /// <summary>
    /// Canonical username of the bootstrap administrator. When set together with
    /// <see cref="SeedPassword"/>, a one-shot Administrator + Enterprise-scope user is seeded at
    /// startup if and only if the database is empty. Intended for local development and CI only;
    /// production tenants must provision their first administrator through the protected
    /// <c>POST /admin/users</c> endpoint.
    /// </summary>
    public string? SeedUsername { get; init; }

    public string? SeedPassword { get; init; }

    public string? SeedEmail { get; init; }

    public string? SeedFirstName { get; init; }

    public string? SeedLastName { get; init; }

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