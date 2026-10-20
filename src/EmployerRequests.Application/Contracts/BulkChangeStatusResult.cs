namespace EmployerRequests.Application.Contracts;

/// <summary>
/// The answer to a bulk status change: a summary and one result per request, in the order of the request.
/// The HTTP status is 200 whenever the batch itself was valid – the client must read
/// <see cref="BulkChangeStatusResult.Succeeded"/> and every item's <see cref="BulkItemResult.Outcome"/>.
/// </summary>
public record BulkChangeStatusResult(
    int Requested,
    int Succeeded,
    int Failed,
    IReadOnlyList<BulkItemResult> Items);

/// <summary>
/// The result for one request. <c>Message</c> says why it was not updated (empty when it was);
/// <c>RowVersion</c> is the new version of an updated request (empty when it was not updated).
/// </summary>
public record BulkItemResult(int Id, BulkItemOutcome Outcome, string? Message, byte[]? RowVersion);

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
