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
    /// <summary>The statuses a request in <paramref name="current"/> status may move to.</summary>
    public static IReadOnlyList<RequestStatus> NextStatuses(RequestStatus current)
    {
        // A new array on every call: whoever receives it cannot change the rules.
        switch (current)
        {
            case RequestStatus.New:
                return new[] { RequestStatus.InProgress, RequestStatus.Waiting };

            case RequestStatus.InProgress:
                return new[] { RequestStatus.Waiting, RequestStatus.Completed };

            case RequestStatus.Waiting:
                return new[] { RequestStatus.InProgress, RequestStatus.Completed };

            case RequestStatus.Completed:
                return new[] { RequestStatus.InProgress };

            default:
                return Array.Empty<RequestStatus>();
        }
    }

    /// <summary>True if a request in status <paramref name="from"/> may move to <paramref name="to"/>.</summary>
    public static bool CanMove(RequestStatus from, RequestStatus to)
    {
        return NextStatuses(from).Contains(to);
    }
}
