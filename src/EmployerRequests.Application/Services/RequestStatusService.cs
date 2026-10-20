using EmployerRequests.Application.Abstractions;
using EmployerRequests.Application.Contracts;
using EmployerRequests.Application.Exceptions;
using EmployerRequests.Domain;
using Microsoft.Extensions.Logging;

namespace EmployerRequests.Application.Services;

/// <summary>
/// Changes request statuses safely while many users work at the same time (optimistic concurrency):
/// a change is saved only if the request is still in the version the user saw. Otherwise nothing is
/// saved and the user gets a conflict – one user can never silently overwrite another (no lost update).
/// </summary>
public class RequestStatusService : IRequestStatusService
{
    private readonly IRequestRepository _repository;
    private readonly ISummaryCache _summaryCache;
    private readonly ICurrentUser _currentUser;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<RequestStatusService> _logger;

    public RequestStatusService(
        IRequestRepository repository,
        ISummaryCache summaryCache,
        ICurrentUser currentUser,
        TimeProvider timeProvider,
        ILogger<RequestStatusService> logger)
    {
        _repository = repository;
        _summaryCache = summaryCache;
        _currentUser = currentUser;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task<RequestDetailsDto> ChangeStatusAsync(int id, ChangeStatusCommand command, CancellationToken cancellationToken)
    {
        var request = await ApplyStatusChangeAsync(id, command.Status, command.RowVersion, cancellationToken);
        return request.ToDetailsDto();
    }

    /// <summary>
    /// Partial success: every request is changed on its own, with exactly the same checks as a single
    /// change, and gets its own outcome. A request that someone else changed meanwhile does not block
    /// the others – and is still never overwritten.
    /// </summary>
    public async Task<BulkChangeStatusResult> ChangeStatusBulkAsync(BulkChangeStatusCommand command, CancellationToken cancellationToken)
    {
        var results = new List<BulkItemResult>(command.Items.Count);

        foreach (var item in command.Items)
        {
            results.Add(await ChangeOneOfManyAsync(item, command.Status, cancellationToken));
        }

        var succeeded = results.Count(result => result.Outcome == BulkItemOutcome.Updated);
        _logger.LogInformation(
            "Bulk status change to {NewStatus}: {Succeeded} of {Requested} requests updated",
            command.Status, succeeded, results.Count);

        return new BulkChangeStatusResult(results.Count, succeeded, results.Count - succeeded, results);
    }

    /// <summary>Runs one item of a bulk change and turns the reason it failed, if it failed, into an outcome.</summary>
    private async Task<BulkItemResult> ChangeOneOfManyAsync(BulkChangeStatusItem item, RequestStatus newStatus, CancellationToken cancellationToken)
    {
        try
        {
            var request = await ApplyStatusChangeAsync(item.Id, newStatus, item.RowVersion, cancellationToken);
            return new BulkItemResult(item.Id, BulkItemOutcome.Updated, null, request.RowVersion);
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

    private BulkItemResult NotUpdated(int requestId, BulkItemOutcome outcome, string message)
    {
        // The same level as a single change that fails this way (GlobalExceptionHandler logs those as warnings).
        _logger.LogWarning("Bulk change: request {RequestId} was not updated ({Outcome})", requestId, outcome);
        return new BulkItemResult(requestId, outcome, message, null);
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
        CancellationToken cancellationToken)
    {
        var request = await _repository.GetForUpdateAsync(id, cancellationToken)
            ?? throw new NotFoundException("Request", id);

        // The user decided based on an older version: someone else has changed the request since.
        if (!request.RowVersion.SequenceEqual(versionSeenByUser))
        {
            throw new ConcurrencyConflictException(id);
        }

        var previousStatus = request.Status;
        request.ChangeStatus(newStatus, _currentUser.UserName, _timeProvider.GetUtcNow());

        // EF Core sends "UPDATE ... WHERE Id = @id AND RowVersion = @versionLoadedInStep1", together with the
        // INSERT of the history row, in one transaction. If another user saved between step 1 and now, no row
        // matches, nothing is saved, and the repository throws ConcurrencyConflictException.
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
}
