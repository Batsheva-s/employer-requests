namespace EmployerRequests.Application.DTOs;

/// <summary>One page of results, and what the client needs to page through the rest.</summary>
public class PagedResultDto<T>
{
    /// <summary>The rows of the requested page only.</summary>
    public IReadOnlyList<T> Items { get; set; } = new List<T>();

    /// <summary>1-based page number.</summary>
    public int Page { get; set; }

    /// <summary>Rows per page.</summary>
    public int PageSize { get; set; }

    /// <summary>How many rows match the filters in total, across all pages.</summary>
    public int TotalCount { get; set; }

    /// <summary>Rounded up: 45 rows with 20 per page are 3 pages.</summary>
    public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);
}
