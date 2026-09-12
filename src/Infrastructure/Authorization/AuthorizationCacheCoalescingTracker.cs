namespace Infrastructure.Authorization;

internal sealed class AuthorizationCacheCoalescingTracker
{
    private readonly Dictionary<string, LookupState> activeLookups = new(StringComparer.Ordinal);
    private readonly Lock synchronizationLock = new();

    internal CacheLookup Begin(string cacheKey, string keyType)
    {
        lock (synchronizationLock)
        {
            if (!activeLookups.TryGetValue(cacheKey, out LookupState? state))
            {
                state = new LookupState();
                activeLookups.Add(cacheKey, state);
            }

            state.ParticipantCount++;
            return new CacheLookup(cacheKey, keyType, state);
        }
    }

    internal void RecordFactoryExecution(CacheLookup lookup)
    {
        lock (synchronizationLock)
        {
            lookup.State.FactoryInvoked = true;
            lookup.FactoryInvoked = true;
        }

        AuthorizationCacheMetrics.RecordFactoryExecution(lookup.KeyType);
    }

    internal void Complete(CacheLookup lookup)
    {
        CacheLookupOutcome outcome;

        lock (synchronizationLock)
        {
            if (lookup.Completed)
            {
                throw new InvalidOperationException("A cache lookup can only be completed once.");
            }

            lookup.Completed = true;
            outcome = CacheLookupOutcome.Hit;
            if (lookup.FactoryInvoked)
            {
                outcome = CacheLookupOutcome.FactoryExecuted;
            }
            else if (lookup.State.FactoryInvoked)
            {
                outcome = CacheLookupOutcome.Coalesced;
            }

            lookup.State.ParticipantCount--;
            if (lookup.State.ParticipantCount == 0)
            {
                activeLookups.Remove(lookup.CacheKey);
            }
        }

        if (outcome == CacheLookupOutcome.Coalesced)
        {
            AuthorizationCacheMetrics.RecordCoalesced(lookup.KeyType);
        }
        else if (outcome == CacheLookupOutcome.Hit)
        {
            AuthorizationCacheMetrics.RecordHit(lookup.KeyType);
        }
    }

    internal sealed class CacheLookup(string cacheKey, string keyType, LookupState state)
    {
        internal string CacheKey { get; } = cacheKey;

        internal string KeyType { get; } = keyType;

        internal LookupState State { get; } = state;

        internal bool FactoryInvoked { get; set; }

        internal bool Completed { get; set; }
    }

    internal sealed class LookupState
    {
        internal int ParticipantCount { get; set; }

        internal bool FactoryInvoked { get; set; }
    }

    private enum CacheLookupOutcome
    {
        FactoryExecuted,
        Coalesced,
        Hit
    }
}
