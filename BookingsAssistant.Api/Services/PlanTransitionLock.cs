namespace BookingsAssistant.Api.Services;

/// <summary>
/// Serializes the "claim" step of Approve/Reject (read the plan's current status, then flip
/// AwaitingApproval -> Processing) across all plans, closing the TOCTOU window where two
/// near-simultaneous requests for the same plan id could both read Status == AwaitingApproval
/// before either writes back, and both go on to execute/reject the same plan.
///
/// Registered as a singleton (see Program.cs) so the same lock instance is shared across the
/// per-request scoped PlansController/ApplicationDbContext instances — mirrors the existing
/// <see cref="OsmRateLimitCooldown"/> pattern for state that must be shared across per-request
/// service resolution.
///
/// Deliberately a single process-wide lock, not one keyed per plan id: Approve/Reject are
/// human-triggered, low-frequency actions, so briefly serializing unrelated plans' claim steps
/// costs nothing in practice, and a per-id lock dictionary would add complexity (eviction,
/// growth) for no real benefit at this scale. The lock is only held for the fast claim
/// read+write, not for the (potentially slow) OSM execution that follows, so it never blocks
/// concurrent requests for other plans for long.
///
/// This only guards a single application instance — the deployed topology (see CLAUDE.md) is
/// one Home Assistant addon container, so that's sufficient. It would NOT prevent a
/// double-execution race across multiple horizontally-scaled instances sharing one database;
/// that would need a database-level atomic conditional update (e.g. EF Core's
/// ExecuteUpdateAsync), which was ruled out here because the EF Core InMemory provider used by
/// this project's test suite does not support it (throws at runtime), making it impossible to
/// exercise via the existing WebApplicationFactory-based integration tests.
/// </summary>
public class PlanTransitionLock
{
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    public async Task<IDisposable> AcquireAsync()
    {
        await _semaphore.WaitAsync();
        return new Releaser(_semaphore);
    }

    private sealed class Releaser : IDisposable
    {
        private readonly SemaphoreSlim _semaphore;
        private bool _released;

        public Releaser(SemaphoreSlim semaphore) => _semaphore = semaphore;

        public void Dispose()
        {
            if (_released) return;
            _released = true;
            _semaphore.Release();
        }
    }
}
