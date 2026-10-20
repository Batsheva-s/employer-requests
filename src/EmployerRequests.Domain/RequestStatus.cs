namespace EmployerRequests.Domain;

/// <summary>
/// Where a request is in its handling. Stored in the database as tinyint.
/// The numbers are written explicitly so that reordering the enum never changes the meaning of stored data.
/// </summary>
public enum RequestStatus : byte
{
    New = 1,
    InProgress = 2,
    Waiting = 3,
    Completed = 4
}
