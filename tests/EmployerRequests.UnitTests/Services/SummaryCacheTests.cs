using EmployerRequests.Application.DTOs;
using EmployerRequests.Application.Services;
using Microsoft.Extensions.Caching.Memory;

namespace EmployerRequests.UnitTests.Services;

public class SummaryCacheTests
{
    // xUnit creates a new instance of this class for every test, so every test starts with an empty cache.
    private readonly SummaryCache _cache = new SummaryCache(new MemoryCache(new MemoryCacheOptions()));

    [Fact]
    public void A_summary_that_was_kept_is_served_from_the_cache()
    {
        Assert.Null(_cache.Get());

        var summary = Summary(1);
        _cache.Set(summary, _cache.GetVersion());

        Assert.Same(summary, _cache.Get());
    }

    [Fact]
    public void After_invalidation_the_summary_is_no_longer_cached()
    {
        _cache.Set(Summary(1), _cache.GetVersion());

        _cache.Invalidate();

        Assert.Null(_cache.Get());
    }

    [Fact]
    public void A_summary_calculated_while_a_status_change_was_saved_is_not_kept()
    {
        // A read remembers the version and starts calculating...
        var versionBeforeCalculation = _cache.GetVersion();

        // ...a status change is saved meanwhile...
        _cache.Invalidate();

        // ...and the calculation finishes with numbers from before the change.
        _cache.Set(Summary(1), versionBeforeCalculation);

        // The outdated numbers were not kept: the next read will calculate again.
        Assert.Null(_cache.Get());
    }

    private static RequestsSummaryDto Summary(int totalCount)
    {
        return new RequestsSummaryDto(
            totalCount,
            new List<StatusCountDto>(),
            new List<PriorityCountDto>(),
            new List<AssigneeWorkloadDto>(),
            DateTimeOffset.UtcNow);
    }
}
