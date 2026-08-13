using System.Text;
using Application.Abstractions.Authentication;
using Application.Abstractions.Assets;
using Application.Abstractions.Authorization;
using Application.Abstractions.Data;
using Application.Abstractions.Ledger;
using Application.Abstractions.Numbering;
using Application.Abstractions.Posting;
using Application.Abstractions.Recipients;
using Application.Abstractions.Storage;
using Application.Abstractions.Warehouses;
using Infrastructure.Authentication;
using Infrastructure.Assets;
using Infrastructure.Authorization;
using Infrastructure.Database;
using Infrastructure.DomainEvents;
using Infrastructure.Ledger;
using Infrastructure.Numbering;
using Infrastructure.Recipients;
using Infrastructure.Storage;
using Infrastructure.Time;
using Infrastructure.Warehouses;
using Infrastructure.WarehouseDocuments;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using SharedKernel;

namespace Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration) =>
        services
            .AddServices(configuration)
            .AddDatabase(configuration)
            .AddHealthChecks(configuration)
            .AddAuthenticationInternal(configuration)
            .AddAuthorizationInternal();

    private static IServiceCollection AddServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<IDateTimeProvider, DateTimeProvider>();

        services.AddTransient<IDomainEventsDispatcher, DomainEventsDispatcher>();

        services.AddSingleton<IDatabaseExceptionClassifier, PostgresDatabaseExceptionClassifier>();

        services.AddScoped<IReferenceNumberGenerator, ReferenceNumberGenerator>();

        services.AddScoped<ICapabilityCheckService, CapabilityCheckService>();

        services.AddScoped<IApplicationTransaction, EfApplicationTransaction>();

        services.AddScoped<IDocumentLock, ApplicationDocumentLock>();

        services.AddScoped<IInventoryLedgerWriter, InventoryLedgerWriter>();

        services.AddScoped<IInventoryKeyLock, PostgresInventoryKeyLock>();

        services.AddScoped<IDocumentPostingCoordinator, DocumentPostingCoordinator>();

        services.AddScoped<IDocumentPostingScopeResolver, DocumentPostingScopeResolver>();

        services.AddScoped<IActivePartyLookup, ActivePartyLookup>();

        services.AddScoped<IReversalPostingStrategy, ReversalPostingStrategy>();

        services.AddScoped<IDocumentPostingStrategy, ReceivingPostingStrategy>();

        services.AddScoped<IDocumentPostingStrategy, OpeningPostingStrategy>();

        services.AddScoped<IDocumentPostingStrategy, IssuePostingStrategy>();

        services.AddScoped<IDocumentPostingStrategy, TransferPostingStrategy>();

        services.AddScoped<IDocumentReversalSideEffectStrategy, AssetCreationReversalSideEffectStrategy>();

        services.AddSingleton<IAssetNumberGenerator, AssetNumberGenerator>();

        services.AddScoped<IReceivedAssetFactory, ReceivedAssetFactory>();

        services.AddScoped<IAssetUsageChecker, AssetUsageChecker>();

        services.AddScoped<IFileStorage, LocalFileStorage>();

        services.AddScoped<IAttachmentFileCleanup, AttachmentFileCleanup>();

        services.AddHostedService<FileCleanupWorker>();

        services.AddOptions<LocalFileStorageOptions>()
            .Bind(configuration.GetSection(LocalFileStorageOptions.SectionName))
            .Validate(options => !string.IsNullOrWhiteSpace(options.RootPath),
                "AttachmentStorage:Local:RootPath is required.")
            .ValidateOnStart();

        services.AddOptions<AssetCreationOptions>()
            .Bind(configuration.GetSection(AssetCreationOptions.SectionName))
            .Validate(options => options.MaxAssetsPerLine is > 0 and <= 100_000,
                "AssetCreation:MaxAssetsPerLine must be between 1 and 100000.")
            .Validate(options =>
                    options.MaxAssetsPerDocument >= options.MaxAssetsPerLine &&
                    options.MaxAssetsPerDocument <= 1_000_000,
                "AssetCreation:MaxAssetsPerDocument must be at least MaxAssetsPerLine and no more than 1000000.")
            .Validate(options => options.MaxLinesPerDocument is > 0 and <= 10_000,
                "AssetCreation:MaxLinesPerDocument must be between 1 and 10000.")
            .ValidateOnStart();

        services.AddOptions<AttachmentStorageOptions>()
            .Bind(configuration.GetSection(AttachmentStorageOptions.SectionName))
            .Validate(options => options.MaxFileSizeInBytes > 0,
                "AttachmentStorage:MaxFileSizeInBytes must be greater than 0.")
            .Validate(options => options.AllowedMimeTypes.Length > 0,
                "AttachmentStorage:AllowedMimeTypes must not be empty.")
            .ValidateOnStart();

        services.AddOptions<FileCleanupOptions>()
            .Bind(configuration.GetSection(FileCleanupOptions.SectionName))
            .Validate(options => options.PollInterval > TimeSpan.Zero,
                "AttachmentStorage:Cleanup:PollInterval must be greater than zero.")
            .Validate(options => options.BatchSize is > 0 and <= 1_000,
                "AttachmentStorage:Cleanup:BatchSize must be between 1 and 1000.")
            .Validate(options => options.MaxRetryDelay > TimeSpan.Zero,
                "AttachmentStorage:Cleanup:MaxRetryDelay must be greater than zero.")
            .ValidateOnStart();

        services.AddOptions<NumberingOptions>()
            .Bind(configuration.GetSection(NumberingOptions.SectionName))
            .Validate(options => options.SequencePadding is > 0 and <= 12,
                "Numbering:SequencePadding must be between 1 and 12.")
            .Validate(options => !string.IsNullOrWhiteSpace(options.Separator),
                "Numbering:Separator is required.")
            .Validate(options => options.MaxReferenceNumberLength is > 0 and <= 100,
                "Numbering:MaxReferenceNumberLength must be between 1 and 100.")
            .Validate(options => options.DocumentTypeCodes().All(code =>
                    !string.IsNullOrWhiteSpace(code) &&
                    !code.Contains(options.Separator, StringComparison.Ordinal)),
                "Document type codes are required and must not contain Numbering:Separator.")
            .Validate(options => options.DocumentTypeCodes()
                    .Distinct(StringComparer.Ordinal)
                    .Count() == Enum.GetValues<Domain.Common.DocumentType>().Length,
                "Document type codes must be unique.")
            .ValidateOnStart();

