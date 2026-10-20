using EmployerRequests.Application.Contracts;
using EmployerRequests.Application.Exceptions;

namespace EmployerRequests.Application.Services;

/// <summary>Reading requests: the list, one request, and its history.</summary>
public interface IRequestQueryService
{
    Task<PagedResult<RequestListItemDto>> SearchAsync(RequestSearchQuery query, CancellationToken cancellationToken);

    /// <exception cref="NotFoundException">There is no request with this id.</exception>
    Task<RequestDetailsDto> GetByIdAsync(int id, CancellationToken cancellationToken);

    /// <exception cref="NotFoundException">There is no request with this id.</exception>
    Task<IReadOnlyList<RequestStatusChangeDto>> GetHistoryAsync(int id, CancellationToken cancellationToken);
}
