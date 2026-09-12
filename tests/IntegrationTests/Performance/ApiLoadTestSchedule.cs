namespace IntegrationTests.Performance;

/// <summary>
/// Defines how a load-test client starts work. Endpoint execution is deliberately outside this foundation.
/// </summary>
internal enum ApiLoadTestMode
{
    ClosedLoop,
    ConstantArrivalRate
}

/// <summary>
/// Logical workload groups only. They intentionally do not identify concrete endpoints or fixture IDs.
/// </summary>
internal enum ApiLoadTestScenario
{
    Login,
    ReadList,
    ReadDetail,
    Report,
    Post
}

internal sealed record ApiLoadTestScenarioWeight(ApiLoadTestScenario Scenario, int Weight);

internal sealed record ApiLoadTestConfiguration(
    int RunCount,
    IReadOnlyList<int> ClientConcurrencyLevels,
    IReadOnlyList<ApiLoadTestMode> Modes,
    TimeSpan WarmupDuration,
    TimeSpan MeasurementDuration,
    double? ConstantArrivalRatePerSecond,
    ulong Seed)
{
    internal const int DefaultRunCount = 3;
    internal const double DefaultConstantArrivalRatePerSecond = 100;
    internal static readonly TimeSpan DefaultWarmupDuration = TimeSpan.FromMinutes(1);
    internal static readonly TimeSpan DefaultMeasurementDuration = TimeSpan.FromMinutes(10);
    internal static readonly IReadOnlyList<int> DefaultClientConcurrencyLevels = [1, 10, 25, 50];
    internal static readonly IReadOnlyList<ApiLoadTestMode> DefaultModes =
        [ApiLoadTestMode.ClosedLoop, ApiLoadTestMode.ConstantArrivalRate];

    internal static ApiLoadTestConfiguration CreateDefault() => new(
        DefaultRunCount,
        DefaultClientConcurrencyLevels,
        DefaultModes,
        DefaultWarmupDuration,
        DefaultMeasurementDuration,
        DefaultConstantArrivalRatePerSecond,
        Seed: 0x5EED_2026);
}

internal sealed record ApiLoadTestPhase(ApiLoadTestPhaseKind Kind, TimeSpan Duration);

internal enum ApiLoadTestPhaseKind
{
    Warmup,
    Measurement
}

/// <summary>
/// Describes one full mixed-workload run. Measurement duration applies to the whole mix, not to each scenario.
/// </summary>
internal sealed class ApiLoadTestRunDefinition(
    int runNumber,
    int clientConcurrency,
    ApiLoadTestMode mode,
    double? arrivalRatePerSecond,
    ulong seed,
    ApiLoadTestPhase warmup,
    ApiLoadTestPhase measurement,
    IReadOnlyList<ApiLoadTestScenarioWeight>? scenarioMix = null)
{
    internal int RunNumber { get; } = runNumber;
    internal int ClientConcurrency { get; } = clientConcurrency;
    internal ApiLoadTestMode Mode { get; } = mode;
    internal double? ArrivalRatePerSecond { get; } = arrivalRatePerSecond;
    internal ulong Seed { get; } = seed;
    internal ApiLoadTestPhase Warmup { get; } = warmup;
    internal ApiLoadTestPhase Measurement { get; } = measurement;
    internal IReadOnlyList<ApiLoadTestScenarioWeight> ScenarioMix { get; } =
        ApiLoadTestScenarioMix.CreateValidatedSnapshot(scenarioMix ?? ApiLoadTestScenarioMix.Weights);

    internal ApiLoadTestScenario SelectScenario(ulong requestIndex) =>
        ApiLoadTestScenarioMix.Select(Seed, requestIndex, ScenarioMix);

    /// <summary>
    /// Lazily yields the deterministic scenario sequence. Executors stop enumeration at their phase boundary.
    /// </summary>
    internal IEnumerable<ApiLoadTestScenario> EnumerateScenarios()
    {
        for (ulong requestIndex = 0; requestIndex < ulong.MaxValue; requestIndex++)
        {
            yield return SelectScenario(requestIndex);
        }
    }
}

