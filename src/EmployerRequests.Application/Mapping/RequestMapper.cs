using System.Linq.Expressions;
using EmployerRequests.Application.DTOs;
using EmployerRequests.Domain;

namespace EmployerRequests.Application.Mapping;

/// <summary>
/// Every entity → DTO mapping, in one place. The API never returns entities.
/// <list type="bullet">
/// <item>The list and the history are mapped <b>inside the database query</b>: the repository passes these
/// expressions to <c>Select</c>, EF Core turns them into the column list of the SELECT, and only those columns
/// are read.</item>
/// <item>The details are mapped <b>in memory</b>, from a request that was already loaded as an entity.</item>
/// </list>
/// </summary>
public static class RequestMapper
{
    /// <summary>A request → a row of the list. Runs in SQL (see the class summary).</summary>
    public static readonly Expression<Func<ServiceRequest, RequestListItemDto>> ToListItem = request =>
        new RequestListItemDto(
            request.Id,
            request.Title,
            request.OrganizationName,
            request.Status,
            request.Priority,
            request.AssignedTo,
            request.CreatedAt,
            request.UpdatedAt,
            request.RowVersion);

    /// <summary>A history row → its DTO. Runs in SQL (see the class summary).</summary>
    public static readonly Expression<Func<RequestStatusChange, RequestStatusChangeDto>> ToStatusChange = change =>
        new RequestStatusChangeDto(
            change.Id,
            change.PreviousStatus,
            change.NewStatus,
            change.ChangedBy,
            change.ChangedAt);

    /// <summary>A loaded request → its details, with the statuses the workflow allows next.</summary>
    public static RequestDetailsDto ToDetails(ServiceRequest request) =>
        new(
            request.Id,
            request.Title,
            request.OrganizationName,
            request.Status,
            request.Priority,
            request.AssignedTo,
            request.CreatedAt,
            request.UpdatedAt,
            request.RowVersion,
            StatusWorkflow.NextStatuses(request.Status));
}
