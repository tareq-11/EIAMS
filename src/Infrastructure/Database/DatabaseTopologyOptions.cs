using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace Infrastructure.Database;

/// <summary>
/// Explicitly rejects topology features that the current request and authorization model cannot
/// safely use. There is one primary DbContext connection: authorization, scope expansion, token
/// revocation, and every operational read therefore observe primary state.
/// </summary>
public sealed class DatabaseTopologyOptions
{
    public const string SectionName = "DatabaseTopology";

    /// <summary>
    /// Reserved for a future reporting-only route. It is rejected today rather than allowing an
    /// accidental replica fallback for revocation- or scope-sensitive reads.
    /// </summary>
    public string? ReadReplicaConnectionString { get; set; }

    /// <summary>
    /// Reserved pending a per-request tenant/session-context contract and pool-reset proof.
    /// </summary>
    public bool EnableRowLevelSecurity { get; set; }
}

public sealed class DatabaseTopologyOptionsValidator : IValidateOptions<DatabaseTopologyOptions>
{
    public ValidateOptionsResult Validate(string? name, DatabaseTopologyOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var failures = new List<string>();

        if (!string.IsNullOrWhiteSpace(options.ReadReplicaConnectionString))
        {
            failures.Add(
                "DatabaseTopology:ReadReplicaConnectionString is not supported. The current DbContext includes permission, scope, and revocation-sensitive reads; replica lag/fallback must fail closed. A future replica requires a separate, explicitly stale-tolerant reporting route and its own authorization review.");
        }

        if (options.EnableRowLevelSecurity)
        {
            failures.Add(
                "DatabaseTopology:EnableRowLevelSecurity is not supported. RLS adoption requires a tenant/session-context contract, SET LOCAL lifecycle, and a proven pool reset so one request's scope cannot leak to another.");
        }

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }
}

/// <summary>Resolves both supported configuration aliases and validates without retaining secrets in DI.</summary>
public static class DatabaseTopologyConfiguration
{
    public static void Validate(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        DatabaseTopologyOptions options = configuration
            .GetSection(DatabaseTopologyOptions.SectionName)
            .Get<DatabaseTopologyOptions>() ?? new DatabaseTopologyOptions();

        // An empty explicit alias must not mask the conventional alias. The value remains a local
        // validation input only and is deliberately not registered in the service container.
        if (string.IsNullOrWhiteSpace(options.ReadReplicaConnectionString))
        {
            options.ReadReplicaConnectionString = configuration.GetConnectionString("ReadReplica");
        }

        var validator = new DatabaseTopologyOptionsValidator();
        ValidateOptionsResult result = validator.Validate(null, options);
        if (result.Failed)
        {
            throw new OptionsValidationException(
                DatabaseTopologyOptions.SectionName,
                typeof(DatabaseTopologyOptions),
                result.Failures);
        }
    }
}
