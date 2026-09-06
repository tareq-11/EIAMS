using System.Security.Claims;
using System.Text;
using Application.Abstractions.Assets;
using Application.Abstractions.Audit;
using Application.Abstractions.Authentication;
using Application.Abstractions.Authorization;
using Application.Abstractions.Data;
using Application.Abstractions.InventoryCounts;
using Application.Abstractions.Ledger;
using Application.Abstractions.Numbering;
using Application.Abstractions.Policies;
using Application.Abstractions.PolymorphicReferences;
using Application.Abstractions.Posting;
using Application.Abstractions.Recipients;
using Application.Abstractions.Storage;
using Application.Abstractions.Warehouses;
using Infrastructure.Assets;
using Infrastructure.AuditLogs;
using Infrastructure.Authentication;
using Infrastructure.Authorization;
using Infrastructure.Database;
using Infrastructure.DomainEvents;
using Infrastructure.DocumentLifecycleEvents;
using Infrastructure.InventoryCounts;
using Infrastructure.Ledger;
using Infrastructure.Numbering;
using Infrastructure.Policies;
using Infrastructure.PolymorphicReferences;
using Infrastructure.Recipients;
using Infrastructure.Storage;
using Infrastructure.Time;
using Infrastructure.WarehouseDocuments;
using Infrastructure.Warehouses;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Diagnostics.HealthChecks;
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

        services.AddSingleton<IBootstrapAdministratorAuthorizer, BootstrapAdministratorAuthorizer>();

        services.AddScoped<IAuditOperationContextAccessor, AuditOperationContextAccessor>();

        services.AddScoped<IRequestAuditContext, RequestAuditContext>();

        services.AddSingleton<AuditEntityRegistry>();

        services.AddSingleton<IAuditValuePolicy, AuditValuePolicy>();

        services.AddSingleton<IAuditRedactionService, AuditRedactionService>();

        services.AddTransient<IDomainEventsDispatcher, DomainEventsDispatcher>();

        services.AddSingleton<IDatabaseExceptionClassifier, PostgresDatabaseExceptionClassifier>();

        services.AddScoped<IReferenceNumberGenerator, ReferenceNumberGenerator>();

        services.AddScoped<ICapabilityCheckService, CapabilityCheckService>();

        services.AddScoped<IApplicationTransaction, EfApplicationTransaction>();

        services.AddScoped<IApplicationLock, PostgresApplicationLock>();

        services.AddScoped<IDocumentLock, ApplicationDocumentLock>();

        services.AddScoped<IInventoryLedgerWriter, InventoryLedgerWriter>();

        services.AddScoped<IInventoryKeyLock, PostgresInventoryKeyLock>();

        services.AddScoped<IDocumentPostingCoordinator, DocumentPostingCoordinator>();

        services.AddScoped<IDocumentPostingScopeResolver, DocumentPostingScopeResolver>();

        services.AddScoped<IActivePartyLookup, ActivePartyLookup>();

        services.AddScoped<ICounterpartResolver, CounterpartResolver>();

        services.AddScoped<IReversalPostingStrategy, ReversalPostingStrategy>();

        services.AddScoped<IDocumentPostingStrategy, ReceivingPostingStrategy>();

        services.AddScoped<IDocumentPostingStrategy, OpeningPostingStrategy>();

        services.AddScoped<IDocumentPostingStrategy, IssuePostingStrategy>();

        services.AddScoped<IDocumentPostingStrategy, TransferPostingStrategy>();

        services.AddScoped<IDocumentPostingStrategy, ReturnPostingStrategy>();

        services.AddScoped<IDocumentPostingStrategy, AdjustmentPostingStrategy>();

        services.AddScoped<IDocumentReversalSideEffectStrategy, AssetCreationReversalSideEffectStrategy>();

        services.AddScoped<IDocumentReversalSideEffectStrategy, AssetCustodyReversalSideEffectStrategy>();

        services.AddScoped<IDocumentReversalSideEffectStrategy, AdjustmentReversalSideEffectStrategy>();

        services.AddSingleton<IAssetNumberGenerator, AssetNumberGenerator>();

        services.AddScoped<IReceivedAssetFactory, ReceivedAssetFactory>();

        services.AddScoped<IAssetUsageChecker, AssetUsageChecker>();

        services.AddScoped<IAssetKeyLock, PostgresAssetKeyLock>();

        services.AddScoped<IAssetLifecycleGuard, AssetLifecycleGuard>();

        services.AddScoped<IWarehouseOperationLock, PostgresWarehouseOperationLock>();

        services.AddScoped<IInventoryFreezePolicyService, InventoryFreezePolicyService>();

        services.AddScoped<ITransferPolicyService, TransferPolicyService>();

        services.AddScoped<IPolymorphicReferenceAuditor, PolymorphicReferenceAuditor>();

        services.AddHostedService<PolymorphicReferenceAuditWorker>();

        services.AddScoped<AssetPostingSelectionService>();

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

        services.AddOptions<TransferPolicyOptions>()
            .Bind(configuration.GetSection(TransferPolicyOptions.SectionName))
            .ValidateOnStart();

        services.AddOptions<InventoryPolicyOptions>()
            .Bind(configuration.GetSection(InventoryPolicyOptions.SectionName))
            .Validate(
                options => string.Equals(
                    options.NegativeStockPolicy,
                    InventoryPolicyOptions.Block,
                    StringComparison.Ordinal),
                "Policies:Inventory:NegativeStockPolicy must be 'Block'.")
            .ValidateOnStart();

        services.AddOptions<PolymorphicReferenceAuditOptions>()
            .Bind(configuration.GetSection(PolymorphicReferenceAuditOptions.SectionName))
            .Validate(options => options.InitialDelay >= TimeSpan.Zero,
                "PolymorphicReferenceAudit:InitialDelay must not be negative.")
            .Validate(options => options.Interval > TimeSpan.Zero,
                "PolymorphicReferenceAudit:Interval must be greater than zero.")
            .Validate(options => options.MaximumLoggedFindingsPerCycle is > 0 and <= 10_000,
                "PolymorphicReferenceAudit:MaximumLoggedFindingsPerCycle must be between 1 and 10000.")
            .ValidateOnStart();

        services.AddOptions<BootstrapAdministratorOptions>()
            .Bind(configuration.GetSection(BootstrapAdministratorOptions.SectionName))
            .Validate(
                options => !options.Enabled ||
                           BootstrapAdministratorOptions.TryDecodeToken(options.Token, out _),
                $"BootstrapAdministrator:Token must be Base64 for exactly {BootstrapAdministratorOptions.TokenBytes} random bytes when bootstrap is enabled.")
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
        int commandTimeoutSeconds = configuration.GetValue<int?>("DatabasePerformance:CommandTimeoutSeconds") ?? 30;

        if (commandTimeoutSeconds is < 1 or > 600)
        {
            throw new InvalidOperationException(
                "DatabasePerformance:CommandTimeoutSeconds must be between 1 and 600 seconds.");
        }

        services.AddScoped<AuditableEntityInterceptor>();

        services.AddScoped<AuditSaveChangesInterceptor>();

        services.AddScoped<DocumentLifecycleSaveChangesInterceptor>();

        services.AddDbContext<ApplicationDbContext>(
            (sp, options) => options
                .UseNpgsql(connectionString, npgsqlOptions =>
                    npgsqlOptions
                        .MigrationsHistoryTable(HistoryRepository.DefaultTableName, Schemas.Default)
                        .CommandTimeout(commandTimeoutSeconds))
                .UseSnakeCaseNamingConvention()
                .AddInterceptors(
                    sp.GetRequiredService<AuditableEntityInterceptor>(),
                    sp.GetRequiredService<DocumentLifecycleSaveChangesInterceptor>(),
                    sp.GetRequiredService<AuditSaveChangesInterceptor>())
                .AddInterceptors(sp.GetServices<DbCommandInterceptor>()));

        services.AddScoped<IApplicationDbContext>(sp => sp.GetRequiredService<ApplicationDbContext>());

        return services;
    }

    private static IServiceCollection AddHealthChecks(this IServiceCollection services, IConfiguration configuration)
    {
        services
            .AddHealthChecks()
            .AddCheck(
                "self",
                () => HealthCheckResult.Healthy(),
                tags: ["live"])
            .AddNpgSql(
                configuration.GetConnectionString("Database")!,
                name: "postgresql",
                tags: ["ready"]);

        return services;
    }

    private static IServiceCollection AddAuthenticationInternal(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var jwtOptions = JwtOptions.FromConfiguration(configuration);

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(o =>
            {
                o.RequireHttpsMetadata = jwtOptions.RequireHttpsMetadata;
                o.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.Secret)),
                    ValidateIssuer = true,
                    ValidIssuer = jwtOptions.Issuer,
                    ValidateAudience = true,
                    ValidAudience = jwtOptions.Audience,
                    ValidateLifetime = true,
                    RequireExpirationTime = true,
                    RequireSignedTokens = true,
                    ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
                    ClockSkew = TimeSpan.Zero
                };
                o.Events = new JwtBearerEvents
                {
                    OnTokenValidated = context =>
                    {
                        string? subject = context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier) ??
                                          context.Principal?.FindFirstValue("sub");

                        if (!Guid.TryParse(subject, out Guid userId) || userId == Guid.Empty)
                        {
                            context.Fail("The access token subject is missing or invalid.");
                        }

                        return Task.CompletedTask;
                    }
                };
            });

        services.AddSingleton(jwtOptions);
        services.AddHttpContextAccessor();
        services.AddScoped<IUserContext, UserContext>();
        services.AddSingleton<IPasswordHasher, PasswordHasher>();
        services.AddSingleton<ITokenProvider, TokenProvider>();

        return services;
    }

    private static IServiceCollection AddAuthorizationInternal(this IServiceCollection services)
    {
        services.AddAuthorization();

        services.AddScoped<IScopeAuthorizationService, ScopeAuthorizationService>();

        services.AddTransient<IAuthorizationHandler, PermissionAuthorizationHandler>();

        services.AddTransient<IAuthorizationPolicyProvider, PermissionAuthorizationPolicyProvider>();

        return services;
    }
}
