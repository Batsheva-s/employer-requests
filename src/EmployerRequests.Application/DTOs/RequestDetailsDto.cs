using EmployerRequests.Domain;

namespace EmployerRequests.Application.DTOs;

/// <summary>
/// One request. <c>RowVersion</c> is the version the user is looking at (base64 in JSON), sent back when changing
/// the status. <c>AllowedNextStatuses</c> comes from the workflow: the client offers only these statuses, so the
/// user never picks a forbidden move.
/// </summary>
public record RequestDetailsDto(
    int Id,
    string Title,
    string OrganizationName,
    RequestStatus Status,
    RequestPriority Priority,
    string? AssignedTo,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    byte[] RowVersion,
    IReadOnlyList<RequestStatus> AllowedNextStatuses);
