using System.ComponentModel.DataAnnotations;
using EmployerRequests.Domain;

namespace EmployerRequests.Application.Contracts;

/// <summary>Body of <c>PATCH /api/requests/status</c>: one new status for up to 100 requests.</summary>
public class BulkChangeStatusCommand : IValidatableObject
{
    public const int MaxItems = 100;

    /// <summary>The new status for all the requests.</summary>
    public required RequestStatus Status { get; init; }

    /// <summary>The requests to change, each with the version the user saw.</summary>
    [Required, MinLength(1), MaxLength(MaxItems)]
    public required List<BulkChangeStatusItem> Items { get; init; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        // JSON such as "items": [null] produces a null entry.
        if (Items.Any(item => item is null))
        {
            yield return new ValidationResult("Items must not contain empty entries.", [nameof(Items)]);
            yield break;
        }

        if (Items.GroupBy(item => item.Id).Any(group => group.Count() > 1))
        {
            yield return new ValidationResult("Each request may appear only once.", [nameof(Items)]);
        }
    }
}

/// <summary>One request in a bulk status change.</summary>
public class BulkChangeStatusItem
{
    [Range(1, int.MaxValue)]
    public required int Id { get; init; }

    /// <summary>The version of this request the user saw (base64).</summary>
    [Required, MinLength(8), MaxLength(8)]
    public required byte[] RowVersion { get; init; }
}
