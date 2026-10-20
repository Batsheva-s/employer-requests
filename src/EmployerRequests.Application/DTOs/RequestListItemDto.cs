using EmployerRequests.Domain;

namespace EmployerRequests.Application.DTOs;

/// <summary>
/// A row of the requests list. <c>RowVersion</c> is the version the user is looking at (base64 in JSON);
/// the client sends it back when changing the status.
/// </summary>
public record RequestListItemDto(
    int Id,
    string Title,
    string OrganizationName,
    RequestStatus Status,
    RequestPriority Priority,
    string? AssignedTo,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    byte[] RowVersion);
