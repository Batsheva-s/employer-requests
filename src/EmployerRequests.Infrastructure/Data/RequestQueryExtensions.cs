using System.Linq.Expressions;
using EmployerRequests.Application.Contracts;
using EmployerRequests.Domain;
using Microsoft.EntityFrameworkCore;

namespace EmployerRequests.Infrastructure.Data;

/// <summary>
/// Turns the filter and sort options into LINQ that EF Core translates to SQL.
/// Nothing here runs in memory: every condition becomes part of the WHERE / ORDER BY of one query.
/// </summary>
internal static class RequestQueryExtensions
{
    public static IQueryable<ServiceRequest> ApplyFilter(this IQueryable<ServiceRequest> requests, RequestFilter filter)
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
            // what it is looking for and seeks the index. Without it, EF Core 9 sends the values as one JSON
            // parameter (OPENJSON), whose size SQL Server cannot estimate – and then it scans.
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

    public static IOrderedQueryable<ServiceRequest> ApplySorting(
        this IQueryable<ServiceRequest> requests,
        RequestSortField sortBy,
        SortDirection direction)
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

    private static IOrderedQueryable<ServiceRequest> SortBy<TKey>(
        IQueryable<ServiceRequest> requests,
        Expression<Func<ServiceRequest, TKey>> key,
        SortDirection direction) =>
        direction == SortDirection.Asc ? requests.OrderBy(key) : requests.OrderByDescending(key);
}
