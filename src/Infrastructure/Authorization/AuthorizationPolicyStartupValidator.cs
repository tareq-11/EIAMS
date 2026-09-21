using Infrastructure.Database;
using Application.Abstractions.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Infrastructure.Authorization;

/// <summary>
/// Validates the persisted authorization marker before the application begins serving requests.
/// Phase 2 only activates the existing colon vocabulary; a dotted marker fails closed until its
/// catalog is present and the coordinated cutover is implemented.
/// </summary>
internal sealed class AuthorizationPolicyStartupValidator(
    IServiceScopeFactory scopeFactory,
    IHostEnvironment environment,
    ILogger<AuthorizationPolicyStartupValidator> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        List<Domain.Permissions.AuthorizationPolicyVersion> markers;
        try
        {
            markers = await context.AuthorizationPolicyVersions
                .AsNoTracking()
                .Where(marker => marker.IsActive)
                .ToListAsync(cancellationToken);
        }
        catch (PostgresException exception) when (exception.SqlState == "42P01" && environment.IsEnvironment("Testing"))
        {
            // The integration factory applies migrations immediately after creating the service
            // provider. Production never bypasses this check when the marker table is missing.
            logger.LogWarning(exception, "Authorization policy marker validation deferred until migrations complete.");
            return;
        }

        if (markers.Count != 1)
        {
            throw new InvalidOperationException("Exactly one active authorization policy marker is required.");
        }

        Domain.Permissions.AuthorizationPolicyVersion marker = markers[0];
        if (marker.MappingVersion <= 0 || marker.ActiveVocabulary is not ("legacy-colon" or "dotted-v1"))
        {
            throw new InvalidOperationException("The active authorization policy marker is unsupported.");
        }

        string[] codes = await context.Permissions
            .AsNoTracking()
            .Select(permission => permission.Code)
            .ToArrayAsync(cancellationToken);

        if (marker.ActiveVocabulary == "legacy-colon")
        {
            var activeLegacyCodes = codes
                .Where(code => code.Contains(':', StringComparison.Ordinal))
                .ToHashSet(StringComparer.Ordinal);
            if (!activeLegacyCodes.SetEquals(PermissionVocabulary.LegacyColonCodes))
            {
                if (!environment.IsEnvironment("Testing"))
                {
                    throw new InvalidOperationException("The legacy-colon authorization catalog is incomplete.");
                }

                logger.LogWarning("Test host started with a partial legacy authorization catalog.");
            }
        }
        else
        {
            var activeDottedCodes = codes
                .Where(code => code.Contains('.', StringComparison.Ordinal))
                .ToHashSet(StringComparer.Ordinal);
            if (!activeDottedCodes.SetEquals(PermissionVocabulary.DottedV1Codes))
            {
                if (!environment.IsEnvironment("Testing"))
                {
                    throw new InvalidOperationException("The dotted authorization catalog is incomplete.");
                }

                logger.LogWarning("Test host started with a partial dotted authorization catalog.");
            }
        }

        logger.LogInformation(
            "Authorization policy validated: vocabulary {Vocabulary}, mapping version {MappingVersion}.",
            marker.ActiveVocabulary,
            marker.MappingVersion);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
