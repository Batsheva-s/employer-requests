using EmployerRequests.Application.DTOs;
using EmployerRequests.Application.Exceptions;
using EmployerRequests.Application.Interfaces;
using EmployerRequests.Application.Mapping;
using EmployerRequests.Domain;
using EmployerRequests.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace EmployerRequests.Infrastructure.Repositories;

/// <summary>
/// The EF Core implementation of <see cref="IRequestRepository"/>.
/// Every read builds one query that SQL Server runs in full: only the requested page – and only the columns the
/// DTO needs – ever leaves the database. Nothing is filtered, sorted or paged in memory.
/// </summary>
public class RequestRepository : IRequestRepository
{
    private readonly AppDbContext _dbContext;

    public RequestRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    // ---------- Reads ----------

    public async Task<PagedResultDto<RequestListItemDto>> SearchAsync(RequestSearchDto search, CancellationToken cancellationToken)
    {
        // The query is only built here: nothing is sent to the database yet.
        var query = Filter(_dbContext.Requests.AsNoTracking(), search);

        // Query 1: SELECT COUNT(*) FROM Requests WHERE <filters> – for the number of pages.
        var totalCount = await query.CountAsync(cancellationToken);

        // long, so that a huge page number cannot overflow the multiplication.
        var skip = (long)(search.Page - 1) * search.PageSize;
        if (skip >= totalCount)
        {
            // Past the last page: no need to ask the database for rows that do not exist.
            return new PagedResultDto<RequestListItemDto>([], search.Page, search.PageSize, totalCount);
        }

        // Query 2: SELECT <the DTO's columns> FROM Requests WHERE <filters> ORDER BY <sort>, Id
        //          OFFSET <skip> ROWS FETCH NEXT <page size> ROWS ONLY
        var items = await Sort(query, search.SortBy, search.SortDirection)
            .Skip((int)skip)
            .Take(search.PageSize)
            .Select(RequestMapper.ToListItem)
            .ToListAsync(cancellationToken);

        return new PagedResultDto<RequestListItemDto>(items, search.Page, search.PageSize, totalCount);
    }

    // SELECT ... FROM Requests WHERE Id = @id
    public Task<ServiceRequest?> GetByIdAsync(int id, CancellationToken cancellationToken) =>
        _dbContext.Requests
            .AsNoTracking()
            .FirstOrDefaultAsync(request => request.Id == id, cancellationToken);

    // SELECT CASE WHEN EXISTS (SELECT 1 FROM Requests WHERE Id = @id) THEN 1 ELSE 0 END
    public Task<bool> ExistsAsync(int id, CancellationToken cancellationToken) =>
        _dbContext.Requests.AnyAsync(request => request.Id == id, cancellationToken);

    // SELECT <the DTO's columns> FROM RequestStatusChanges WHERE RequestId = @id ORDER BY ChangedAt DESC, Id DESC
    public async Task<IReadOnlyList<RequestStatusChangeDto>> GetHistoryAsync(int requestId, CancellationToken cancellationToken) =>
        await _dbContext.RequestStatusChanges
            .AsNoTracking()
            .Where(change => change.RequestId == requestId)
            .OrderByDescending(change => change.ChangedAt)
            .ThenByDescending(change => change.Id)
            .Select(RequestMapper.ToStatusChange)
            .ToListAsync(cancellationToken);

    // SELECT Status, Priority, COUNT(*) FROM Requests WHERE <filters> GROUP BY Status, Priority
    public async Task<IReadOnlyList<StatusPriorityCount>> CountByStatusAndPriorityAsync(RequestFilterDto filter, CancellationToken cancellationToken) =>
        await Filter(_dbContext.Requests.AsNoTracking(), filter)
            .GroupBy(request => new { request.Status, request.Priority })
            .Select(group => new StatusPriorityCount(group.Key.Status, group.Key.Priority, group.Count()))
            .ToListAsync(cancellationToken);

    // SELECT TOP (@count) AssignedTo, COUNT(*) FROM Requests
    // WHERE <filters> AND AssignedTo IS NOT NULL AND Status <> Completed
    // GROUP BY AssignedTo ORDER BY COUNT(*) DESC, AssignedTo
    public async Task<IReadOnlyList<AssigneeWorkloadDto>> GetTopAssigneesAsync(RequestFilterDto filter, int count, CancellationToken cancellationToken) =>
        await Filter(_dbContext.Requests.AsNoTracking(), filter)
            .Where(request => request.AssignedTo != null && request.Status != RequestStatus.Completed)
            .GroupBy(request => request.AssignedTo!)
            // An anonymous type first: EF Core can sort by its properties in SQL. The DTO is created last.
            .Select(group => new { AssignedTo = group.Key, OpenRequests = group.Count() })
            .OrderByDescending(workload => workload.OpenRequests)
            .ThenBy(workload => workload.AssignedTo)
            .Take(count)
            .Select(workload => new AssigneeWorkloadDto(workload.AssignedTo, workload.OpenRequests))
            .ToListAsync(cancellationToken);

    // ---------- Writes ----------

