using System.ComponentModel.DataAnnotations;
using EmployerRequests.Domain;

namespace EmployerRequests.Application.DTOs;

/// <summary>Body of <c>PATCH /api/requests/{id}/status</c>.</summary>
public class ChangeStatusDto
{
    /// <summary>The new status. The workflow must allow it from the current status.</summary>
    public required RequestStatus Status { get; set; }

    /// <summary>
    /// The version of the request the user saw (base64, from the list or from <c>GET /api/requests/{id}</c>).
    /// The change is saved only if the request is still in this version; otherwise the answer is 409.
    /// </summary>
    [Required, MinLength(8), MaxLength(8)]
    public required byte[] RowVersion { get; set; }
}
