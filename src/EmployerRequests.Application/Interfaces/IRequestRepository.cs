using EmployerRequests.Application.DTOs;
using EmployerRequests.Application.Exceptions;
using EmployerRequests.Domain;

namespace EmployerRequests.Application.Interfaces;

/// <summary>
/// All database access for requests. Implemented in the Infrastructure project with EF Core,
/// so this project never depends on EF Core or on SQL Server.
/// </summary>
public interface IRequestRepository
{
    // ---------- Reads: no change tracking; filtering, sorting and paging run in the database ----------

    /// <summary>One page of requests matching the search, plus the total number of matching requests.</summary>
    Task<PagedResultDto<RequestListItemDto>> SearchAsync(RequestSearchDto search, CancellationToken cancellationToken);

    /// <summary>A request for display, or null if it does not exist.</summary>
    Task<ServiceRequest?> GetByIdAsync(int id, CancellationToken cancellationToken);

    Task<bool> ExistsAsync(int id, CancellationToken cancellationToken);

    /// <summary>The status changes of a request, newest first.</summary>
    Task<IReadOnlyList<RequestStatusChangeDto>> GetHistoryAsync(int requestId, CancellationToken cancellationToken);

    /// <summary>
    /// The number of matching requests per (status, priority) pair – at most 4 × 3 = 12 rows,
    /// from which every count of the summary is calculated.
    /// </summary>
    Task<IReadOnlyList<StatusPriorityCount>> CountByStatusAndPriorityAsync(RequestFilterDto filter, CancellationToken cancellationToken);

    /// <summary>The handlers with the most open (not completed) matching requests.</summary>
    Task<IReadOnlyList<AssigneeWorkloadDto>> GetTopAssigneesAsync(RequestFilterDto filter, int count, CancellationToken cancellationToken);

    // ---------- Writes: load the entity, change it through its own method, then save ----------

    /// <summary>Loads a request whose changes <see cref="SaveChangesAsync"/> will save, or null if it does not exist.</summary>
    Task<ServiceRequest?> GetForUpdateAsync(int id, CancellationToken cancellationToken);

    /// <summary>
    /// Saves all pending changes in one transaction. If a request was changed by someone else after it was
    /// loaded, nothing is saved and <see cref="ConcurrencyConflictException"/> is thrown.
    /// </summary>
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
