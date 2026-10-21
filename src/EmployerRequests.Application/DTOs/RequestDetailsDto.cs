using EmployerRequests.Domain;

namespace EmployerRequests.Application.DTOs;

/// <summary>
/// One request: everything a row of the list has, and the statuses the workflow allows next. The client offers
/// only these statuses, so the user never picks a forbidden move.
/// </summary>
public class RequestDetailsDto : RequestListItemDto
{
    public IReadOnlyList<RequestStatus> AllowedNextStatuses { get; set; } = Array.Empty<RequestStatus>();
}