    public Task<ServiceRequest?> GetForUpdateAsync(int id, CancellationToken cancellationToken) =>
        // Tracked (no AsNoTracking): EF Core remembers the loaded RowVersion and puts it in the UPDATE's WHERE.
        _dbContext.Requests.FirstOrDefaultAsync(request => request.Id == id, cancellationToken);

    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            // One SaveChanges is one transaction: the UPDATE of the request and the INSERT of its history row
            // are saved together, or not at all.
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException exception)
        {
            // The UPDATE found a different RowVersion: someone else saved this request first.
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

    // ---------- Filtering and sorting: LINQ that EF Core turns into the WHERE and the ORDER BY ----------

    /// <summary>Adds a WHERE condition for every filter the user filled in. The conditions are combined with AND.</summary>
    private static IQueryable<ServiceRequest> Filter(IQueryable<ServiceRequest> requests, RequestFilterDto filter)
    {
        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var text = filter.Search.Trim();
            // Title LIKE '%text%' OR OrganizationName LIKE '%text%'. EF Core escapes %, _ and [ in the text,
            // so they are searched as plain characters.
            requests = requests.Where(request => request.Title.Contains(text) || request.OrganizationName.Contains(text));
        }

        if (filter.Status != null && filter.Status.Length > 0)
        {
            RequestStatus[] statuses = filter.Status;
            // Status IN (1, 3). EF.Constant writes the values into the SQL itself, so SQL Server knows exactly which
            // statuses it looks for and can use the index. (Without it, EF Core 9 sends them as one JSON parameter.)
            requests = requests.Where(request => EF.Constant(statuses).Contains(request.Status));
        }

        if (filter.Priority != null && filter.Priority.Length > 0)
        {
            RequestPriority[] priorities = filter.Priority;
            // Priority IN (...), the same way.
            requests = requests.Where(request => EF.Constant(priorities).Contains(request.Priority));
        }

        if (!string.IsNullOrWhiteSpace(filter.OrganizationName))
        {
            var prefix = filter.OrganizationName.Trim();
            // OrganizationName LIKE 'text%' – "starts with", so SQL Server can seek IX_Requests_OrganizationName.
            requests = requests.Where(request => request.OrganizationName.StartsWith(prefix));
        }

        if (!string.IsNullOrWhiteSpace(filter.AssignedTo))
        {
            var prefix = filter.AssignedTo.Trim();
            // AssignedTo LIKE 'text%', on IX_Requests_AssignedTo_Status. No "AssignedTo != null" check: in SQL,
            // NULL LIKE 'x%' is never true, and the extra IS NOT NULL made SQL Server read the whole index instead of
            // seeking the prefix (measured – docs/PERFORMANCE.md). The ! only tells the compiler; this runs in SQL.
            requests = requests.Where(request => request.AssignedTo!.StartsWith(prefix));
        }

        if (filter.CreatedFrom != null)
        {
            var from = filter.CreatedFrom.Value;
            // CreatedAt >= from – the first moment is included.
            requests = requests.Where(request => request.CreatedAt >= from);
        }

        if (filter.CreatedTo != null)
        {
            var to = filter.CreatedTo.Value;
            // CreatedAt < to – the client sends the start of the day after the last day the user picked.
            requests = requests.Where(request => request.CreatedAt < to);
        }

        return requests;
    }

    /// <summary>
    /// ORDER BY the field the user chose, in the direction she chose, and then by Id.
    /// The fields are a closed list (an enum), so a client can never sort by an arbitrary column.
    /// </summary>
    private static IQueryable<ServiceRequest> Sort(IQueryable<ServiceRequest> requests, RequestSortField sortBy, SortDirection direction)
    {
        var ascending = direction == SortDirection.Asc;
        IOrderedQueryable<ServiceRequest> sorted;

        switch (sortBy)
        {
            case RequestSortField.UpdatedAt:
                sorted = ascending
                    ? requests.OrderBy(request => request.UpdatedAt)
                    : requests.OrderByDescending(request => request.UpdatedAt);
                break;

            case RequestSortField.Title:
                sorted = ascending
                    ? requests.OrderBy(request => request.Title)
                    : requests.OrderByDescending(request => request.Title);
                break;

            case RequestSortField.OrganizationName:
                sorted = ascending
                    ? requests.OrderBy(request => request.OrganizationName)
                    : requests.OrderByDescending(request => request.OrganizationName);
                break;

            case RequestSortField.Status:
                sorted = ascending
                    ? requests.OrderBy(request => request.Status)
                    : requests.OrderByDescending(request => request.Status);
                break;

            case RequestSortField.Priority:
                sorted = ascending
                    ? requests.OrderBy(request => request.Priority)
                    : requests.OrderByDescending(request => request.Priority);
                break;

            case RequestSortField.CreatedAt:
            default:
                sorted = ascending
                    ? requests.OrderBy(request => request.CreatedAt)
                    : requests.OrderByDescending(request => request.CreatedAt);
                break;
        }

        // Many requests share a status or a priority. Id breaks the ties, so the order is always the same
        // and a row never appears on two pages or on none.
        return ascending
            ? sorted.ThenBy(request => request.Id)
            : sorted.ThenByDescending(request => request.Id);
    }
}
