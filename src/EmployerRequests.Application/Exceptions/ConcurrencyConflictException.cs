namespace EmployerRequests.Application.Exceptions;

/// <summary>
/// The request was changed by someone else after the user loaded it, so the user's change was not saved.
/// Returned to the client as 409 Conflict.
/// </summary>
public class ConcurrencyConflictException : Exception
{
    public ConcurrencyConflictException(int requestId)
        : base($"Request {requestId} was changed by another user after you loaded it. Load it again and retry.")
    {
        RequestId = requestId;
    }

    public int RequestId { get; }
}
