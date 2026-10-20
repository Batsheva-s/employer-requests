namespace EmployerRequests.Application.Abstractions;

/// <summary>
/// Who is performing the current operation – recorded in the status history.
/// The API layer implements it; with real authentication only that implementation would change.
/// </summary>
public interface ICurrentUser
{
    string UserName { get; }
}
