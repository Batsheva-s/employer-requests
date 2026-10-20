using EmployerRequests.Application.DTOs;
using Microsoft.Extensions.Caching.Memory;

namespace EmployerRequests.Application.Services;

/// <summary>
/// Keeps the summary of the unfiltered view in the server's memory, shared by all users.
/// <list type="bullet">
/// <item><b>Expiration:</b> 60 seconds at most.</item>
/// <item><b>Invalidation:</b> every saved status change calls <see cref="Invalidate"/>, so the next read is fresh.</item>
/// <item><b>Race:</b> a summary that was being calculated while a status change was saved may already be outdated.
/// It is returned to its caller but not kept – otherwise it would be served for a whole minute.</item>
/// </list>
/// Registered as a singleton: the cached value and the version counter belong to the whole application,
/// not to one HTTP request.
/// </summary>
public class SummaryCache
{
    public static readonly TimeSpan Expiration = TimeSpan.FromSeconds(60);

    private const string CacheKey = "requests-summary";

    private readonly IMemoryCache _cache;
    private readonly object _lock = new();

    // Goes up by one on every invalidation. Read and written only inside the lock.
    private long _version;

    public SummaryCache(IMemoryCache cache)
    {
        _cache = cache;
    }

    /// <summary>Returns the cached summary, or calculates it with <paramref name="calculate"/> and keeps it.</summary>
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
            // Keep the result only if no status change was saved while it was being calculated.
            if (_version == versionBeforeCalculation)
            {
                _cache.Set(CacheKey, summary, Expiration);
            }
        }

        return summary;
    }

    /// <summary>Removes the cached summary. Called after every saved status change.</summary>
    public void Invalidate()
    {
        lock (_lock)
        {
            _version++;
            _cache.Remove(CacheKey);
        }
    }
}
