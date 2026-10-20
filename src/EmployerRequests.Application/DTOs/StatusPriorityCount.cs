using EmployerRequests.Domain;

namespace EmployerRequests.Application.DTOs;

/// <summary>
/// How many matching requests have a given status and priority – one row of the summary's GROUP BY.
/// The repository returns these rows, and RequestService adds them up into the summary.
/// </summary>
public record StatusPriorityCount(RequestStatus Status, RequestPriority Priority, int Count);
