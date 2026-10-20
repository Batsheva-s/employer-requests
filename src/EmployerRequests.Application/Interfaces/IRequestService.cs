using EmployerRequests.Application.DTOs;
using EmployerRequests.Application.Exceptions;
using EmployerRequests.Domain;

namespace EmployerRequests.Application.Interfaces;

/// <summary>Everything the API can do with requests. The controller calls only this interface.</summary>
public interface IRequestService
{
    /// <summary>One page of requests. Filtering, sorting and paging all run in the database.</summary>
    Task<PagedResultDto<RequestListItemDto>> SearchAsync(RequestSearchDto search, CancellationToken cancellationToken);

    /// <summary>Counts for the same filters as the list: by status, by priority, and the busiest handlers.</summary>
    Task<RequestsSummaryDto> GetSummaryAsync(RequestFilterDto filter, CancellationToken cancellationToken);

    /// <exception cref="NotFoundException">There is no request with this id.</exception>
    Task<RequestDetailsDto> GetByIdAsync(int id, CancellationToken cancellationToken);

    /// <summary>The status history of a request, newest first.</summary>
    /// <exception cref="NotFoundException">There is no request with this id.</exception>
    Task<IReadOnlyList<RequestStatusChangeDto>> GetHistoryAsync(int id, CancellationToken cancellationToken);

    /// <summary>
    /// Changes the status of one request and returns it with its new version. <c>changedBy</c> is the name recorded
    /// in the history.
    /// </summary>
    /// <exception cref="NotFoundException">There is no request with this id.</exception>
    /// <exception cref="ConcurrencyConflictException">Someone else changed the request after the user loaded it.</exception>
    /// <exception cref="InvalidStatusTransitionException">The workflow does not allow this move.</exception>
    Task<RequestDetailsDto> ChangeStatusAsync(int id, ChangeStatusDto change, string changedBy, CancellationToken cancellationToken);

    /// <summary>
    /// Changes the status of up to 100 requests. Each request succeeds or fails on its own.
    /// <c>changedBy</c> is the name recorded in the history.
    /// </summary>
    Task<BulkChangeStatusResultDto> ChangeStatusBulkAsync(BulkChangeStatusDto bulkChange, string changedBy, CancellationToken cancellationToken);
}
