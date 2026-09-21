using Application.Abstractions.Authentication;
using Application.Abstractions.Data;
using Domain.Common;
using Domain.Permissions;
using Domain.Roles;
using Domain.UserRoleScopes;
using Domain.Users;
using Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SharedKernel;

namespace Infrastructure.Authentication;

/// <summary>
/// Creates the first SYSTEM_ADMIN only when explicitly enabled and the database is empty.
/// The transaction-scoped advisory lock makes concurrent application starts idempotent.
/// </summary>
internal sealed class BootstrapAdministratorSeeder(
    IServiceScopeFactory scopeFactory,
    IOptions<BootstrapAdministratorOptions> options,
    ILogger<BootstrapAdministratorSeeder> logger) : IHostedService
{
    private const string BootstrapLockKey = "security:bootstrap-administrator";

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        BootstrapAdministratorOptions settings = options.Value;
        if (!settings.Enabled)
        {
            return;
        }

        await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        IApplicationTransaction transaction = scope.ServiceProvider.GetRequiredService<IApplicationTransaction>();
        IApplicationLock applicationLock = scope.ServiceProvider.GetRequiredService<IApplicationLock>();
        IPasswordHasher passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
        Guid? createdUserId = null;

        Result result = await transaction.ExecuteAsync(
            async ct =>
            {
                await applicationLock.AcquireAsync(BootstrapLockKey, ct);

                bool roleExists = await context.Roles
                    .AsNoTracking()
                    .AnyAsync(role => role.Id == WellKnownRoles.AdministratorId, ct);
                if (!roleExists)
                {
                    throw new InvalidOperationException(
                        "Bootstrap administrator cannot start because the SYSTEM_ADMIN role is missing.");
                }

                var assignedPermissions = await (
                        from rolePermission in context.RolePermissions.AsNoTracking()
                        join permission in context.Permissions.AsNoTracking()
                            on rolePermission.PermissionId equals permission.Id
                        where rolePermission.RoleId == WellKnownRoles.AdministratorId
                        select new { permission.Id, permission.Code })
                    .ToListAsync(ct);
                var actualDottedPermissionIds = assignedPermissions
                    .Where(permission => permission.Code.Contains('.'))
                    .Select(permission => permission.Id)
                    .ToHashSet();
                if (!actualDottedPermissionIds.SetEquals(WellKnownDottedPermissions.SystemAdministratorPermissionIds))
                {
                    throw new InvalidOperationException(
                        "Bootstrap administrator cannot start because SYSTEM_ADMIN dotted grants are incomplete or contain unexpected permissions.");
                }

                bool activeAdministratorExists = await (
                        from assignment in context.UserRoleScopes
                        join candidate in context.Users on assignment.UserId equals candidate.Id
                        where assignment.RoleId == WellKnownRoles.AdministratorId &&
                              assignment.ScopeType == ScopeType.Enterprise &&
                              assignment.ScopeId == null &&
                              candidate.Status == UserStatus.Active
                        select assignment.Id)
                    .AnyAsync(ct);

                if (activeAdministratorExists)
                {
                    return Result.Success();
                }

                if (await context.Users.AsNoTracking().AnyAsync(ct))
                {
                    throw new InvalidOperationException(
                        "Bootstrap administrator cannot create an account in a non-empty database without an active SYSTEM_ADMIN. Use the administrator recovery flow.");
                }

                string normalizedEmail = User.NormalizeEmail(settings.Email);
                string normalizedUsername = User.NormalizeUsername(settings.Username);
                var user = User.Create(
                    Guid.NewGuid(),
                    normalizedEmail,
                    normalizedUsername,
                    settings.FirstName,
                    settings.LastName,
                    passwordHasher.Hash(settings.Password));

                context.Users.Add(user);
                context.UserRoleScopes.Add(UserRoleScope.Create(
                    Guid.NewGuid(),
                    user.Id,
                    WellKnownRoles.AdministratorId,
                    ScopeType.Enterprise,
                    scopeId: null));

                await context.SaveChangesAsync(ct);
                createdUserId = user.Id;
                return Result.Success();
            },
            cancellationToken);

        if (result.IsFailure)
        {
            throw new InvalidOperationException(result.Error.Description);
        }

        if (createdUserId.HasValue)
        {
            logger.LogInformation("Bootstrap SYSTEM_ADMIN created with user id {UserId}.", createdUserId.Value);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
