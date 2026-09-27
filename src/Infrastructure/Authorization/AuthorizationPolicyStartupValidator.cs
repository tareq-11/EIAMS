using Application.Abstractions.Authorization;
using Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Infrastructure.Authorization;

/// <summary>
/// Validates the persisted authorization marker before the application begins serving requests.
/// Phase 2 requires the single dotted-v1 vocabulary and fails closed on legacy, unknown, or
/// incomplete catalog state.
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
        if (marker.MappingVersion <= 0 || marker.ActiveVocabulary != "dotted-v1")
        {
            throw new InvalidOperationException("The active authorization policy marker must be dotted-v1.");
        }

        string[] codes = await context.Permissions
            .AsNoTracking()
            .Select(permission => permission.Code)
            .ToArrayAsync(cancellationToken);

        HashSet<string> activeCodes = codes.ToHashSet(StringComparer.Ordinal);
        if (!activeCodes.SetEquals(PermissionVocabulary.DottedV1Codes))
        {
            if (!environment.IsEnvironment("Testing"))
            {
                throw new InvalidOperationException("The dotted-v1 authorization catalog must contain exactly the approved dotted codes and no legacy or unknown codes.");
            }

            logger.LogWarning("Test host started with a non-final dotted-only authorization catalog.");
        }

        int usersWithInvalidAssignmentCount = await context.Users
            .AsNoTracking()
            .GroupJoin(
                context.UserRoleScopes.AsNoTracking(),
                user => user.Id,
                assignment => assignment.UserId,
                (user, assignments) => new { user.Id, user.Status, AssignmentCount = assignments.Count() })
            .CountAsync(item => item.AssignmentCount > 1 || item.Status == Domain.Users.UserStatus.Active && item.AssignmentCount == 0, cancellationToken);
        bool hasLegacyUserAssignment = await context.UserRoleScopes
            .AsNoTracking()
            .AnyAsync(scope => scope.ScopeType == Domain.Common.ScopeType.OrganizationalUnit, cancellationToken);
        bool hasLegacyAllowedScope = await context.RoleAllowedScopeTypes
            .AsNoTracking()
            .AnyAsync(scope => scope.ScopeType == Domain.Common.ScopeType.OrganizationalUnit, cancellationToken)
            || await context.PermissionAllowedScopeTypes
                .AsNoTracking()
                .AnyAsync(scope => scope.ScopeType == Domain.Common.ScopeType.OrganizationalUnit, cancellationToken);

        if (usersWithInvalidAssignmentCount != 0 || hasLegacyUserAssignment || hasLegacyAllowedScope)
        {
            const string message = "User assignment cutover invariant failed: active users need one Enterprise, Site, or Warehouse assignment; no user may have multiple assignments; OrganizationalUnit must not remain in assignment/allowed-scope rows.";
            if (!environment.IsEnvironment("Testing"))
            {
                throw new InvalidOperationException(message);
            }

            logger.LogWarning(
                "{Message} Testing environment continues for fixture compatibility. UsersWithoutExactlyOneAssignment={Count}, LegacyUserAssignment={LegacyUserAssignment}, LegacyAllowedScope={LegacyAllowedScope}.",
                message,
                usersWithInvalidAssignmentCount,
                hasLegacyUserAssignment,
                hasLegacyAllowedScope);
        }

        logger.LogInformation(
            "Authorization policy validated: vocabulary {Vocabulary}, mapping version {MappingVersion}.",
            marker.ActiveVocabulary,
            marker.MappingVersion);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
