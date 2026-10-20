using EmployerRequests.Application.Contracts;
using EmployerRequests.Application.Exceptions;
using EmployerRequests.Domain;

namespace EmployerRequests.Application.Services;

/// <summary>Changing the status of one request or of many requests at once.</summary>
public interface IRequestStatusService
{
    /// <summary>Changes the status of one request and returns it with its new version.</summary>
    /// <exception cref="NotFoundException">There is no request with this id.</exception>
    /// <exception cref="ConcurrencyConflictException">Someone else changed the request after the user loaded it.</exception>
    /// <exception cref="InvalidStatusTransitionException">The workflow does not allow this move.</exception>
    Task<RequestDetailsDto> ChangeStatusAsync(int id, ChangeStatusCommand command, CancellationToken cancellationToken);

    /// <summary>Changes the status of up to 100 requests. Each request succeeds or fails on its own.</summary>
    Task<BulkChangeStatusResult> ChangeStatusBulkAsync(BulkChangeStatusCommand command, CancellationToken cancellationToken);
}
