using EmployerRequests.Application.Contracts;
using EmployerRequests.Application.Exceptions;
using EmployerRequests.Domain;

namespace EmployerRequests.Application.Abstractions;

/// <summary>
/// Data access for requests. Implemented in the infrastructure layer with EF Core,
/// so this layer never depends on EF Core or on SQL Server.
/// </summary>
public interface IRequestRepository
{
    // ---------- Reads: no change tracking; filtering, sorting and paging run in the database ----------

    /// <summary>One page of requests matching the query, plus the total count of matching requests.</summary>
    Task<PagedResult<RequestListItemDto>> SearchAsync(RequestSearchQuery query, CancellationToken cancellationToken);

    /// <summary>A request for display, or null if it does not exist.</summary>
    Task<ServiceRequest?> FindAsync(int id, CancellationToken cancellationToken);

    Task<bool> ExistsAsync(int id, CancellationToken cancellationToken);

    /// <summary>The status changes of a request, newest first.</summary>
    Task<IReadOnlyList<RequestStatusChangeDto>> GetHistoryAsync(int requestId, CancellationToken cancellationToken);

    /// <summary>
    /// The number of matching requests per (status, priority) pair – at most 4 × 3 = 12 rows,
    /// from which every count of the summary is calculated.
    /// </summary>
    Task<IReadOnlyList<StatusPriorityCount>> CountByStatusAndPriorityAsync(RequestFilter filter, CancellationToken cancellationToken);

    /// <summary>The handlers with the most open (not completed) matching requests.</summary>
    Task<IReadOnlyList<AssigneeWorkloadDto>> GetTopAssigneesAsync(RequestFilter filter, int count, CancellationToken cancellationToken);

    // ---------- Writes: load the entity, change it through its own methods, then save ----------

    /// <summary>Loads a request whose changes will be saved by <see cref="SaveChangesAsync"/>, or null if it does not exist.</summary>
    Task<ServiceRequest?> GetForUpdateAsync(int id, CancellationToken cancellationToken);

    /// <summary>
    /// Saves all pending changes in one transaction.
    /// If a request was changed by someone else since it was loaded, nothing is saved and
    /// <see cref="ConcurrencyConflictException"/> is thrown.
    /// </summary>
    Task SaveChangesAsync(CancellationToken cancellationToken);
}

/// <summary>How many matching requests have a given status and priority.</summary>
public record StatusPriorityCount(RequestStatus Status, RequestPriority Priority, int Count);
