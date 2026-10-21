using EmployerRequests.Application.DTOs;
using EmployerRequests.Domain;

namespace EmployerRequests.Application.Mapping;

/// <summary>
/// Entity → DTO for the details of one request. The API never returns entities.
/// (The list and the history are mapped inside their database queries, in RequestRepository, so that only the
/// columns the DTO needs are read.)
/// </summary>
public static class RequestMapper
{
    /// <summary>A loaded request → its details, with the statuses the workflow allows next.</summary>
    public static RequestDetailsDto ToDetails(ServiceRequest request)
    {
        return new RequestDetailsDto(
            request.Id,
            request.Title,
            request.OrganizationName,
            request.Status,
            request.Priority,
            request.AssignedTo,
            request.CreatedAt,
            request.UpdatedAt,
            request.RowVersion,
            StatusWorkflow.NextStatuses(request.Status));
    }
}
