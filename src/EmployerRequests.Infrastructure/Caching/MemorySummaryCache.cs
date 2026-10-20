using EmployerRequests.Application.Abstractions;
using EmployerRequests.Application.Contracts;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EmployerRequests.Infrastructure.Caching;

/// <summary>
/// Keeps the summary of the unfiltered view in the server's memory.
/// <list type="bullet">
/// <item><b>Expiration:</b> absolute, from configuration (60 seconds by default).</item>
/// <item><b>Invalidation:</b> every saved status change calls <see cref="Invalidate"/>, so the next read is fresh.</item>
/// <item><b>Race:</b> a summary calculated while a status change was being saved may already be outdated.
/// It is returned to its caller but not stored – otherwise it would be served for a whole minute.</item>
/// </list>
/// Registered as a singleton: the cached value and the version counter are shared by all requests.
/// </summary>
public class MemorySummaryCache : ISummaryCache
{
    private const string CacheKey = "requests-summary";

    private readonly IMemoryCache _cache;
    private readonly TimeSpan _expiration;
    private readonly ILogger<MemorySummaryCache> _logger;

    private readonly Lock _lock = new();

    // Goes up by one on every invalidation. Read and written only inside the lock.
    private long _version;

    public MemorySummaryCache(IMemoryCache cache, IOptions<SummaryCacheOptions> options, ILogger<MemorySummaryCache> logger)
    {
        _cache = cache;
        _expiration = TimeSpan.FromSeconds(options.Value.ExpirationSeconds);
        _logger = logger;
    }

    public async Task<RequestsSummaryDto> GetOrCalculateAsync(
        Func<CancellationToken, Task<RequestsSummaryDto>> calculate,
        CancellationToken cancellationToken)
    {
        if (_cache.TryGetValue(CacheKey, out RequestsSummaryDto? cached) && cached is not null)
        {
            return cached;
        }

        long versionBeforeCalculation;
        lock (_lock)
        {
            versionBeforeCalculation = _version;
        }

        var summary = await calculate(cancellationToken);

        lock (_lock)
        {
            // Store the result only if no status change was saved while it was being calculated.
            if (_version == versionBeforeCalculation)
            {
                _cache.Set(CacheKey, summary, _expiration);
            }
        }

        return summary;
    }

    public void Invalidate()
    {
        lock (_lock)
        {
            _version++;
            _cache.Remove(CacheKey);
        }

        _logger.LogDebug("Summary cache invalidated");
    }
}
