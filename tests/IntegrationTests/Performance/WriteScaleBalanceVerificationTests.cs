namespace IntegrationTests.Performance;

public sealed class WriteScaleBalanceVerificationTests
{
    [Fact]
    public void HasExactPostingEffects_AcceptsExactPerKeyDeltas()
    {
        var first = new WriteScaleInventoryKey(Guid.NewGuid(), Guid.NewGuid());
        var second = new WriteScaleInventoryKey(first.WarehouseId, Guid.NewGuid());
        IReadOnlyDictionary<WriteScaleInventoryKey, decimal> baseline = new Dictionary<WriteScaleInventoryKey, decimal>
        {
            [first] = 25m,
            [second] = 10m
        };
        IReadOnlyDictionary<WriteScaleInventoryKey, decimal> final = new Dictionary<WriteScaleInventoryKey, decimal>
        {
            [first] = 28m,
            [second] = 17m
        };
        IReadOnlyDictionary<WriteScaleInventoryKey, decimal> deltas = new Dictionary<WriteScaleInventoryKey, decimal>
        {
            [first] = 3m,
            [second] = 7m
        };

        WriteScaleBalanceVerification.HasExactPostingEffects(baseline, final, deltas, 2).ShouldBeTrue();
    }

    [Fact]
    public void HasExactPostingEffects_RejectsUnchangedBalanceEvenWhenItAlreadyExceedsDelta()
    {
        var key = new WriteScaleInventoryKey(Guid.NewGuid(), Guid.NewGuid());
        IReadOnlyDictionary<WriteScaleInventoryKey, decimal> baseline = new Dictionary<WriteScaleInventoryKey, decimal> { [key] = 100m };
        IReadOnlyDictionary<WriteScaleInventoryKey, decimal> final = new Dictionary<WriteScaleInventoryKey, decimal> { [key] = 100m };
        IReadOnlyDictionary<WriteScaleInventoryKey, decimal> deltas = new Dictionary<WriteScaleInventoryKey, decimal> { [key] = 1m };

        WriteScaleBalanceVerification.HasExactPostingEffects(baseline, final, deltas, 1).ShouldBeFalse();
    }
}
