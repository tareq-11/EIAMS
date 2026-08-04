using Application.Abstractions.Data;
using Domain.Employees;
using Domain.MaterialCategories;
using Domain.MaterialDomains;
using Domain.MaterialFamilies;
using Domain.Materials;
using Domain.MaterialUnitConversions;
using Domain.OrganizationalUnits;
using Domain.Organizations;
using Domain.Permissions;
using Domain.Roles;
using Domain.Sites;
using Domain.UnitsOfMeasure;
using Domain.Users;
using Domain.UserRoleScopes;
using Infrastructure.DomainEvents;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Infrastructure.Database;

public sealed class ApplicationDbContext(
    DbContextOptions<ApplicationDbContext> options,
    IDomainEventsDispatcher domainEventsDispatcher)
    : DbContext(options), IApplicationDbContext
{
    public DbSet<User> Users { get; set; }

    public DbSet<RefreshToken> RefreshTokens { get; set; }

    public DbSet<Organization> Organizations { get; set; }

    public DbSet<Site> Sites { get; set; }

    public DbSet<OrganizationalUnit> OrganizationalUnits { get; set; }

    public DbSet<Employee> Employees { get; set; }

    public DbSet<Role> Roles { get; set; }

    public DbSet<Permission> Permissions { get; set; }

    public DbSet<RolePermission> RolePermissions { get; set; }

    public DbSet<UserRoleScope> UserRoleScopes { get; set; }

    public DbSet<UnitOfMeasure> UnitsOfMeasure { get; set; }

    public DbSet<MaterialDomain> MaterialDomains { get; set; }

    public DbSet<MaterialCategory> MaterialCategories { get; set; }

    public DbSet<MaterialFamily> MaterialFamilies { get; set; }

    public DbSet<Material> Materials { get; set; }

    public DbSet<MaterialUnitConversion> MaterialUnitConversions { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);

        modelBuilder.HasDefaultSchema(Schemas.Default);
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        // When should you publish domain events?
        //
        // 1. BEFORE calling SaveChangesAsync
        //     - domain events are part of the same transaction
        //     - immediate consistency
        // 2. AFTER calling SaveChangesAsync
        //     - domain events are a separate transaction
        //     - eventual consistency
        //     - handlers can fail

        List<IDomainEvent> domainEvents = ExtractDomainEvents();
        int result = await base.SaveChangesAsync(cancellationToken);

        await PublishDomainEventsAsync(domainEvents);

        return result;
    }

    private async Task PublishDomainEventsAsync(IEnumerable<IDomainEvent> domainEvents)
    {
        await domainEventsDispatcher.DispatchAsync(domainEvents);
    }

    private List<IDomainEvent> ExtractDomainEvents()
    {
        var domainEvents = ChangeTracker
            .Entries()
            .Select(entry => entry.Entity)
            .OfType<IDomainEventSource>()
            .SelectMany(source =>
            {
                List<IDomainEvent> domainEvents = source.DomainEvents;

                source.ClearDomainEvents();

                return domainEvents;
            })
            .ToList();
        return domainEvents;
    }
}
