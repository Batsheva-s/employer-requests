namespace EmployerRequests.Application.DTOs;

/// <summary>
/// The answer to a bulk status change: totals, and one result per request in the order they were sent.
/// The HTTP status is 200 whenever the batch itself was valid, so the client reads <c>Succeeded</c> and every
/// item's <c>Outcome</c>.
/// </summary>
public record BulkChangeStatusResultDto(
    int Requested,
    int Succeeded,
    int Failed,
    IReadOnlyList<BulkItemResultDto> Items);

/// <summary>
/// The result for one request. <c>Message</c> says why it was not updated (empty when it was);
/// <c>RowVersion</c> is the new version of an updated request (empty when it was not updated).
/// </summary>
public record BulkItemResultDto(int Id, BulkItemOutcome Outcome, string? Message, byte[]? RowVersion);

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
