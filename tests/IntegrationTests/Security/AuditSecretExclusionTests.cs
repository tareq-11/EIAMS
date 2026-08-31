using Domain.AuditLogs;
using Domain.Users;
using Infrastructure.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IntegrationTests.Security;

[Collection(nameof(IntegrationTestCollection))]
public sealed class AuditSecretExclusionTests : BaseIntegrationTest
{
    private readonly IntegrationTestWebAppFactory factory;

    public AuditSecretExclusionTests(IntegrationTestWebAppFactory factory) : base(factory)
    {
        this.factory = factory;
    }

    [Fact]
    public async Task AuditLog_Should_NotStorePasswordHashInFieldEntries()
    {
        // 1. Arrange & Act: Register user
        string email = UniqueEmail();
        Guid userId = await RegisterUserAsync(email);

        // 2. Assert: Query audit log entries for user
        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        List<AuditLogEntry> sensitiveEntries = await context.AuditLogEntries
            .Where(entry => context.AuditLogs.Any(log =>
                log.Id == entry.AuditLogId &&
                log.EntityType == "User" &&
                log.EntityId == userId))
            .Where(entry => EF.Functions.ILike(entry.FieldName, "%password%") ||
                            EF.Functions.ILike(entry.FieldName, "%secret%"))
            .ToListAsync();

        sensitiveEntries.ShouldBeEmpty("Audit log should never capture password or secret fields");
    }
}
