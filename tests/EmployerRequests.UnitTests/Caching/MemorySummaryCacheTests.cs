using EmployerRequests.Application.Contracts;
using EmployerRequests.Infrastructure.Caching;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace EmployerRequests.UnitTests.Caching;

public class MemorySummaryCacheTests
{
    [Fact]
    public async Task The_second_read_is_served_from_the_cache()
    {
        var cache = CreateCache();
        var calculations = 0;

        var first = await cache.GetOrCalculateAsync(_ => Task.FromResult(Summary(++calculations)), CancellationToken.None);
        var second = await cache.GetOrCalculateAsync(_ => Task.FromResult(Summary(++calculations)), CancellationToken.None);

        Assert.Equal(1, calculations);
        Assert.Same(first, second);
    }

    [Fact]
    public async Task After_invalidation_the_summary_is_calculated_again()
    {
        var cache = CreateCache();
        await cache.GetOrCalculateAsync(_ => Task.FromResult(Summary(1)), CancellationToken.None);

        cache.Invalidate();
        var afterInvalidation = await cache.GetOrCalculateAsync(_ => Task.FromResult(Summary(2)), CancellationToken.None);

        Assert.Equal(2, afterInvalidation.TotalCount);
    }

    [Fact]
    public async Task A_summary_calculated_while_a_status_change_was_saved_is_not_kept()
    {
        var cache = CreateCache();
        var calculationStarted = new TaskCompletionSource();
        var calculationResult = new TaskCompletionSource<RequestsSummaryDto>();

        // A read starts calculating and is still running...
        var slowRead = cache.GetOrCalculateAsync(_ =>
        {
            calculationStarted.SetResult();
            return calculationResult.Task;
        }, CancellationToken.None);
        await calculationStarted.Task;

        // ...when a status change is saved. Then the calculation finishes, with numbers from before the change.
        cache.Invalidate();
        calculationResult.SetResult(Summary(1));
        await slowRead;

        // The outdated numbers were not stored: the next read calculates again.
        var calculatedAgain = false;
        await cache.GetOrCalculateAsync(_ =>
        {
            calculatedAgain = true;
            return Task.FromResult(Summary(2));
        }, CancellationToken.None);

        Assert.True(calculatedAgain);
    }

    private static MemorySummaryCache CreateCache() =>
        new(
            new MemoryCache(new MemoryCacheOptions()),
            Options.Create(new SummaryCacheOptions { ExpirationSeconds = 60 }),
            NullLogger<MemorySummaryCache>.Instance);

    private static RequestsSummaryDto Summary(int totalCount) =>
        new(totalCount, [], [], [], DateTimeOffset.UtcNow);
}
