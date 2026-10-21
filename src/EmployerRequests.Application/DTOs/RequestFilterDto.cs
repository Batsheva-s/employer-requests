using System.ComponentModel.DataAnnotations;
using EmployerRequests.Domain;

namespace EmployerRequests.Application.DTOs;

/// <summary>
/// The filters shared by the list and the summary, read from the query string.
/// Every filter is optional, and filters are combined with AND.
/// </summary>
public class RequestFilterDto : IValidatableObject
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

    /// <summary>False for the default, unfiltered view – the only view whose summary is cached.</summary>
    public bool HasFilters()
    {
        return !string.IsNullOrWhiteSpace(Search)
            || (Status != null && Status.Length > 0)
            || (Priority != null && Priority.Length > 0)
            || !string.IsNullOrWhiteSpace(OrganizationName)
            || !string.IsNullOrWhiteSpace(AssignedTo)
            || CreatedFrom != null
            || CreatedTo != null;
    }

    /// <summary>
    /// The rules that attributes cannot express. ASP.NET Core runs them before the action, like the attributes,
    /// and answers 400 with every error found.
    /// </summary>
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        var errors = new List<ValidationResult>();

        // The query string binder also accepts numbers (?status=99), so every value is checked against the enum.
        if (Status != null && Status.Any(status => !Enum.IsDefined(status)))
        {
            errors.Add(new ValidationResult("Unknown status.", new[] { nameof(Status) }));
        }

        if (Priority != null && Priority.Any(priority => !Enum.IsDefined(priority)))
        {
            errors.Add(new ValidationResult("Unknown priority.", new[] { nameof(Priority) }));
        }

        if (CreatedFrom != null && CreatedTo != null && CreatedFrom.Value >= CreatedTo.Value)
        {
            errors.Add(new ValidationResult(
                "CreatedFrom must be earlier than CreatedTo.",
                new[] { nameof(CreatedFrom), nameof(CreatedTo) }));
        }

        return errors;
    }
}
