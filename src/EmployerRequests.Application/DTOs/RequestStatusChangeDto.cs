using EmployerRequests.Domain;

namespace EmployerRequests.Application.DTOs;

/// <summary>One row of a request's status history: when, from which status to which, and who made the change.</summary>
public class RequestStatusChangeDto
{
    public long Id { get; set; }

    public RequestStatus PreviousStatus { get; set; }

    public RequestStatus NewStatus { get; set; }

    public string ChangedBy { get; set; } = string.Empty;

    public DateTimeOffset ChangedAt { get; set; }
}
