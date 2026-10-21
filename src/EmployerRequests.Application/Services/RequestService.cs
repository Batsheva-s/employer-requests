using EmployerRequests.Application.DTOs;
using EmployerRequests.Application.Exceptions;
using EmployerRequests.Application.Interfaces;
using EmployerRequests.Application.Mapping;
using EmployerRequests.Domain;
using Microsoft.Extensions.Logging;

namespace EmployerRequests.Application.Services;

/// <summary>
/// The business logic of requests: reading them, the summary, and status changes.
/// Status changes are safe while many users work at the same time (optimistic concurrency): a change is saved
/// only if the request is still in the version the user saw. Otherwise nothing is saved and the user gets a
/// conflict – one user can never silently overwrite another (no lost update).
/// </summary>
public class RequestService : IRequestService
{
    private const int TopAssigneesCount = 5;

    private readonly IRequestRepository _repository;
    private readonly SummaryCache _summaryCache;
    private readonly ILogger<RequestService> _logger;

    public RequestService(IRequestRepository repository, SummaryCache summaryCache, ILogger<RequestService> logger)
    {
        _repository = repository;
        _summaryCache = summaryCache;
        _logger = logger;
    }

    // ---------- Reading ----------

    public async Task<PagedResultDto<RequestListItemDto>> SearchAsync(RequestSearchDto search, CancellationToken cancellationToken)
    {
        // Filtering, sorting and paging all run in the database.
        return await _repository.SearchAsync(search, cancellationToken);
    }

    public async Task<RequestDetailsDto> GetByIdAsync(int id, CancellationToken cancellationToken)
    {
        var request = await _repository.GetByIdAsync(id, cancellationToken);
        if (request == null)
        {
            throw new NotFoundException(id);
        }

        return RequestMapper.ToDetails(request);
    }

    public async Task<IReadOnlyList<RequestStatusChangeDto>> GetHistoryAsync(int id, CancellationToken cancellationToken)
    {
        // An empty history is a valid answer for an existing request, so existence is checked on its own.
        var exists = await _repository.ExistsAsync(id, cancellationToken);
        if (!exists)
        {
            throw new NotFoundException(id);
        }

        return await _repository.GetHistoryAsync(id, cancellationToken);
    }

    public async Task<RequestsSummaryDto> GetSummaryAsync(RequestFilterDto filter, CancellationToken cancellationToken)
    {
        // Filtered summaries come in endless combinations, so the same one is rarely asked for again:
        // they are calculated on every call. (Most of them use an index; free-text search reads the whole table –
        // docs/PERFORMANCE.md.)
        if (filter.HasFilters())
        {
            return await CalculateSummaryAsync(filter, cancellationToken);
        }

        // The unfiltered summary is the first screen every user sees, it is the same for everyone, and it reads
        // all the requests (two GROUP BY queries): it is cached.
        var cached = _summaryCache.Get();
        if (cached != null)
        {
            return cached;
        }

        // Remember the version before calculating: if a status change is saved meanwhile, the result is not kept.
        var versionBeforeCalculation = _summaryCache.GetVersion();
        var summary = await CalculateSummaryAsync(filter, cancellationToken);
        _summaryCache.Set(summary, versionBeforeCalculation);

        return summary;
    }

    // ---------- Changing the status ----------

    public async Task<RequestDetailsDto> ChangeStatusAsync(int id, ChangeStatusDto change, string changedBy, CancellationToken cancellationToken)
    {
        var request = await ApplyStatusChangeAsync(id, change.Status, change.RowVersion, changedBy, cancellationToken);
        return RequestMapper.ToDetails(request);
    }

    /// <summary>
    /// Partial success: every request is changed on its own, with exactly the same checks as a single change,
    /// and gets its own outcome. A request that someone else changed meanwhile does not block the others –
    /// and is still never overwritten.
    /// </summary>
    public async Task<BulkChangeStatusResultDto> ChangeStatusBulkAsync(BulkChangeStatusDto bulkChange, string changedBy, CancellationToken cancellationToken)
    {
        var results = new List<BulkItemResultDto>();
        var succeeded = 0;

        foreach (var item in bulkChange.Items)
        {
            var result = await ChangeOneOfManyAsync(item, bulkChange.Status, changedBy, cancellationToken);
            results.Add(result);

            if (result.Outcome == BulkItemOutcome.Updated)
            {
                succeeded++;
            }
        }

        var failed = results.Count - succeeded;
        _logger.LogInformation(
            "Bulk status change to {NewStatus}: {Succeeded} of {Requested} requests updated",
            bulkChange.Status, succeeded, results.Count);

        return new BulkChangeStatusResultDto
        {
            Requested = results.Count,
            Succeeded = succeeded,
            Failed = failed,
            Items = results
        };
    }

