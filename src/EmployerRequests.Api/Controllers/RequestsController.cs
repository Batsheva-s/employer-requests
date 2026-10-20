using EmployerRequests.Application.DTOs;
using EmployerRequests.Application.Interfaces;
using EmployerRequests.Domain;
using Microsoft.AspNetCore.Mvc;

namespace EmployerRequests.Api.Controllers;

/// <summary>
/// Requests from organizations and employers: search, summary, details, history and status changes.
/// The controller only speaks HTTP – every decision is made in <see cref="IRequestService"/>.
/// Invalid input is answered with 400 automatically ([ApiController]); errors are turned into Problem Details
/// in one place (GlobalExceptionHandler).
/// </summary>
[ApiController]
[Route("api/requests")]
[Produces("application/json")]
public class RequestsController : ControllerBase
{
    /// <summary>
    /// The name recorded in the history. The exam has no login, so the client sends the user's name in this header –
    /// URL-encoded, because HTTP headers carry ASCII only and names may be in Hebrew.
    /// </summary>
    public const string UserNameHeader = "X-User-Name";

    private const string AnonymousUser = "anonymous";

    private readonly IRequestService _requestService;

    public RequestsController(IRequestService requestService)
    {
        _requestService = requestService;
    }

    /// <summary>Searches requests. Filtering, sorting and paging all run in the database; one page is returned.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResultDto<RequestListItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PagedResultDto<RequestListItemDto>>> Search(
        // Not named "search": the query string has a "search" key, and ASP.NET Core would then read every property
        // under that prefix ("search.Page", ...) – and find none of them. The parameter's name must not be a key.
        [FromQuery] RequestSearchDto query,
        CancellationToken cancellationToken)
    {
        return Ok(await _requestService.SearchAsync(query, cancellationToken));
    }

    /// <summary>Counts for the same filters as the search: by status, by priority, and the busiest handlers.</summary>
    [HttpGet("summary")]
    [ProducesResponseType(typeof(RequestsSummaryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<RequestsSummaryDto>> GetSummary(
        [FromQuery] RequestFilterDto filter,
        CancellationToken cancellationToken)
    {
        return Ok(await _requestService.GetSummaryAsync(filter, cancellationToken));
    }

    /// <summary>One request, with its current version and the statuses it may move to.</summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(RequestDetailsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RequestDetailsDto>> GetById(int id, CancellationToken cancellationToken)
    {
        return Ok(await _requestService.GetByIdAsync(id, cancellationToken));
    }

    /// <summary>The status history of a request, newest first.</summary>
    [HttpGet("{id:int}/history")]
    [ProducesResponseType(typeof(IReadOnlyList<RequestStatusChangeDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<RequestStatusChangeDto>>> GetHistory(int id, CancellationToken cancellationToken)
    {
        return Ok(await _requestService.GetHistoryAsync(id, cancellationToken));
    }

    /// <summary>
    /// Changes the status of one request. The body carries the version the user saw: if someone else changed
    /// the request since, nothing is saved and the answer is 409. A successful answer carries the new version.
    /// </summary>
    [HttpPatch("{id:int}/status")]
    [ProducesResponseType(typeof(RequestDetailsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<RequestDetailsDto>> ChangeStatus(
        int id,
        [FromBody] ChangeStatusDto change,
        [FromHeader(Name = UserNameHeader)] string? userName,
        CancellationToken cancellationToken)
    {
        return Ok(await _requestService.ChangeStatusAsync(id, change, ToChangedBy(userName), cancellationToken));
    }

    /// <summary>
    /// Changes the status of up to 100 requests. Partial success: every request gets its own outcome
    /// (Updated, NotFound, Conflict, InvalidTransition). The answer is 200 whenever the batch itself was valid,
    /// even if some – or all – of the requests were not updated; 400 means nothing was changed.
    /// </summary>
    [HttpPatch("status")]
    [ProducesResponseType(typeof(BulkChangeStatusResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<BulkChangeStatusResultDto>> ChangeStatusBulk(
        [FromBody] BulkChangeStatusDto bulkChange,
        [FromHeader(Name = UserNameHeader)] string? userName,
        CancellationToken cancellationToken)
    {
        return Ok(await _requestService.ChangeStatusBulkAsync(bulkChange, ToChangedBy(userName), cancellationToken));
    }

    /// <summary>
    /// The header's value as the name to record: decoded, trimmed, and never longer than the history column.
    /// With real authentication, the name would come from the signed-in user (User.Identity.Name) instead.
    /// </summary>
    private static string ToChangedBy(string? userName)
    {
        var name = string.IsNullOrWhiteSpace(userName) ? string.Empty : Uri.UnescapeDataString(userName).Trim();

        if (name.Length == 0)
        {
            return AnonymousUser;
        }

        return name.Length <= RequestStatusChange.ChangedByMaxLength
            ? name
            : name[..RequestStatusChange.ChangedByMaxLength];
    }
}
