namespace IntegrationTests.Performance;

public sealed class ApiLoadTestScheduleTests
{
    [Fact]
    public void RunDefinition_ShouldRejectScenarioMixWhoseWeightTotalIsNot100()
    {
        Should.Throw<ArgumentException>(() => CreateRun([new(ApiLoadTestScenario.Login, 99)]));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void RunDefinition_ShouldRejectZeroOrNegativeScenarioWeights(int invalidWeight)
    {
        Should.Throw<ArgumentException>(() => CreateRun(
        [
            new(ApiLoadTestScenario.Login, invalidWeight),
            new(ApiLoadTestScenario.ReadList, 100 - invalidWeight)
        ]));
    }

    [Fact]
    public void RunDefinition_ShouldSnapshotScenarioMixBeforeTheSourceIsMutated()
    {
        var source = new List<ApiLoadTestScenarioWeight>
        {
            new(ApiLoadTestScenario.Login, 100)
        };
        ApiLoadTestRunDefinition run = CreateRun(source);
        source[0] = new ApiLoadTestScenarioWeight(ApiLoadTestScenario.Post, 100);

        run.ScenarioMix.Single().Scenario.ShouldBe(ApiLoadTestScenario.Login);
        run.SelectScenario(0).ShouldBe(ApiLoadTestScenario.Login);
    }

    [Fact]
    public void SmokeWeights_ShouldDistributeExactlyWithoutPost()
    {
        ApiLoadTestRunDefinition run = CreateRun(ApiLoadTestScenarioMix.SmokeWeights);
        var counts = Enumerable.Range(0, 100)
            .Select(index => run.SelectScenario((ulong)index))
            .GroupBy(scenario => scenario)
            .ToDictionary(group => group.Key, group => group.Count());

        counts[ApiLoadTestScenario.Login].ShouldBe(15);
        counts[ApiLoadTestScenario.ReadList].ShouldBe(45);
        counts[ApiLoadTestScenario.ReadDetail].ShouldBe(25);
        counts[ApiLoadTestScenario.Report].ShouldBe(15);
        counts.ContainsKey(ApiLoadTestScenario.Post).ShouldBeFalse();
    }

    [Fact]
    public void DefaultWeights_ShouldRemainTheFullMixedWorkload()
    {
        ApiLoadTestScenarioMix.Weights.ShouldBe(new[]
        {
            new ApiLoadTestScenarioWeight(ApiLoadTestScenario.Login, 10),
            new ApiLoadTestScenarioWeight(ApiLoadTestScenario.ReadList, 45),
            new ApiLoadTestScenarioWeight(ApiLoadTestScenario.ReadDetail, 25),
            new ApiLoadTestScenarioWeight(ApiLoadTestScenario.Report, 15),
            new ApiLoadTestScenarioWeight(ApiLoadTestScenario.Post, 5)
        });
    }

    [Fact]
    public void CreateDefault_ShouldProduceSeparateMixedWorkloadRunsForEveryModeAndConcurrency()
    {
        var configuration = ApiLoadTestConfiguration.CreateDefault();

        var plan = ApiLoadTestPlan.Create(configuration);

        plan.Runs.Count.ShouldBe(24);
        plan.Runs.ShouldAllBe(run =>
            run.Warmup.Kind.Equals(ApiLoadTestPhaseKind.Warmup) &&
            run.Warmup.Duration == TimeSpan.FromMinutes(1) &&
            run.Measurement.Kind.Equals(ApiLoadTestPhaseKind.Measurement) &&
            run.Measurement.Duration == TimeSpan.FromMinutes(10));
        plan.Runs.Count(run => run.Mode == ApiLoadTestMode.ClosedLoop).ShouldBe(12);
        plan.Runs.Count(run => run.Mode == ApiLoadTestMode.ConstantArrivalRate).ShouldBe(12);
        plan.Runs.Where(run => run.Mode == ApiLoadTestMode.ClosedLoop)
            .ShouldAllBe(run => !run.ArrivalRatePerSecond.HasValue);
        plan.Runs.Where(run => run.Mode == ApiLoadTestMode.ConstantArrivalRate)
            .ShouldAllBe(run =>
                run.ArrivalRatePerSecond.HasValue &&
                Math.Abs(run.ArrivalRatePerSecond.Value - 100) < double.Epsilon);
    }

    [Fact]
    public void SelectScenario_ShouldUseFixedWeightsForEachCompleteSelectionCycle()
    {
        var distribution = Enumerable.Range(0, ApiLoadTestScenarioMix.TotalWeight)
            .Select(index => ApiLoadTestScenarioMix.Select(42, (ulong)index))
            .GroupBy(scenario => scenario)
            .ToDictionary(group => group.Key, group => group.Count());

        distribution[ApiLoadTestScenario.Login].ShouldBe(10);
        distribution[ApiLoadTestScenario.ReadList].ShouldBe(45);
        distribution[ApiLoadTestScenario.ReadDetail].ShouldBe(25);
        distribution[ApiLoadTestScenario.Report].ShouldBe(15);
        distribution[ApiLoadTestScenario.Post].ShouldBe(5);
    }

    [Fact]
    public void Create_ShouldDeriveDeterministicRunSeedsAndScenarioSelections()
    {
        var configuration = ApiLoadTestConfiguration.CreateDefault();

        var first = ApiLoadTestPlan.Create(configuration);
        var second = ApiLoadTestPlan.Create(configuration);

        first.Runs.Select(run => run.Seed).ShouldBe(second.Runs.Select(run => run.Seed));
        first.Runs.Select(run => run.SelectScenario(17)).ShouldBe(second.Runs.Select(run => run.SelectScenario(17)));
        first.Runs.Select(run => run.Seed).Distinct().Count().ShouldBe(first.Runs.Count);
    }

    [Fact]
    public void SelectScenario_ShouldChangeTheInterleavedOrderForDifferentSeeds()
    {
        ApiLoadTestScenario[] first = Enumerable.Range(0, ApiLoadTestScenarioMix.TotalWeight)
            .Select(index => ApiLoadTestScenarioMix.Select(42, (ulong)index))
            .ToArray();
        ApiLoadTestScenario[] second = Enumerable.Range(0, ApiLoadTestScenarioMix.TotalWeight)
            .Select(index => ApiLoadTestScenarioMix.Select(43, (ulong)index))
            .ToArray();

        first.SequenceEqual(second).ShouldBeFalse();
    }

    [Fact]
    public void SelectScenario_ShouldAvoidLongContiguousScenarioBlocks()
    {
        foreach (ulong seed in Enumerable.Range(0, 100).Select(index => (ulong)index))
        {
            ApiLoadTestScenario[] cycle = Enumerable.Range(0, ApiLoadTestScenarioMix.TotalWeight)
                .Select(index => ApiLoadTestScenarioMix.Select(seed, (ulong)index))
                .ToArray();

            GetMaximumContiguousBlockLength(cycle).ShouldBeLessThanOrEqualTo(3);
        }
    }

    [Fact]
    public void EnumerateScenarios_ShouldRemainLazyAndNotDependOnMeasurementDuration()
    {
        ApiLoadTestRunDefinition run = ApiLoadTestPlan.Create(ApiLoadTestConfiguration.CreateDefault()).Runs[0];

        ApiLoadTestScenario[] selected = run.EnumerateScenarios().Take(100_000).ToArray();

        selected.Length.ShouldBe(100_000);
        selected[0].ShouldBe(run.SelectScenario(0));
        selected[^1].ShouldBe(run.SelectScenario(99_999));
    }

    [Theory]
    [InlineData(0, 1, 1, 100)]
    [InlineData(21, 1, 1, 100)]
    [InlineData(1, 0, 1, 100)]
    [InlineData(1, 257, 1, 100)]
    [InlineData(1, 1, 0, 100)]
    [InlineData(1, 1, 16, 100)]
    public void Validate_ShouldRejectDangerousBounds(
        int runCount,
        int concurrency,
        int measurementMinutes,
        double arrivalRate)
    {
        var configuration = new ApiLoadTestConfiguration(
            runCount,
            [concurrency],
            [ApiLoadTestMode.ClosedLoop],
            TimeSpan.FromMinutes(1),
            TimeSpan.FromMinutes(measurementMinutes),
            arrivalRate,
            Seed: 1);

        ApiLoadTestConfigurationValidator.Validate(configuration).ShouldNotBeEmpty();
        Should.Throw<ArgumentException>(() => ApiLoadTestPlan.Create(configuration));
    }

    [Fact]
    public void Validate_ShouldAllowClosedLoopOnlyConfigurationWithoutAnArrivalRate()
    {
        var configuration = new ApiLoadTestConfiguration(
            1,
            [1],
            [ApiLoadTestMode.ClosedLoop],
            TimeSpan.FromMinutes(1),
            TimeSpan.FromMinutes(1),
            ConstantArrivalRatePerSecond: null,
            Seed: 1);

        ApiLoadTestConfigurationValidator.Validate(configuration).ShouldBeEmpty();
        ApiLoadTestPlan.Create(configuration).Runs.Single().ArrivalRatePerSecond.ShouldBeNull();
    }

    [Theory]
    [MemberData(nameof(InvalidConstantArrivalRates))]
    public void Validate_ShouldRequireAValidArrivalRateWhenConstantArrivalModeIsIncluded(double? arrivalRate)
    {
        var configuration = new ApiLoadTestConfiguration(
            1,
            [1],
            [ApiLoadTestMode.ConstantArrivalRate],
            TimeSpan.FromMinutes(1),
            TimeSpan.FromMinutes(1),
            arrivalRate,
            Seed: 1);

        ApiLoadTestConfigurationValidator.Validate(configuration).ShouldNotBeEmpty();
        Should.Throw<ArgumentException>(() => ApiLoadTestPlan.Create(configuration));
    }

    [Fact]
    public void Validate_ShouldRejectWarmupSeparatelyFromMeasurementDuration()
    {
        ApiLoadTestConfiguration configuration = ApiLoadTestConfiguration.CreateDefault() with
        {
            WarmupDuration = TimeSpan.Zero,
            MeasurementDuration = TimeSpan.FromMinutes(1)
        };

        ApiLoadTestConfigurationValidator.Validate(configuration).ShouldNotBeEmpty();
        Should.Throw<ArgumentException>(() => ApiLoadTestPlan.Create(configuration));
    }

    [Fact]
    public void Validate_ShouldRejectEmptyOrDuplicateConfigurationCollections()
    {
        var empty = new ApiLoadTestConfiguration(
            1,
            [],
            [],
            TimeSpan.FromMinutes(1),
            TimeSpan.FromMinutes(1),
            null,
            Seed: 1);
        var duplicate = new ApiLoadTestConfiguration(
            1,
            [1, 1],
            [ApiLoadTestMode.ClosedLoop, ApiLoadTestMode.ClosedLoop],
            TimeSpan.FromMinutes(1),
            TimeSpan.FromMinutes(1),
            null,
            Seed: 1);

        ApiLoadTestConfigurationValidator.Validate(empty).ShouldNotBeEmpty();
        ApiLoadTestConfigurationValidator.Validate(duplicate).ShouldNotBeEmpty();
    }

    public static IEnumerable<object?[]> InvalidConstantArrivalRates =>
    [
        [null],
        [0d],
        [double.NaN],
        [ApiLoadTestConfigurationValidator.MaximumConstantArrivalRatePerSecond + 1]
    ];

    private static int GetMaximumContiguousBlockLength(IReadOnlyList<ApiLoadTestScenario> scenarios)
    {
        int maximum = 0;
        int current = 0;
        ApiLoadTestScenario? previous = null;
        foreach (ApiLoadTestScenario scenario in scenarios)
        {
            current = scenario == previous ? current + 1 : 1;
            maximum = Math.Max(maximum, current);
            previous = scenario;
        }

        return maximum;
    }

    private static ApiLoadTestRunDefinition CreateRun(IReadOnlyList<ApiLoadTestScenarioWeight> mix) => new(
        1, 1, ApiLoadTestMode.ClosedLoop, null, 42,
        new ApiLoadTestPhase(ApiLoadTestPhaseKind.Warmup, TimeSpan.FromSeconds(10)),
        new ApiLoadTestPhase(ApiLoadTestPhaseKind.Measurement, TimeSpan.FromMinutes(1)), mix);
}
