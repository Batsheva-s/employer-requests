using EmployerRequests.Domain;

namespace EmployerRequests.Application.DTOs;

/// <summary>Aggregations over the requests that match the filters – exactly the requests the list shows.</summary>
/// <param name="TotalCount">Equals the list's total for the same filters.</param>
/// <param name="ByStatus">A count for every status (zero included).</param>
/// <param name="ByPriority">A count for every priority (zero included).</param>
/// <param name="TopAssignees">The five handlers with the most open (not completed) requests.</param>
/// <param name="CalculatedAt">When the numbers were calculated. The unfiltered summary may come from the cache.</param>
public record RequestsSummaryDto(
    int TotalCount,
    IReadOnlyList<StatusCountDto> ByStatus,
    IReadOnlyList<PriorityCountDto> ByPriority,
    IReadOnlyList<AssigneeWorkloadDto> TopAssignees,
    DateTimeOffset CalculatedAt);

public record StatusCountDto(RequestStatus Status, int Count);

public record PriorityCountDto(RequestPriority Priority, int Count);

/// <param name="AssignedTo">The handler's name.</param>
/// <param name="OpenRequests">How many matching requests of this handler are not completed.</param>
public record AssigneeWorkloadDto(string AssignedTo, int OpenRequests);
