using EmployerRequests.Application.DTOs;
using Microsoft.Extensions.Caching.Memory;

namespace EmployerRequests.Application.Services;

/// <summary>
/// Keeps the summary of the unfiltered view in the server's memory, shared by all users.
/// <list type="bullet">
/// <item><b>Expiration:</b> 60 seconds at most.</item>
/// <item><b>Invalidation:</b> every saved status change calls <see cref="Invalidate"/>, so the next read is fresh.</item>
/// <item><b>Race:</b> a summary that was being calculated while a status change was saved may already be outdated,
/// so <see cref="Set"/> does not keep it – otherwise it would be served for a whole minute.</item>
/// </list>
/// Registered as a singleton: the cached value and the version belong to the whole application,
/// not to one HTTP request.
/// </summary>
public class SummaryCache
{
    public static readonly TimeSpan Expiration = TimeSpan.FromSeconds(60);

    private const string CacheKey = "requests-summary";

    private readonly IMemoryCache _cache;

    // Many requests run at the same time, so _version is read and written only inside lock (_lock).
    private readonly object _lock = new object();

    // Goes up by one on every invalidation.
    private long _version;

    public SummaryCache(IMemoryCache cache)
    {
        _cache = cache;
    }

    /// <summary>The cached summary, or null if there is none.</summary>
    public RequestsSummaryDto? Get()
    {
        if (_cache.TryGetValue(CacheKey, out RequestsSummaryDto? summary))
        {
            return summary;
        }

        return null;
    }

    /// <summary>The current version. Read it before calculating a summary, and pass it to <see cref="Set"/>.</summary>
    public long GetVersion()
    {
        lock (_lock)
        {
            return _version;
        }
    }

    /// <summary>
    /// Keeps <paramref name="summary"/> – but only if no status change was saved since
    /// <paramref name="versionBeforeCalculation"/> was read.
    /// </summary>
    public void Set(RequestsSummaryDto summary, long versionBeforeCalculation)
    {
        lock (_lock)
        {
            if (_version == versionBeforeCalculation)
            {
                _cache.Set(CacheKey, summary, Expiration);
            }
        }
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
