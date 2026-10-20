using EmployerRequests.Application.Abstractions;
using EmployerRequests.Application.Contracts;
using EmployerRequests.Application.Exceptions;
using EmployerRequests.Domain;
using Microsoft.EntityFrameworkCore;

namespace EmployerRequests.Infrastructure.Data;

/// <summary>
/// EF Core implementation of <see cref="IRequestRepository"/>.
/// Every read builds one query that SQL Server executes in full: only the requested page – and only the
/// columns the DTO needs – ever leaves the database. Nothing is filtered, sorted or paged in memory.
/// </summary>
public class RequestRepository : IRequestRepository
{
    private readonly AppDbContext _dbContext;

    public RequestRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<PagedResult<RequestListItemDto>> SearchAsync(RequestSearchQuery query, CancellationToken cancellationToken)
    {
        var matching = _dbContext.Requests
            .AsNoTracking()
            .ApplyFilter(query);

        // Query 1: SELECT COUNT(*) ... WHERE <filters> – for the number of pages.
        var totalCount = await matching.CountAsync(cancellationToken);

        // long, so that a huge page number cannot overflow the multiplication.
        var skip = (long)(query.Page - 1) * query.PageSize;
        if (skip >= totalCount)
        {
            // Past the last page: no need to ask the database for rows that do not exist.
            return new PagedResult<RequestListItemDto>([], query.Page, query.PageSize, totalCount);
        }

        // Query 2: SELECT <DTO columns> ... WHERE <filters> ORDER BY <sort>, Id OFFSET @skip ROWS FETCH NEXT @take ROWS ONLY
        var items = await matching
            .ApplySorting(query.SortBy, query.SortDirection)
            .Skip((int)skip)
            .Take(query.PageSize)
            .Select(request => new RequestListItemDto(
                request.Id,
                request.Title,
                request.OrganizationName,
                request.Status,
                request.Priority,
                request.AssignedTo,
                request.CreatedAt,
                request.UpdatedAt,
                request.RowVersion))
            .ToListAsync(cancellationToken);

        return new PagedResult<RequestListItemDto>(items, query.Page, query.PageSize, totalCount);
    }

    public Task<ServiceRequest?> FindAsync(int id, CancellationToken cancellationToken) =>
        _dbContext.Requests
            .AsNoTracking()
            .FirstOrDefaultAsync(request => request.Id == id, cancellationToken);

    public Task<bool> ExistsAsync(int id, CancellationToken cancellationToken) =>
        _dbContext.Requests.AnyAsync(request => request.Id == id, cancellationToken);

    public async Task<IReadOnlyList<RequestStatusChangeDto>> GetHistoryAsync(int requestId, CancellationToken cancellationToken) =>
        await _dbContext.RequestStatusChanges
            .AsNoTracking()
            .Where(change => change.RequestId == requestId)
            .OrderByDescending(change => change.ChangedAt)
            .ThenByDescending(change => change.Id)
            .Select(change => new RequestStatusChangeDto(
                change.Id,
                change.PreviousStatus,
                change.NewStatus,
                change.ChangedBy,
                change.ChangedAt))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<StatusPriorityCount>> CountByStatusAndPriorityAsync(RequestFilter filter, CancellationToken cancellationToken) =>
        await _dbContext.Requests
            .AsNoTracking()
            .ApplyFilter(filter)
            .GroupBy(request => new { request.Status, request.Priority })
            .Select(group => new StatusPriorityCount(group.Key.Status, group.Key.Priority, group.Count()))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<AssigneeWorkloadDto>> GetTopAssigneesAsync(RequestFilter filter, int count, CancellationToken cancellationToken) =>
        await _dbContext.Requests
            .AsNoTracking()
            .ApplyFilter(filter)
            .Where(request => request.AssignedTo != null && request.Status != RequestStatus.Completed)
            .GroupBy(request => request.AssignedTo!)
            .Select(group => new { AssignedTo = group.Key, OpenRequests = group.Count() })
            .OrderByDescending(workload => workload.OpenRequests)
            .ThenBy(workload => workload.AssignedTo)
            .Take(count)
            .Select(workload => new AssigneeWorkloadDto(workload.AssignedTo, workload.OpenRequests))
            .ToListAsync(cancellationToken);

    public Task<ServiceRequest?> GetForUpdateAsync(int id, CancellationToken cancellationToken) =>
        // Tracked (no AsNoTracking): EF Core remembers the loaded RowVersion and uses it in the UPDATE.
        _dbContext.Requests.FirstOrDefaultAsync(request => request.Id == id, cancellationToken);

    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException exception)
        {
            var requestId = exception.Entries
                .Select(entry => entry.Entity)
                .OfType<ServiceRequest>()
                .Select(request => request.Id)
                .FirstOrDefault();

            // Nothing was saved. Forget the pending changes, so this DbContext stays usable – a bulk change
            // continues with the next request on the same DbContext.
            _dbContext.ChangeTracker.Clear();

            throw new ConcurrencyConflictException(requestId);
        }
    }
}
