namespace EmployerRequests.Application.Exceptions;

/// <summary>The requested resource does not exist. Returned to the client as 404.</summary>
public class NotFoundException : Exception
{
    public NotFoundException(string resourceName, object key)
        : base($"{resourceName} {key} was not found.")
    {
    }
}
