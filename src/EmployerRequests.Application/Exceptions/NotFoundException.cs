namespace EmployerRequests.Application.Exceptions;

/// <summary>There is no request with this id. Returned to the client as 404.</summary>
public class NotFoundException : Exception
{
    public NotFoundException(int requestId)
        : base($"Request {requestId} was not found.")
    {
        RequestId = requestId;
    }

    public int RequestId { get; }
}
