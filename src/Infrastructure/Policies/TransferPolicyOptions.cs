namespace Infrastructure.Policies;

/// <summary>Configuration for geographic transfer restrictions.</summary>
public sealed class TransferPolicyOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "Policies:Transfers";

    /// <summary>Gets whether transfers across governorate codes are rejected.</summary>
    public bool BlockCrossGovernorate { get; init; }

    /// <summary>Gets whether missing governorate codes reject a transfer when blocking is enabled.</summary>
    public bool RequireGovernorateCodeWhenEnabled { get; init; } = true;
}