#pragma warning disable EXTEXP0018 // HybridCache is released; the API is stable in .NET 10.
        services.AddHybridCache();
#pragma warning restore EXTEXP0018

        return services;
    }

    private static IServiceCollection AddDatabase(this IServiceCollection services, IConfiguration configuration)
    {
        string connectionString = configuration.GetConnectionString("Database")
            ?? throw new InvalidOperationException(
                "Database connection string is not configured. " +
                "Set 'ConnectionStrings:Database' in appsettings.json or user secrets.");

        services.AddScoped<AuditableEntityInterceptor>();

        services.AddDbContext<ApplicationDbContext>(
            (sp, options) => options
                .UseNpgsql(connectionString, npgsqlOptions =>
                    npgsqlOptions.MigrationsHistoryTable(HistoryRepository.DefaultTableName, Schemas.Default))
                .UseSnakeCaseNamingConvention()
                .AddInterceptors(sp.GetRequiredService<AuditableEntityInterceptor>()));

        services.AddScoped<IApplicationDbContext>(sp => sp.GetRequiredService<ApplicationDbContext>());

        return services;
    }

    private static IServiceCollection AddHealthChecks(this IServiceCollection services, IConfiguration configuration)
    {
        services
            .AddHealthChecks()
            .AddNpgSql(configuration.GetConnectionString("Database")!);

        return services;
    }

    private static IServiceCollection AddAuthenticationInternal(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(o =>
            {
                o.RequireHttpsMetadata = false;
                o.TokenValidationParameters = new TokenValidationParameters
                {
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(configuration["Jwt:Secret"]!)),
                    ValidIssuer = configuration["Jwt:Issuer"],
                    ValidAudience = configuration["Jwt:Audience"],
                    ClockSkew = TimeSpan.Zero
                };
            });

        services.AddHttpContextAccessor();
        services.AddScoped<IUserContext, UserContext>();
        services.AddSingleton<IPasswordHasher, PasswordHasher>();
        services.AddSingleton<ITokenProvider, TokenProvider>();

        return services;
    }

    private static IServiceCollection AddAuthorizationInternal(this IServiceCollection services)
    {
        services.AddAuthorization();

        services.AddScoped<PermissionProvider>();

        services.AddScoped<IScopeAuthorizationService, ScopeAuthorizationService>();

        services.AddTransient<IAuthorizationHandler, PermissionAuthorizationHandler>();

        services.AddTransient<IAuthorizationPolicyProvider, PermissionAuthorizationPolicyProvider>();

        return services;
    }
}
