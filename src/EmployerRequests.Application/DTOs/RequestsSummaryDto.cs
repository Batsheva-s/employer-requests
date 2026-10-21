using EmployerRequests.Domain;

namespace EmployerRequests.Application.DTOs;

/// <summary>Aggregations over the requests that match the filters – exactly the requests the list shows.</summary>
public class RequestsSummaryDto
{
    /// <summary>Equals the list's total for the same filters.</summary>
    public int TotalCount { get; set; }

    /// <summary>A count for every status (zero included).</summary>
    public IReadOnlyList<StatusCountDto> ByStatus { get; set; } = new List<StatusCountDto>();

    /// <summary>A count for every priority (zero included).</summary>
    public IReadOnlyList<PriorityCountDto> ByPriority { get; set; } = new List<PriorityCountDto>();

    /// <summary>The five handlers with the most open (not completed) requests.</summary>
    public IReadOnlyList<AssigneeWorkloadDto> TopAssignees { get; set; } = new List<AssigneeWorkloadDto>();

    /// <summary>When the numbers were calculated. The unfiltered summary may come from the cache.</summary>
    public DateTimeOffset CalculatedAt { get; set; }
}

public class StatusCountDto
{
    public RequestStatus Status { get; set; }

    public int Count { get; set; }
}

public class PriorityCountDto
{
    public RequestPriority Priority { get; set; }

    public int Count { get; set; }
}

public class AssigneeWorkloadDto
{
    /// <summary>The handler's name.</summary>
    public string AssignedTo { get; set; } = string.Empty;

    /// <summary>How many matching requests of this handler are not completed.</summary>
    public int OpenRequests { get; set; }
}