internal sealed class ApiLoadTestPlan(
    ApiLoadTestConfiguration configuration,
    IReadOnlyList<ApiLoadTestRunDefinition> runs)
{
    internal ApiLoadTestConfiguration Configuration { get; } = configuration;
    internal IReadOnlyList<ApiLoadTestRunDefinition> Runs { get; } = runs;

    internal static ApiLoadTestPlan Create(ApiLoadTestConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ApiLoadTestConfigurationValidator.ValidateAndThrow(configuration);

        var runs = new List<ApiLoadTestRunDefinition>(
            configuration.RunCount * configuration.ClientConcurrencyLevels.Count * configuration.Modes.Count);
        for (int runNumber = 1; runNumber <= configuration.RunCount; runNumber++)
        {
            foreach (ApiLoadTestMode mode in configuration.Modes)
            {
                foreach (int clientConcurrency in configuration.ClientConcurrencyLevels)
                {
                    runs.Add(new ApiLoadTestRunDefinition(
                        runNumber,
                        clientConcurrency,
                        mode,
                        mode == ApiLoadTestMode.ConstantArrivalRate
                            ? configuration.ConstantArrivalRatePerSecond
                            : null,
                        DeriveRunSeed(configuration.Seed, runNumber, clientConcurrency, mode),
                        new ApiLoadTestPhase(ApiLoadTestPhaseKind.Warmup, configuration.WarmupDuration),
                        new ApiLoadTestPhase(ApiLoadTestPhaseKind.Measurement, configuration.MeasurementDuration)));
                }
            }
        }

        return new ApiLoadTestPlan(configuration, runs);
    }

    private static ulong DeriveRunSeed(
        ulong seed,
        int runNumber,
        int clientConcurrency,
        ApiLoadTestMode mode)
    {
        ulong value = seed;
        value ^= (uint)runNumber * 0x9E37_79B9;
        value ^= (uint)clientConcurrency * 0x85EB_CA6B;
        value ^= (uint)mode * 0xC2B2_AE35;
        value += 0x9E37_79B9_7F4A_7C15;
        value = (value ^ (value >> 30)) * 0xBF58_476D_1CE4_E5B9;
        value = (value ^ (value >> 27)) * 0x94D0_49BB_1331_11EB;
        return value ^ (value >> 31);
    }
}

internal static class ApiLoadTestConfigurationValidator
{
    internal static readonly TimeSpan MinimumMeasurementDuration = TimeSpan.FromSeconds(1);
    internal static readonly TimeSpan MaximumMeasurementDuration = TimeSpan.FromMinutes(15);
    internal static readonly TimeSpan MinimumWarmupDuration = TimeSpan.FromSeconds(1);
    internal static readonly TimeSpan MaximumWarmupDuration = TimeSpan.FromMinutes(15);
    internal const int MaximumRunCount = 20;
    internal const int MaximumClientConcurrency = 256;
    internal const double MaximumConstantArrivalRatePerSecond = 10_000;

    internal static void ValidateAndThrow(ApiLoadTestConfiguration configuration)
    {
        IReadOnlyList<string> errors = Validate(configuration);
        if (errors.Count > 0)
        {
            throw new ArgumentException(string.Join(" ", errors), nameof(configuration));
        }
    }

    internal static IReadOnlyList<string> Validate(ApiLoadTestConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var errors = new List<string>();

        if (configuration.RunCount is < 1 or > MaximumRunCount)
        {
            errors.Add($"RunCount must be between 1 and {MaximumRunCount}.");
        }

        ValidateDuration(
            configuration.WarmupDuration,
            MinimumWarmupDuration,
            MaximumWarmupDuration,
            nameof(configuration.WarmupDuration),
            errors);
        ValidateDuration(
            configuration.MeasurementDuration,
            MinimumMeasurementDuration,
            MaximumMeasurementDuration,
            nameof(configuration.MeasurementDuration),
            errors);

        if (configuration.ClientConcurrencyLevels is null || configuration.ClientConcurrencyLevels.Count == 0)
        {
            errors.Add("At least one client concurrency level is required.");
        }
        else if (configuration.ClientConcurrencyLevels.Any(level => level is < 1 or > MaximumClientConcurrency) ||
                 configuration.ClientConcurrencyLevels.Distinct().Count() != configuration.ClientConcurrencyLevels.Count)
        {
            errors.Add($"Client concurrency levels must be unique and between 1 and {MaximumClientConcurrency}.");
        }

        if (configuration.Modes is null || configuration.Modes.Count == 0)
        {
            errors.Add("At least one load-test mode is required.");
        }
        else if (configuration.Modes.Any(mode => !Enum.IsDefined(mode)) ||
                 configuration.Modes.Distinct().Count() != configuration.Modes.Count)
        {
            errors.Add("Load-test modes must be known and unique.");
        }

        bool includesConstantArrivalRate = configuration.Modes?.Contains(ApiLoadTestMode.ConstantArrivalRate) == true;
        if (includesConstantArrivalRate &&
            (configuration.ConstantArrivalRatePerSecond is null ||
             double.IsNaN(configuration.ConstantArrivalRatePerSecond.Value) ||
             double.IsInfinity(configuration.ConstantArrivalRatePerSecond.Value) ||
             configuration.ConstantArrivalRatePerSecond.Value <= 0 ||
             configuration.ConstantArrivalRatePerSecond.Value > MaximumConstantArrivalRatePerSecond))
        {
            errors.Add(
                $"Constant arrival rate must be greater than zero and at most {MaximumConstantArrivalRatePerSecond} requests/second.");
        }

        return errors;
    }

