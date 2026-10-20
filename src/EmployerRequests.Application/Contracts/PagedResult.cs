namespace EmployerRequests.Application.Contracts;

/// <summary>One page of results and the information needed to page through the rest.</summary>
/// <param name="Items">The rows of the requested page only.</param>
/// <param name="Page">1-based page number.</param>
/// <param name="PageSize">Rows per page.</param>
/// <param name="TotalCount">How many rows match the query in total, across all pages.</param>
public record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount)
{
    public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);
}
