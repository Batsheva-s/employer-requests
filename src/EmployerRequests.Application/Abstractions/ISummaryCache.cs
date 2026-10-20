using EmployerRequests.Application.Contracts;

namespace EmployerRequests.Application.Abstractions;

/// <summary>Cache for the summary of the unfiltered view. Implemented in the infrastructure layer.</summary>
public interface ISummaryCache
{
    /// <summary>Returns the cached summary, or calculates it with <paramref name="calculate"/> and caches it.</summary>
    Task<RequestsSummaryDto> GetOrCalculateAsync(
        Func<CancellationToken, Task<RequestsSummaryDto>> calculate,
        CancellationToken cancellationToken);

    /// <summary>Called after every saved status change, so the next read calculates fresh numbers.</summary>
    void Invalidate();
}
