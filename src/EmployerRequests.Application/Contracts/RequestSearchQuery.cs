using System.ComponentModel.DataAnnotations;

namespace EmployerRequests.Application.Contracts;

/// <summary>The query string of <c>GET /api/requests</c>: the filters, plus sorting and paging.</summary>
public class RequestSearchQuery : RequestFilter
{
    public const int MaxPageSize = 100;

    /// <summary>1-based page number.</summary>
    [Range(1, int.MaxValue)]
    public int Page { get; set; } = 1;

    /// <summary>Rows per page, 1 to 100. A client can never ask for "everything".</summary>
    [Range(1, MaxPageSize)]
    public int PageSize { get; set; } = 20;

    [EnumDataType(typeof(RequestSortField))]
    public RequestSortField SortBy { get; set; } = RequestSortField.CreatedAt;

    [EnumDataType(typeof(SortDirection))]
    public SortDirection SortDirection { get; set; } = SortDirection.Desc;
}
