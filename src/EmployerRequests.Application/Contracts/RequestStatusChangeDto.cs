using EmployerRequests.Domain;

namespace EmployerRequests.Application.Contracts;

/// <summary>One entry of a request's status history.</summary>
public record RequestStatusChangeDto(
    long Id,
    RequestStatus PreviousStatus,
    RequestStatus NewStatus,
    string ChangedBy,
    DateTimeOffset ChangedAt);
