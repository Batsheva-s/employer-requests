using EmployerRequests.Domain;

namespace EmployerRequests.Application.DTOs;

/// <summary>One row of a request's status history: when, from which status to which, and who made the change.</summary>
public record RequestStatusChangeDto(
    long Id,
    RequestStatus PreviousStatus,
    RequestStatus NewStatus,
    string ChangedBy,
    DateTimeOffset ChangedAt);