    /// <summary>One request of a bulk change: the reason it was not updated, if any, becomes its outcome.</summary>
    private async Task<BulkItemResultDto> ChangeOneOfManyAsync(
        BulkChangeStatusItemDto item,
        RequestStatus newStatus,
        string changedBy,
        CancellationToken cancellationToken)
    {
        try
        {
            var request = await ApplyStatusChangeAsync(item.Id, newStatus, item.RowVersion, changedBy, cancellationToken);
            return new BulkItemResultDto
            {
                Id = item.Id,
                Outcome = BulkItemOutcome.Updated,
                RowVersion = request.RowVersion
            };
        }
        catch (NotFoundException)
        {
            return NotUpdated(item.Id, BulkItemOutcome.NotFound, "The request does not exist.");
        }
        catch (ConcurrencyConflictException)
        {
            return NotUpdated(item.Id, BulkItemOutcome.Conflict, "The request was changed by another user.");
        }
        catch (InvalidStatusTransitionException exception)
        {
            return NotUpdated(item.Id, BulkItemOutcome.InvalidTransition, exception.Message);
        }
    }

    private BulkItemResultDto NotUpdated(int requestId, BulkItemOutcome outcome, string message)
    {
        // The same level as a single change that fails this way (GlobalExceptionHandler logs those as warnings).
        _logger.LogWarning("Bulk change: request {RequestId} was not updated ({Outcome})", requestId, outcome);
        return new BulkItemResultDto
        {
            Id = requestId,
            Outcome = outcome,
            Message = message
        };
    }

    /// <summary>
    /// The heart of every status change, single or bulk:
    /// 1. load the request;
    /// 2. make sure the user saw its current version;
    /// 3. apply the workflow rule – the entity records the change in its history;
    /// 4. save – the database itself rejects the save if someone else saved after step 1.
    /// </summary>
    private async Task<ServiceRequest> ApplyStatusChangeAsync(
        int id,
        RequestStatus newStatus,
        byte[] versionSeenByUser,
        string changedBy,
        CancellationToken cancellationToken)
    {
        // 1. Load.
        var request = await _repository.GetForUpdateAsync(id, cancellationToken);
        if (request == null)
        {
            throw new NotFoundException(id);
        }

        // 2. The user decided based on an older version: someone else has changed the request since.
        //    (SequenceEqual compares the two byte arrays value by value.)
        if (!request.RowVersion.SequenceEqual(versionSeenByUser))
        {
            throw new ConcurrencyConflictException(id);
        }

        // 3. The entity checks the workflow and adds the history row.
        var previousStatus = request.Status;
        request.ChangeStatus(newStatus, changedBy, DateTimeOffset.UtcNow);

        // 4. EF Core sends "UPDATE ... WHERE Id = @id AND RowVersion = @versionLoadedInStep1", together with the
        //    INSERT of the history row, in one transaction. If another user saved between step 1 and now, no row
        //    matches, nothing is saved, and the repository throws ConcurrencyConflictException.
        await _repository.SaveChangesAsync(cancellationToken);

        // Saved: the summary counts requests by status, so the cached numbers are outdated from this moment.
        // Cleared here, right after each save, so that nothing that happens later – the next request of a bulk
        // change failing, or the client going away – can leave an outdated summary behind.
        _summaryCache.Invalidate();

        _logger.LogInformation(
            "Request {RequestId} status changed from {PreviousStatus} to {NewStatus}",
            id, previousStatus, newStatus);

        return request;
    }

    // ---------- Summary ----------

    private async Task<RequestsSummaryDto> CalculateSummaryAsync(RequestFilterDto filter, CancellationToken cancellationToken)
    {
        // One GROUP BY returns at most 12 rows (4 statuses × 3 priorities); every count below is a sum over them.
        var counts = await _repository.CountByStatusAndPriorityAsync(filter, cancellationToken);
        var topAssignees = await _repository.GetTopAssigneesAsync(filter, TopAssigneesCount, cancellationToken);

        // Every status appears, with zero when there are no requests in it.
        var byStatus = new List<StatusCountDto>();
        foreach (var status in Enum.GetValues<RequestStatus>())
        {
            var countForStatus = counts.Where(count => count.Status == status).Sum(count => count.Count);
            byStatus.Add(new StatusCountDto { Status = status, Count = countForStatus });
        }

        // The same for every priority.
        var byPriority = new List<PriorityCountDto>();
        foreach (var priority in Enum.GetValues<RequestPriority>())
        {
            var countForPriority = counts.Where(count => count.Priority == priority).Sum(count => count.Count);
            byPriority.Add(new PriorityCountDto { Priority = priority, Count = countForPriority });
        }

        var totalCount = counts.Sum(count => count.Count);

        return new RequestsSummaryDto
        {
            TotalCount = totalCount,
            ByStatus = byStatus,
            ByPriority = byPriority,
            TopAssignees = topAssignees,
            CalculatedAt = DateTimeOffset.UtcNow
        };
    }
}
