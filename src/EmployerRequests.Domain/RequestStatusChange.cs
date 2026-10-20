namespace EmployerRequests.Domain;

/// <summary>
/// One status change of a request – a row in the audit trail.
/// Created only by <see cref="ServiceRequest.ChangeStatus"/>, never changed afterwards.
/// </summary>
public class RequestStatusChange
{
    public const int ChangedByMaxLength = 100;

    // Required by EF Core to materialize rows.
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
