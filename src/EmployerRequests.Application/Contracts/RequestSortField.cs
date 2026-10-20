namespace EmployerRequests.Application.Contracts;

/// <summary>The fields the list can be sorted by – a closed list, so a client can never sort by an arbitrary column.</summary>
public enum RequestSortField
{
    CreatedAt,
    UpdatedAt,
    Title,
    OrganizationName,
    Status,
    Priority
}
