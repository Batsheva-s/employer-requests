using System.ComponentModel.DataAnnotations;
using EmployerRequests.Domain;

namespace EmployerRequests.Application.Contracts;

/// <summary>
/// The filters shared by the list and the summary. Every filter is optional, and filters are combined with AND.
/// </summary>
public class RequestFilter : IValidatableObject
{
    /// <summary>Free text, searched in the title and in the organization name ("contains").</summary>
    [StringLength(100)]
    public string? Search { get; set; }

    /// <summary>One or more statuses, for example <c>?status=New&amp;status=Waiting</c>.</summary>
    public RequestStatus[]? Status { get; set; }

    /// <summary>One or more priorities, for example <c>?priority=High</c>.</summary>
    public RequestPriority[]? Priority { get; set; }

    /// <summary>The organization name starts with this text.</summary>
    [StringLength(ServiceRequest.OrganizationNameMaxLength)]
    public string? OrganizationName { get; set; }

    /// <summary>The handler's name starts with this text.</summary>
    [StringLength(ServiceRequest.AssignedToMaxLength)]
    public string? AssignedTo { get; set; }

    /// <summary>Created at or after this moment (inclusive).</summary>
    public DateTimeOffset? CreatedFrom { get; set; }

    /// <summary>Created before this moment (exclusive). To include a whole day, send the start of the next day.</summary>
    public DateTimeOffset? CreatedTo { get; set; }

    /// <summary>False for the default, unfiltered view – the view whose summary is cached.</summary>
    public bool HasFilters() =>
        !string.IsNullOrWhiteSpace(Search)
        || Status is { Length: > 0 }
        || Priority is { Length: > 0 }
        || !string.IsNullOrWhiteSpace(OrganizationName)
        || !string.IsNullOrWhiteSpace(AssignedTo)
        || CreatedFrom is not null
        || CreatedTo is not null;

    /// <summary>Rules that involve more than one value or that attributes cannot express.</summary>
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        // The query string binder also accepts numbers (?status=99), so every value is checked against the enum.
        if (Status?.Any(status => !Enum.IsDefined(status)) == true)
        {
            yield return new ValidationResult("Unknown status.", [nameof(Status)]);
        }

        if (Priority?.Any(priority => !Enum.IsDefined(priority)) == true)
        {
            yield return new ValidationResult("Unknown priority.", [nameof(Priority)]);
        }

        if (CreatedFrom >= CreatedTo)
        {
            yield return new ValidationResult(
                "CreatedFrom must be earlier than CreatedTo.",
                [nameof(CreatedFrom), nameof(CreatedTo)]);
        }
    }
}
