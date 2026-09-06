using Application.Abstractions.Authentication;
using Domain.Common;
using Domain.Roles;
using Domain.UserRoleScopes;
using Domain.Users;
using Infrastructure.Database;
using IntegrationTests.Performance;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;
using Web.Api;

namespace IntegrationTests;

public sealed class IntegrationTestWebAppFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    internal const string AdministratorEmail = "integration-admin@example.com";
    internal const string AdministratorPassword = "Password123!";
    internal const string JwtSecret = "super-duper-secret-value-that-should-be-in-user-secrets";
    internal const string JwtIssuer = "clean-architecture-template";
    internal const string JwtAudience = "developers";

    private readonly string attachmentStoragePath = Path.Combine(
        Path.GetTempPath(),
        $"eiams-integration-attachments-{Guid.NewGuid():N}");

    private readonly PostgreSqlContainer _dbContainer = new PostgreSqlBuilder("postgres:17-alpine")
        .WithDatabase("clean-architecture-template")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:Database", _dbContainer.GetConnectionString());

        // Provide deterministic JWT settings so tokens can be issued and validated in tests.
        builder.UseSetting("Jwt:Secret", JwtSecret);
        builder.UseSetting("Jwt:Issuer", JwtIssuer);
        builder.UseSetting("Jwt:Audience", JwtAudience);
        builder.UseSetting("Jwt:ExpirationInMinutes", "60");
        builder.UseSetting("AttachmentStorage:Local:RootPath", attachmentStoragePath);

        // Relax rate limiting so the test suite is not throttled.
        builder.UseSetting("RateLimiting:Global:PermitLimit", "100000");
        builder.UseSetting("RateLimiting:Authentication:PermitLimit", "100000");
        builder.UseSetting("RateLimiting:Authentication:ConcurrencyLimit", "100000");
        builder.UseSetting("RateLimiting:Authentication:GlobalConcurrencyLimit", "100000");
        builder.UseSetting("RateLimiting:Concurrency:Reporting", "100000");
        builder.UseSetting("RateLimiting:Concurrency:Upload", "100000");
        builder.UseSetting("RateLimiting:Concurrency:Posting", "100000");

        builder.ConfigureServices(services =>
        {
            services.AddSingleton<SqlCommandCounterInterceptor>();
            services.AddSingleton<DbCommandInterceptor>(serviceProvider =>
                serviceProvider.GetRequiredService<SqlCommandCounterInterceptor>());
        });
    }

    public async Task InitializeAsync()
    {
        await _dbContainer.StartAsync();

        using IServiceScope scope = Services.CreateScope();
        ApplicationDbContext dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await dbContext.Database.MigrateAsync();

        IPasswordHasher passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
        var administrator = User.Create(
            Guid.NewGuid(),
            AdministratorEmail,
            "Integration",
            "Administrator",
            passwordHasher.Hash(AdministratorPassword));

        dbContext.Users.Add(administrator);
        dbContext.UserRoleScopes.Add(UserRoleScope.Create(
            Guid.NewGuid(),
            administrator.Id,
            WellKnownRoles.AdministratorId,
            ScopeType.Enterprise,
            scopeId: null));

        await dbContext.SaveChangesAsync();
    }

    internal WebApplicationFactory<Program> CreateSiblingFactory() =>
        new SiblingWebAppFactory(_dbContainer.GetConnectionString());

    public new async Task DisposeAsync()
    {
        await _dbContainer.DisposeAsync();
        await base.DisposeAsync();

        if (Directory.Exists(attachmentStoragePath))
        {
            Directory.Delete(attachmentStoragePath, recursive: true);
        }
    }

    private sealed class SiblingWebAppFactory(string connectionString) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseSetting("ConnectionStrings:Database", connectionString);
            builder.UseSetting("Jwt:Secret", JwtSecret);
            builder.UseSetting("Jwt:Issuer", JwtIssuer);
            builder.UseSetting("Jwt:Audience", JwtAudience);
            builder.UseSetting("Jwt:ExpirationInMinutes", "60");
            builder.UseSetting(
                "AttachmentStorage:Local:RootPath",
                Path.Combine(Path.GetTempPath(), $"eiams-sibling-attachments-{Guid.NewGuid():N}"));
            builder.UseSetting("RateLimiting:Global:PermitLimit", "100000");
            builder.UseSetting("RateLimiting:Authentication:PermitLimit", "100000");
            builder.UseSetting("RateLimiting:Authentication:ConcurrencyLimit", "100000");
            builder.UseSetting("RateLimiting:Authentication:GlobalConcurrencyLimit", "100000");
            builder.UseSetting("RateLimiting:Concurrency:Reporting", "100000");
            builder.UseSetting("RateLimiting:Concurrency:Upload", "100000");
            builder.UseSetting("RateLimiting:Concurrency:Posting", "100000");
        }
    }
}
