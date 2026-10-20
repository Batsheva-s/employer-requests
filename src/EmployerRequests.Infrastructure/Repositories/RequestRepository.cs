using System.Linq.Expressions;
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
        var matching = Filter(_dbContext.Requests.AsNoTracking(), search);

        // Query 1: SELECT COUNT(*) ... WHERE <filters> – for the number of pages.
        var totalCount = await matching.CountAsync(cancellationToken);

        // long, so that a huge page number cannot overflow the multiplication.
        var skip = (long)(search.Page - 1) * search.PageSize;
        if (skip >= totalCount)
        {
            // Past the last page: no need to ask the database for rows that do not exist.
            return new PagedResultDto<RequestListItemDto>([], search.Page, search.PageSize, totalCount);
        }

        // Query 2: SELECT <the DTO's columns> ... WHERE <filters> ORDER BY <sort>, Id OFFSET @skip ROWS FETCH NEXT @take ROWS ONLY
        var items = await Sort(matching, search.SortBy, search.SortDirection)
            .Skip((int)skip)
            .Take(search.PageSize)
            .Select(RequestMapper.ToListItem)
            .ToListAsync(cancellationToken);

        return new PagedResultDto<RequestListItemDto>(items, search.Page, search.PageSize, totalCount);
    }

    public Task<ServiceRequest?> GetByIdAsync(int id, CancellationToken cancellationToken) =>
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
            .Select(RequestMapper.ToStatusChange)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<StatusPriorityCount>> CountByStatusAndPriorityAsync(RequestFilterDto filter, CancellationToken cancellationToken) =>
        await Filter(_dbContext.Requests.AsNoTracking(), filter)
            .GroupBy(request => new { request.Status, request.Priority })
            .Select(group => new StatusPriorityCount(group.Key.Status, group.Key.Priority, group.Count()))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<AssigneeWorkloadDto>> GetTopAssigneesAsync(RequestFilterDto filter, int count, CancellationToken cancellationToken) =>
        await Filter(_dbContext.Requests.AsNoTracking(), filter)
            .Where(request => request.AssignedTo != null && request.Status != RequestStatus.Completed)
            .GroupBy(request => request.AssignedTo!)
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

    // ---------- Filtering and sorting: LINQ that EF Core turns into the WHERE and the ORDER BY ----------

    private static IQueryable<ServiceRequest> Filter(IQueryable<ServiceRequest> requests, RequestFilterDto filter)
    {
        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var text = filter.Search.Trim();
            // LIKE '%text%'. EF Core escapes %, _ and [ in the text, so they are searched as plain characters.
            requests = requests.Where(request => request.Title.Contains(text) || request.OrganizationName.Contains(text));
        }

        if (filter.Status is { Length: > 0 } statuses)
        {
            // EF.Constant writes the few selected values into the SQL (Status IN (1, 3)), so SQL Server knows exactly
            // what it is looking for when it builds the plan. Without it, EF Core 9 sends the values as one JSON
            // parameter (OPENJSON), whose size SQL Server can only guess – which can lead to a worse plan.
            requests = requests.Where(request => EF.Constant(statuses).Contains(request.Status));
        }

        if (filter.Priority is { Length: > 0 } priorities)
        {
            requests = requests.Where(request => EF.Constant(priorities).Contains(request.Priority));
        }

        if (!string.IsNullOrWhiteSpace(filter.OrganizationName))
        {
            var prefix = filter.OrganizationName.Trim();
            // LIKE 'text%' – a prefix, so SQL Server can seek IX_Requests_OrganizationName.
            requests = requests.Where(request => request.OrganizationName.StartsWith(prefix));
        }

        if (!string.IsNullOrWhiteSpace(filter.AssignedTo))
        {
            var prefix = filter.AssignedTo.Trim();
            // LIKE 'text%' on IX_Requests_AssignedTo_Status. No "AssignedTo != null" check: in SQL, NULL LIKE 'x%' is
            // never true, and the extra IS NOT NULL made SQL Server read the whole index instead of seeking the
            // prefix (measured – docs/PERFORMANCE.md). The ! only tells the compiler; this code never runs in memory.
            requests = requests.Where(request => request.AssignedTo!.StartsWith(prefix));
        }

        if (filter.CreatedFrom is { } from)
        {
            requests = requests.Where(request => request.CreatedAt >= from);
        }

        if (filter.CreatedTo is { } to)
        {
            requests = requests.Where(request => request.CreatedAt < to);
        }

        return requests;
    }

    private static IOrderedQueryable<ServiceRequest> Sort(IQueryable<ServiceRequest> requests, RequestSortField sortBy, SortDirection direction)
    {
        var sorted = sortBy switch
        {
            RequestSortField.UpdatedAt => SortBy(requests, request => request.UpdatedAt, direction),
            RequestSortField.Title => SortBy(requests, request => request.Title, direction),
            RequestSortField.OrganizationName => SortBy(requests, request => request.OrganizationName, direction),
            RequestSortField.Status => SortBy(requests, request => request.Status, direction),
            RequestSortField.Priority => SortBy(requests, request => request.Priority, direction),
            _ => SortBy(requests, request => request.CreatedAt, direction),
        };

        // Many requests share a status or a priority. Id breaks the ties, so the order is always the same
        // and a row never appears on two pages or on none.
        return direction == SortDirection.Asc
            ? sorted.ThenBy(request => request.Id)
            : sorted.ThenByDescending(request => request.Id);
    }

    /// <summary>OrderBy or OrderByDescending by <paramref name="key"/> – the sort field and the direction are chosen separately.</summary>
    private static IOrderedQueryable<ServiceRequest> SortBy<TKey>(
        IQueryable<ServiceRequest> requests,
        Expression<Func<ServiceRequest, TKey>> key,
        SortDirection direction) =>
        direction == SortDirection.Asc ? requests.OrderBy(key) : requests.OrderByDescending(key);
}
