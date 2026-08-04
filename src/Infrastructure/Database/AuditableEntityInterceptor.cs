using Application.Abstractions.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using SharedKernel;

namespace Infrastructure.Database;

internal sealed class AuditableEntityInterceptor(IDateTimeProvider dateTimeProvider, IUserContext userContext)
    : SaveChangesInterceptor
{
    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context is not null)
        {
            UpdateAuditableEntities(eventData.Context);
        }

        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void UpdateAuditableEntities(DbContext context)
    {
        DateTime utcNow = dateTimeProvider.UtcNow;
        Guid? userId = userContext.UserIdOrDefault;

        foreach (EntityEntry<IAuditableEntity> entry in context.ChangeTracker.Entries<IAuditableEntity>())
        {
            if (entry.State == EntityState.Added)
            {
                entry.Property(nameof(IAuditableEntity.CreatedAtUtc)).CurrentValue = utcNow;
                entry.Property(nameof(IAuditableEntity.CreatedBy)).CurrentValue = userId;
            }
            else if (entry.State == EntityState.Modified)
            {
                entry.Property(nameof(IAuditableEntity.UpdatedAtUtc)).CurrentValue = utcNow;
                entry.Property(nameof(IAuditableEntity.UpdatedBy)).CurrentValue = userId;
            }
        }
    }
}
