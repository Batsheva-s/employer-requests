namespace EmployerRequests.Domain;

/// <summary>
/// One status change of a request – a row in the audit trail.
/// Created only by <see cref="ServiceRequest.ChangeStatus"/>, never changed afterwards.
/// </summary>
public class RequestStatusChange
{
    public const int ChangedByMaxLength = 100;

    // EF Core uses this constructor when it reads rows (it prefers the one with the fewest parameters),
    // and then sets the properties from the columns.
    private RequestStatusChange()
    {
    }

    public RequestStatusChange(
        int requestId,
        RequestStatus previousStatus,
        RequestStatus newStatus,
        string changedBy,
        DateTimeOffset changedAt)
    {
        RequestId = requestId;
        PreviousStatus = previousStatus;
        NewStatus = newStatus;
        ChangedBy = changedBy;
        ChangedAt = changedAt;
    }

    public long Id { get; private set; }

    public int RequestId { get; private set; }

    public RequestStatus PreviousStatus { get; private set; }

    public RequestStatus NewStatus { get; private set; }

    public string ChangedBy { get; private set; } = string.Empty;

    public DateTimeOffset ChangedAt { get; private set; }
}
