namespace EmployerRequests.Domain;

/// <summary>Thrown when a status change is not allowed by the <see cref="StatusWorkflow"/>.</summary>
public class InvalidStatusTransitionException : Exception
{
    public InvalidStatusTransitionException(RequestStatus currentStatus, RequestStatus requestedStatus)
        : base($"A request in status {currentStatus} cannot move to {requestedStatus}.")
    {
        CurrentStatus = currentStatus;
        RequestedStatus = requestedStatus;
    }

    public RequestStatus CurrentStatus { get; }

    public RequestStatus RequestedStatus { get; }

    /// <summary>What the request may move to instead – helps the client explain the refusal.</summary>
    public IReadOnlyList<RequestStatus> AllowedStatuses => StatusWorkflow.NextStatuses(CurrentStatus);
}