    private static void ValidateDuration(
        TimeSpan value,
        TimeSpan minimum,
        TimeSpan maximum,
        string name,
        List<string> errors)
    {
        if (value < minimum || value > maximum)
        {
            errors.Add($"{name} must be between {minimum} and {maximum}.");
        }
    }
}

internal static class ApiLoadTestScenarioMix
{
    private static readonly int[] steps = [17, 23, 27, 33, 37, 43, 47, 53, 57, 63, 67, 73, 77, 83];
    private static readonly ApiLoadTestScenarioWeight[] weights =
    [
        new(ApiLoadTestScenario.Login, 10),
        new(ApiLoadTestScenario.ReadList, 45),
        new(ApiLoadTestScenario.ReadDetail, 25),
        new(ApiLoadTestScenario.Report, 15),
        new(ApiLoadTestScenario.Post, 5)
    ];

    internal static IReadOnlyList<ApiLoadTestScenarioWeight> Weights => weights;
    internal static readonly IReadOnlyList<ApiLoadTestScenarioWeight> SmokeWeights =
    [
        new(ApiLoadTestScenario.Login, 15),
        new(ApiLoadTestScenario.ReadList, 45),
        new(ApiLoadTestScenario.ReadDetail, 25),
        new(ApiLoadTestScenario.Report, 15)
    ];
    internal static int TotalWeight => 100;

    internal static ApiLoadTestScenario Select(ulong seed, ulong requestIndex)
        => Select(seed, requestIndex, weights);

    internal static ApiLoadTestScenario Select(
        ulong seed,
        ulong requestIndex,
        IReadOnlyList<ApiLoadTestScenarioWeight> scenarioMix)
    {
        ulong mixedSeed = MixSeed(seed);
        int offset = (int)(mixedSeed % (ulong)TotalWeight);
        int step = steps[(int)((mixedSeed >> 32) % (ulong)steps.Length)];
        int slot = (int)(((ulong)offset + requestIndex * (ulong)step) % (ulong)TotalWeight);
        int cumulativeWeight = 0;
        foreach (ApiLoadTestScenarioWeight weight in scenarioMix)
        {
            cumulativeWeight += weight.Weight;
            if (slot < cumulativeWeight)
            {
                return weight.Scenario;
            }
        }

        throw new InvalidOperationException("Scenario mix weights must cover every selection slot.");
    }

    internal static IReadOnlyList<ApiLoadTestScenarioWeight> CreateValidatedSnapshot(
        IReadOnlyList<ApiLoadTestScenarioWeight> scenarioMix)
    {
        ArgumentNullException.ThrowIfNull(scenarioMix);
        ApiLoadTestScenarioWeight[] snapshot = scenarioMix.ToArray();
        if (snapshot.Length == 0 || snapshot.Any(weight => weight.Weight <= 0) || snapshot.Sum(weight => weight.Weight) != TotalWeight)
        {
            throw new ArgumentException("Scenario mix must use positive weights totaling 100.", nameof(scenarioMix));
        }
        return Array.AsReadOnly(snapshot);
    }

    private static ulong MixSeed(ulong seed)
    {
        seed += 0x9E37_79B9_7F4A_7C15;
        seed = (seed ^ (seed >> 30)) * 0xBF58_476D_1CE4_E5B9;
        seed = (seed ^ (seed >> 27)) * 0x94D0_49BB_1331_11EB;
        return seed ^ (seed >> 31);
    }
}
