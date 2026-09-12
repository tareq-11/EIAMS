using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;

namespace Application.UnitTests.Performance;

public sealed class HybridCacheCoalescingTests
{
    [Fact]
    public async Task GetOrCreateAsync_ShouldExecuteOneFactoryForConcurrentRequestsForTheSameKey()
    {
        // Arrange
        HybridCache cache = CreateCache();
        const string cacheKey = "coalescing-test";
        int factoryExecutions = 0;
        var factoryStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseFactory = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        Task<int> firstRequest = GetOrCreateAsync(
            cache,
            cacheKey,
            async _ =>
            {
                Interlocked.Increment(ref factoryExecutions);
                factoryStarted.SetResult();
                await releaseFactory.Task;
                return 42;
            });

        await factoryStarted.Task;
        Task<int>[] concurrentRequests = Enumerable.Range(0, 16)
            .Select(_ => GetOrCreateAsync(
                cache,
                cacheKey,
                _ =>
                {
                    Interlocked.Increment(ref factoryExecutions);
                    return Task.FromResult(7);
                }))
            .ToArray();

        // Act
        releaseFactory.SetResult();
        int[] values = await Task.WhenAll(concurrentRequests.Append(firstRequest));

        // Assert
        factoryExecutions.ShouldBe(1);
        values.ShouldAllBe(value => value == 42);
    }

    private static HybridCache CreateCache()
    {
        var services = new ServiceCollection();

#pragma warning disable EXTEXP0018
        services.AddHybridCache();
#pragma warning restore EXTEXP0018

        return services.BuildServiceProvider().GetRequiredService<HybridCache>();
    }

    private static async Task<int> GetOrCreateAsync(
        HybridCache cache,
        string cacheKey,
        Func<CancellationToken, Task<int>> factory) =>
        await cache.GetOrCreateAsync(cacheKey, cancellationToken => new ValueTask<int>(factory(cancellationToken)));
}
