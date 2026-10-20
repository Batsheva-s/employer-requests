using EmployerRequests.Application.Contracts;
using EmployerRequests.Domain;

namespace EmployerRequests.Application.Services;

/// <summary>Entity → DTO mapping, in one place. The API never returns entities.</summary>
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
