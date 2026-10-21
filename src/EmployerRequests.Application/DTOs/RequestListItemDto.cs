using EmployerRequests.Domain;

namespace EmployerRequests.Application.DTOs;

/// <summary>
/// A row of the requests list. <c>RowVersion</c> is the version the user is looking at (base64 in JSON);
/// the client sends it back when changing the status.
/// </summary>
public class RequestListItemDto
{
    public int Id { get; set; }

    public string Title { get; set; } = string.Empty;

    public string OrganizationName { get; set; } = string.Empty;

    public RequestStatus Status { get; set; }

    public RequestPriority Priority { get; set; }

    /// <summary>The handler's name; empty (null) while nobody is assigned.</summary>
    public string? AssignedTo { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
}
