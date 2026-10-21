namespace EmployerRequests.Application.DTOs;

/// <summary>One page of results, and what the client needs to page through the rest.</summary>
/// <param name="Items">The rows of the requested page only.</param>
/// <param name="Page">1-based page number.</param>
/// <param name="PageSize">Rows per page.</param>
/// <param name="TotalCount">How many rows match the filters in total, across all pages.</param>
public record PagedResultDto<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount)
{
    /// <summary>Rounded up: 45 rows with 20 per page are 3 pages.</summary>
    public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);
}
