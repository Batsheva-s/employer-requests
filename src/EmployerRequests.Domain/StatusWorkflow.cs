namespace EmployerRequests.Domain;

/// <summary>
/// The status workflow – the one place that defines which status changes are allowed:
/// <code>
///   New        → InProgress, Waiting
///   InProgress → Waiting, Completed
///   Waiting    → InProgress, Completed
///   Completed  → InProgress   (reopen)
/// </code>
/// Staying in the same status is not a change, so it is not allowed either.
/// </summary>
public static class StatusWorkflow
{
    private static readonly Dictionary<RequestStatus, RequestStatus[]> AllowedMoves = new()
    {
        [RequestStatus.New] = [RequestStatus.InProgress, RequestStatus.Waiting],
        [RequestStatus.InProgress] = [RequestStatus.Waiting, RequestStatus.Completed],
        [RequestStatus.Waiting] = [RequestStatus.InProgress, RequestStatus.Completed],
        [RequestStatus.Completed] = [RequestStatus.InProgress],
    };

    /// <summary>The statuses a request in <paramref name="current"/> status may move to.</summary>
    public static IReadOnlyList<RequestStatus> NextStatuses(RequestStatus current) =>
        AllowedMoves.TryGetValue(current, out var next) ? next : [];

    public static bool CanMove(RequestStatus from, RequestStatus to) =>
        NextStatuses(from).Contains(to);
}
