namespace IntegrationTests.Performance;

public sealed class WriteScaleBenchmarkConfigurationTests
{
    [Fact]
    public void FromEnvironment_RequiresExplicitGate_AndSecondOptInForScale()
    {
        Should.Throw<InvalidOperationException>(() => WriteScaleBenchmarkConfiguration.FromEnvironment(_ => null));
        Should.Throw<InvalidOperationException>(() => WriteScaleBenchmarkConfiguration.FromEnvironment(name => name switch
        {
            WriteScaleBenchmarkConfiguration.GateEnvironmentVariable => "1",
            "EIAMS_WRITE_SCALE_BENCHMARK_PROFILE" => "Scale",
            _ => null
        }));
    }

    [Fact]
    public void FromEnvironment_QuickIsSmall_AndScaleContainsOnlyBoundedScenarios()
    {
        var quick = WriteScaleBenchmarkConfiguration.FromEnvironment(name =>
            name == WriteScaleBenchmarkConfiguration.GateEnvironmentVariable ? "1" : null);
        quick.Profile.ShouldBe(WriteScaleBenchmarkProfile.QuickValidation);
        quick.Scenarios.ShouldBe([new WriteScaleScenario("one_line_one_inventory_key", 1, 1)]);

        var scale = WriteScaleBenchmarkConfiguration.FromEnvironment(name => name switch
        {
            WriteScaleBenchmarkConfiguration.GateEnvironmentVariable => "1",
            WriteScaleBenchmarkConfiguration.LongRunOptInEnvironmentVariable => "1",
            "EIAMS_WRITE_SCALE_BENCHMARK_PROFILE" => "Scale",
            _ => null
        });
        scale.Scenarios.ShouldContain(s => s.LineCount == 100);
        scale.Scenarios.ShouldContain(s => s.LineCount == 1_000);
        scale.Scenarios.ShouldAllBe(s => s.LineCount <= WriteScaleBenchmarkConfiguration.MaximumLinesPerScenario &&
                                      s.InventoryKeyCount <= WriteScaleBenchmarkConfiguration.MaximumInventoryKeysPerScenario);
    }

    [Fact]
    public void ValidateScenarios_RejectsUnboundedOrImpossibleInput()
    {
        Should.Throw<ArgumentException>(() => WriteScaleBenchmarkConfiguration.ValidateScenarios(
            [new WriteScaleScenario("invalid", 1_001, 1)]));
        Should.Throw<ArgumentException>(() => WriteScaleBenchmarkConfiguration.ValidateScenarios(
            [new WriteScaleScenario("invalid", 10, 11)]));
    }
}
