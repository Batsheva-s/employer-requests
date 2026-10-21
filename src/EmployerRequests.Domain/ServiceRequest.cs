namespace EmployerRequests.Domain;

/// <summary>
/// A request (פנייה) received from an organization or an employer.
/// Named ServiceRequest so it is never confused with an HTTP request.
/// </summary>
/// <remarks>
/// The properties have private setters: the status can change only through <see cref="ChangeStatus"/>,
/// which enforces the workflow and records the change in the history – there is no other way to change it.
/// </remarks>
public class ServiceRequest
{
    public const int TitleMaxLength = 200;
    public const int OrganizationNameMaxLength = 200;
    public const int AssignedToMaxLength = 100;

    private readonly List<RequestStatusChange> _statusChanges = new List<RequestStatusChange>();

    // Required by EF Core to materialize rows.
    private ServiceRequest()
    {
    }

    /// <param name="status">New for a request that just arrived; another status when importing existing requests.</param>
    public ServiceRequest(
        string title,
        string organizationName,
        RequestPriority priority,
        string? assignedTo,
        DateTimeOffset createdAt,
        RequestStatus status = RequestStatus.New)
    {
        Title = title;
        OrganizationName = organizationName;
        Priority = priority;
        AssignedTo = assignedTo;
        Status = status;
        CreatedAt = createdAt;
        UpdatedAt = createdAt;
    }

    public int Id { get; private set; }

    public string Title { get; private set; } = string.Empty;

    public string OrganizationName { get; private set; } = string.Empty;

    public RequestStatus Status { get; private set; }

    public RequestPriority Priority { get; private set; }

    /// <summary>Name of the handler; empty while nobody is assigned.</summary>
    public string? AssignedTo { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    /// <summary>
    /// The version of the row. SQL Server generates a new value on every update (a rowversion column),
    /// which is how a concurrent change by another user is detected.
    /// </summary>
    public byte[] RowVersion { get; private set; } = Array.Empty<byte>();

    /// <summary>
    /// The status history. Only changes made in the current unit of work are loaded here;
    /// they are saved together with the request, in the same transaction.
    /// </summary>
    public IReadOnlyCollection<RequestStatusChange> StatusChanges => _statusChanges;

    /// <summary>
    /// Moves the request to <paramref name="newStatus"/>: checks the workflow, records the change in the
    /// history and updates <see cref="UpdatedAt"/>. If the move is not allowed, nothing changes.
    /// </summary>
    /// <exception cref="InvalidStatusTransitionException">The workflow does not allow this move.</exception>
    public void ChangeStatus(RequestStatus newStatus, string changedBy, DateTimeOffset changedAt)
    {
        if (!StatusWorkflow.CanMove(Status, newStatus))
        {
            throw new InvalidStatusTransitionException(Status, newStatus);
        }

        _statusChanges.Add(new RequestStatusChange(Id, Status, newStatus, changedBy, changedAt));
        Status = newStatus;
        UpdatedAt = changedAt;
    }
}
