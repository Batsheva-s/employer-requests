namespace EmployerRequests.Domain;

/// <summary>
/// How urgent a request is. Stored in the database as tinyint.
/// The numeric order is the business order, so sorting by priority puts High above Low.
/// </summary>
public enum RequestPriority : byte
{
    Low = 1,
    Medium = 2,
    High = 3
}
