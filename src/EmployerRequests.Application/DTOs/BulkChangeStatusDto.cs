using System.ComponentModel.DataAnnotations;
using EmployerRequests.Domain;

namespace EmployerRequests.Application.DTOs;

/// <summary>Body of <c>PATCH /api/requests/status</c>: one new status for up to 100 requests.</summary>
public class BulkChangeStatusDto : IValidatableObject
{
    public const int MaxItems = 100;

    /// <summary>The new status for all the requests.</summary>
    public required RequestStatus Status { get; set; }

    /// <summary>The requests to change, each with the version the user saw.</summary>
    [Required, MinLength(1), MaxLength(MaxItems)]
    public required List<BulkChangeStatusItemDto> Items { get; set; }

    /// <summary>The rules that attributes cannot express. ASP.NET Core runs them before the action.</summary>
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        var errors = new List<ValidationResult>();

        // JSON such as "items": [null] produces a null entry.
        if (Items.Any(item => item == null))
        {
            errors.Add(new ValidationResult("Items must not contain empty entries.", new[] { nameof(Items) }));
            return errors;
        }

        // Fewer different ids than items means that some request was sent twice.
        var differentIds = Items.Select(item => item.Id).Distinct().Count();
        if (differentIds < Items.Count)
        {
            errors.Add(new ValidationResult("Each request may appear only once.", new[] { nameof(Items) }));
        }

        return errors;
    }
}

/// <summary>One request in a bulk status change.</summary>
public class BulkChangeStatusItemDto
{
    [Range(1, int.MaxValue)]
    public required int Id { get; set; }

    /// <summary>The version of this request the user saw (base64).</summary>
    [Required, MinLength(8), MaxLength(8)]
    public required byte[] RowVersion { get; set; }
}
