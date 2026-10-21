namespace EmployerRequests.Application.DTOs;

/// <summary>
/// The answer to a bulk status change: totals, and one result per request in the order they were sent.
/// The HTTP status is 200 whenever the batch itself was valid, so the client reads <c>Succeeded</c> and every
/// item's <c>Outcome</c>.
/// </summary>
public class BulkChangeStatusResultDto
{
    public int Requested { get; set; }

    public int Succeeded { get; set; }

    public int Failed { get; set; }

    public IReadOnlyList<BulkItemResultDto> Items { get; set; } = new List<BulkItemResultDto>();
}

/// <summary>The result for one request of a bulk change.</summary>
public class BulkItemResultDto
{
    public int Id { get; set; }

    public BulkItemOutcome Outcome { get; set; }

    /// <summary>Why the request was not updated; empty (null) when it was.</summary>
    public string? Message { get; set; }

    /// <summary>The new version of an updated request; empty (null) when it was not updated.</summary>
    public byte[]? RowVersion { get; set; }
}

public enum BulkItemOutcome
{
    /// <summary>The status was changed and recorded in the history.</summary>
    Updated,

    /// <summary>There is no request with this id.</summary>
    NotFound,

    /// <summary>Someone else changed the request after the user loaded it; nothing was changed.</summary>
    Conflict,

    /// <summary>The workflow does not allow this move from the request's current status.</summary>
    InvalidTransition
}
