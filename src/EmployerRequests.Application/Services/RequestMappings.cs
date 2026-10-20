using EmployerRequests.Application.Contracts;
using EmployerRequests.Domain;

namespace EmployerRequests.Application.Services;

/// <summary>
/// Entity → DTO, for a request that was loaded as an entity (to change its status). Lists and history are
/// projected to their DTOs inside the query itself (RequestRepository), so only the needed columns are read.
/// Either way, the API never returns entities.
/// </summary>
public static class RequestMappings
{
    public static RequestDetailsDto ToDetailsDto(this ServiceRequest request) =>
        new(
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
