using EmployerRequests.Application.Abstractions;
using EmployerRequests.Application.Contracts;
using EmployerRequests.Domain;

namespace EmployerRequests.Application.Services;

public class RequestSummaryService : IRequestSummaryService
{
    private const int TopAssigneesCount = 5;

    private readonly IRequestRepository _repository;
    private readonly ISummaryCache _cache;
    private readonly TimeProvider _timeProvider;

    public RequestSummaryService(IRequestRepository repository, ISummaryCache cache, TimeProvider timeProvider)
    {
        _repository = repository;
        _cache = cache;
        _timeProvider = timeProvider;
    }

    public Task<RequestsSummaryDto> GetSummaryAsync(RequestFilter filter, CancellationToken cancellationToken)
    {
        // The unfiltered summary is the first screen every user sees, the same for everyone, and the most
        // expensive one (it reads all the requests) – so it is cached. Filtered summaries read fewer rows,
        // use the indexes and come in endless combinations – so they are calculated on every call.
        return filter.HasFilters()
            ? CalculateAsync(filter, cancellationToken)
            : _cache.GetOrCalculateAsync(token => CalculateAsync(filter, token), cancellationToken);
    }

    private async Task<RequestsSummaryDto> CalculateAsync(RequestFilter filter, CancellationToken cancellationToken)
    {
        // One GROUP BY returns at most 12 rows (4 statuses × 3 priorities); every count below is a sum over them.
        var counts = await _repository.CountByStatusAndPriorityAsync(filter, cancellationToken);
        var topAssignees = await _repository.GetTopAssigneesAsync(filter, TopAssigneesCount, cancellationToken);

        var byStatus = Enum.GetValues<RequestStatus>()
            .Select(status => new StatusCountDto(status, counts.Where(c => c.Status == status).Sum(c => c.Count)))
            .ToList();

        var byPriority = Enum.GetValues<RequestPriority>()
            .Select(priority => new PriorityCountDto(priority, counts.Where(c => c.Priority == priority).Sum(c => c.Count)))
            .ToList();

        return new RequestsSummaryDto(
            TotalCount: counts.Sum(c => c.Count),
            ByStatus: byStatus,
            ByPriority: byPriority,
            TopAssignees: topAssignees,
            CalculatedAt: _timeProvider.GetUtcNow());
    }
}
