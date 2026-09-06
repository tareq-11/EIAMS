using System.Security.Cryptography;
using Application.Abstractions.Authentication;
using Microsoft.Extensions.Options;

namespace Infrastructure.Authentication;

internal sealed class BootstrapAdministratorAuthorizer(
    IOptions<BootstrapAdministratorOptions> options) : IBootstrapAdministratorAuthorizer
{
    private readonly BootstrapAdministratorOptions options = options.Value;
    private readonly byte[] expectedToken = BootstrapAdministratorOptions.TryDecodeToken(
        options.Value.Token,
        out byte[] configuredToken)
        ? configuredToken
        : [];

    public bool IsAuthorized(string? bootstrapToken)
    {
        if (!options.Enabled ||
            !BootstrapAdministratorOptions.TryDecodeToken(bootstrapToken, out byte[] suppliedToken))
        {
            return false;
        }

        return expectedToken.Length == BootstrapAdministratorOptions.TokenBytes &&
               CryptographicOperations.FixedTimeEquals(expectedToken, suppliedToken);
    }
}
