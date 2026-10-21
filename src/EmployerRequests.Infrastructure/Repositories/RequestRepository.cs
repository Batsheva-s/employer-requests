using EmployerRequests.Application.DTOs;
using EmployerRequests.Application.Exceptions;
using EmployerRequests.Application.Interfaces;
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
        // 1. Build the query with the filters. Nothing is sent to the database yet.
        IQueryable<ServiceRequest> query = _dbContext.Requests.AsNoTracking();
        query = ApplyFilters(query, search);

        // 2. How many requests match, for the number of pages:
        //    SELECT COUNT(*) FROM Requests WHERE <filters>
        int totalCount = await query.CountAsync(cancellationToken);

        // 3. How many rows come before the requested page. (long: a huge page number times the page size
        //    could overflow an int.)
        long rowsBeforePage = (long)(search.Page - 1) * search.PageSize;
        if (rowsBeforePage >= totalCount)
        {
            // The page is past the last row: it is empty, and the database is not asked again.
            return new PagedResultDto<RequestListItemDto>
            {
                Items = new List<RequestListItemDto>(),
                Page = search.Page,
                PageSize = search.PageSize,
                TotalCount = totalCount
            };
        }

        // 4. Sort, cut out the page, and read only the columns of the DTO:
        //    SELECT ... FROM Requests WHERE <filters> ORDER BY <sort>, Id
        //    OFFSET <rows before the page> ROWS FETCH NEXT <page size> ROWS ONLY
        query = ApplySort(query, search.SortBy, search.SortDirection);

        List<RequestListItemDto> items = await query
            .Skip((int)rowsBeforePage)   // smaller than totalCount here, so it fits in an int
            .Take(search.PageSize)
            .Select(request => new RequestListItemDto
            {
                Id = request.Id,
                Title = request.Title,
                OrganizationName = request.OrganizationName,
                Status = request.Status,
                Priority = request.Priority,
                AssignedTo = request.AssignedTo,
                CreatedAt = request.CreatedAt,
                UpdatedAt = request.UpdatedAt,
                RowVersion = request.RowVersion
            })
            .ToListAsync(cancellationToken);

        return new PagedResultDto<RequestListItemDto>
        {
            Items = items,
            Page = search.Page,
            PageSize = search.PageSize,
            TotalCount = totalCount
        };
    }

    public async Task<ServiceRequest?> GetByIdAsync(int id, CancellationToken cancellationToken)
    {
        // SELECT ... FROM Requests WHERE Id = @id
        return await _dbContext.Requests
            .AsNoTracking()
            .FirstOrDefaultAsync(request => request.Id == id, cancellationToken);
    }

    public async Task<bool> ExistsAsync(int id, CancellationToken cancellationToken)
    {
        // SELECT CASE WHEN EXISTS (SELECT 1 FROM Requests WHERE Id = @id) THEN 1 ELSE 0 END
        return await _dbContext.Requests.AnyAsync(request => request.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<RequestStatusChangeDto>> GetHistoryAsync(int requestId, CancellationToken cancellationToken)
    {
        // SELECT ... FROM RequestStatusChanges WHERE RequestId = @requestId ORDER BY ChangedAt DESC, Id DESC
        return await _dbContext.RequestStatusChanges
            .AsNoTracking()
            .Where(change => change.RequestId == requestId)
            .OrderByDescending(change => change.ChangedAt)
            .ThenByDescending(change => change.Id)
            .Select(change => new RequestStatusChangeDto
            {
                Id = change.Id,
                PreviousStatus = change.PreviousStatus,
                NewStatus = change.NewStatus,
                ChangedBy = change.ChangedBy,
                ChangedAt = change.ChangedAt
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<StatusPriorityCount>> CountByStatusAndPriorityAsync(RequestFilterDto filter, CancellationToken cancellationToken)
    {
        // SELECT Status, Priority, COUNT(*) FROM Requests WHERE <filters> GROUP BY Status, Priority
        IQueryable<ServiceRequest> query = _dbContext.Requests.AsNoTracking();
        query = ApplyFilters(query, filter);

        return await query
            .GroupBy(request => new { Status = request.Status, Priority = request.Priority })
            .Select(group => new StatusPriorityCount
            {
                Status = group.Key.Status,
                Priority = group.Key.Priority,
                Count = group.Count()
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<AssigneeWorkloadDto>> GetTopAssigneesAsync(RequestFilterDto filter, int count, CancellationToken cancellationToken)
    {
        // SELECT TOP (@count) AssignedTo, COUNT(*) FROM Requests
        // WHERE <filters> AND AssignedTo IS NOT NULL AND Status <> Completed
        // GROUP BY AssignedTo ORDER BY COUNT(*) DESC, AssignedTo
        IQueryable<ServiceRequest> query = _dbContext.Requests.AsNoTracking();
        query = ApplyFilters(query, filter);

        return await query
            .Where(request => request.AssignedTo != null && request.Status != RequestStatus.Completed)
            // The ! only tells the compiler that AssignedTo is not null here – the Where above made sure of it.
            .GroupBy(request => request.AssignedTo!)
            .Select(group => new AssigneeWorkloadDto
            {
                AssignedTo = group.Key,
                OpenRequests = group.Count()
            })
            .OrderByDescending(workload => workload.OpenRequests)
            .ThenBy(workload => workload.AssignedTo)
            .Take(count)
            .ToListAsync(cancellationToken);
    }

    // ---------- Writes ----------

    public async Task<ServiceRequest?> GetForUpdateAsync(int id, CancellationToken cancellationToken)
    {
        // Tracked (no AsNoTracking): EF Core remembers the loaded RowVersion and puts it in the UPDATE's WHERE.
        return await _dbContext.Requests.FirstOrDefaultAsync(request => request.Id == id, cancellationToken);
    }

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
            // The exception lists the entities whose save failed; we take the id of the request among them.
            int requestId = 0;
            foreach (var entry in exception.Entries)
            {
                if (entry.Entity is ServiceRequest request)
                {
                    requestId = request.Id;
                    break;
                }
            }

            // Nothing was saved. Forget the pending changes, so this DbContext stays usable – a bulk change
            // continues with the next request on the same DbContext.
            _dbContext.ChangeTracker.Clear();

            throw new ConcurrencyConflictException(requestId);
        }
    }

    // ---------- Filters: one small method per filter ----------

    /// <summary>
    /// Adds a WHERE condition for every filter the user filled in; the conditions are combined with AND.
    /// Each filter method returns the query unchanged when its filter is empty.
    /// Nothing runs here: the query is only built, and runs later – in CountAsync or ToListAsync.
    /// </summary>
    private static IQueryable<ServiceRequest> ApplyFilters(IQueryable<ServiceRequest> query, RequestFilterDto filter)
    {
        query = FilterBySearchText(query, filter.Search);
        query = FilterByStatuses(query, filter.Status);
        query = FilterByPriorities(query, filter.Priority);
        query = FilterByOrganizationName(query, filter.OrganizationName);
        query = FilterByAssignedTo(query, filter.AssignedTo);
        query = FilterByCreatedFrom(query, filter.CreatedFrom);
        query = FilterByCreatedTo(query, filter.CreatedTo);
        return query;
    }

    /// <summary>The text appears in the title or in the organization name.</summary>
    private static IQueryable<ServiceRequest> FilterBySearchText(IQueryable<ServiceRequest> query, string? searchText)
    {
        if (string.IsNullOrWhiteSpace(searchText))
        {
            return query;
        }

        // Title LIKE '%text%' OR OrganizationName LIKE '%text%'.
        // EF Core escapes %, _ and [ in the text, so they are searched as plain characters.
        string text = searchText.Trim();
        return query.Where(request => request.Title.Contains(text) || request.OrganizationName.Contains(text));
    }

    /// <summary>The request's status is one of the chosen statuses.</summary>
    private static IQueryable<ServiceRequest> FilterByStatuses(IQueryable<ServiceRequest> query, RequestStatus[]? statuses)
    {
        if (statuses == null || statuses.Length == 0)
        {
            return query;
        }

        // Status IN (1, 3) – the values are written into the SQL itself (see Program.cs).
        return query.Where(request => statuses.Contains(request.Status));
    }

    /// <summary>The request's priority is one of the chosen priorities.</summary>
    private static IQueryable<ServiceRequest> FilterByPriorities(IQueryable<ServiceRequest> query, RequestPriority[]? priorities)
    {
        if (priorities == null || priorities.Length == 0)
        {
            return query;
        }

        // Priority IN (...)
        return query.Where(request => priorities.Contains(request.Priority));
    }

    /// <summary>The organization name starts with the text.</summary>
    private static IQueryable<ServiceRequest> FilterByOrganizationName(IQueryable<ServiceRequest> query, string? organizationName)
    {
        if (string.IsNullOrWhiteSpace(organizationName))
        {
            return query;
        }

        // OrganizationName LIKE 'text%' – "starts with", so SQL Server can seek IX_Requests_OrganizationName.
        string prefix = organizationName.Trim();
        return query.Where(request => request.OrganizationName.StartsWith(prefix));
    }

    /// <summary>The handler's name starts with the text.</summary>
    private static IQueryable<ServiceRequest> FilterByAssignedTo(IQueryable<ServiceRequest> query, string? assignedTo)
    {
        if (string.IsNullOrWhiteSpace(assignedTo))
        {
            return query;
        }

        // AssignedTo LIKE 'text%', on IX_Requests_AssignedTo_Status. No "AssignedTo != null" check: in SQL,
        // NULL LIKE 'x%' is never true, and the extra IS NOT NULL made SQL Server read the whole index instead of
        // seeking the prefix (measured – docs/PERFORMANCE.md). The ! only silences the compiler's null warning.
        string prefix = assignedTo.Trim();
        return query.Where(request => request.AssignedTo!.StartsWith(prefix));
    }

    /// <summary>Created at or after this moment.</summary>
    private static IQueryable<ServiceRequest> FilterByCreatedFrom(IQueryable<ServiceRequest> query, DateTimeOffset? createdFrom)
    {
        if (createdFrom == null)
        {
            return query;
        }

        // CreatedAt >= from – the first moment is included.
        DateTimeOffset from = createdFrom.Value;
        return query.Where(request => request.CreatedAt >= from);
    }

    /// <summary>Created before this moment.</summary>
    private static IQueryable<ServiceRequest> FilterByCreatedTo(IQueryable<ServiceRequest> query, DateTimeOffset? createdTo)
    {
        if (createdTo == null)
        {
            return query;
        }

        // CreatedAt < to – the client sends the start of the day after the last day the user picked.
        DateTimeOffset to = createdTo.Value;
        return query.Where(request => request.CreatedAt < to);
    }

    // ---------- Sorting ----------

    /// <summary>
    /// ORDER BY the field the user chose, in the direction she chose, and then by Id.
    /// The fields are a closed list (an enum), so a client can never sort by an arbitrary column.
    /// </summary>
    private static IQueryable<ServiceRequest> ApplySort(IQueryable<ServiceRequest> query, RequestSortField sortBy, SortDirection direction)
    {
        if (direction == SortDirection.Asc)
        {
            return SortAscending(query, sortBy);
        }

        return SortDescending(query, sortBy);
    }

    /// <summary>
    /// ORDER BY &lt;field&gt; ASC, Id ASC.
    /// Many requests share a status or a priority. Id breaks the ties, so the order is always the same: while the
    /// data does not change between the requests, a row never appears on two pages or on none.
    /// </summary>
    private static IQueryable<ServiceRequest> SortAscending(IQueryable<ServiceRequest> query, RequestSortField sortBy)
    {
        switch (sortBy)
        {
            case RequestSortField.UpdatedAt:
                return query.OrderBy(request => request.UpdatedAt).ThenBy(request => request.Id);

            case RequestSortField.Title:
                return query.OrderBy(request => request.Title).ThenBy(request => request.Id);

            case RequestSortField.OrganizationName:
                return query.OrderBy(request => request.OrganizationName).ThenBy(request => request.Id);

            case RequestSortField.Status:
                return query.OrderBy(request => request.Status).ThenBy(request => request.Id);

            case RequestSortField.Priority:
                return query.OrderBy(request => request.Priority).ThenBy(request => request.Id);

            case RequestSortField.CreatedAt:
            default:
                return query.OrderBy(request => request.CreatedAt).ThenBy(request => request.Id);
        }
    }

    /// <summary>ORDER BY &lt;field&gt; DESC, Id DESC – the same fields, newest or highest first.</summary>
    private static IQueryable<ServiceRequest> SortDescending(IQueryable<ServiceRequest> query, RequestSortField sortBy)
    {
        switch (sortBy)
        {
            case RequestSortField.UpdatedAt:
                return query.OrderByDescending(request => request.UpdatedAt).ThenByDescending(request => request.Id);

            case RequestSortField.Title:
                return query.OrderByDescending(request => request.Title).ThenByDescending(request => request.Id);

            case RequestSortField.OrganizationName:
                return query.OrderByDescending(request => request.OrganizationName).ThenByDescending(request => request.Id);

            case RequestSortField.Status:
                return query.OrderByDescending(request => request.Status).ThenByDescending(request => request.Id);

            case RequestSortField.Priority:
                return query.OrderByDescending(request => request.Priority).ThenByDescending(request => request.Id);

            case RequestSortField.CreatedAt:
            default:
                return query.OrderByDescending(request => request.CreatedAt).ThenByDescending(request => request.Id);
        }
    }
}
