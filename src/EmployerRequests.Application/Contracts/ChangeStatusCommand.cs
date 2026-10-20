using System.ComponentModel.DataAnnotations;
using EmployerRequests.Domain;

namespace EmployerRequests.Application.Contracts;

/// <summary>Body of <c>PATCH /api/requests/{id}/status</c>.</summary>
public class ChangeStatusCommand
{
    /// <summary>The new status. Must be allowed by the workflow from the current status.</summary>
    public required RequestStatus Status { get; init; }

    /// <summary>
    /// The version of the request the user saw (base64, from the list or from GET /api/requests/{id}).
    /// The change is saved only if the request is still in this version; otherwise the answer is 409.
    /// </summary>
    [Required, MinLength(8), MaxLength(8)]
    public required byte[] RowVersion { get; init; }
}
