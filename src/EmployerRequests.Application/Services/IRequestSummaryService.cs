using EmployerRequests.Application.Contracts;

namespace EmployerRequests.Application.Services;

/// <summary>Aggregations for the same filters as the list.</summary>
public interface IRequestSummaryService
{
    Task<RequestsSummaryDto> GetSummaryAsync(RequestFilter filter, CancellationToken cancellationToken);
}
