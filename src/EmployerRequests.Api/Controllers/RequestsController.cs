using EmployerRequests.Application.Contracts;
using EmployerRequests.Application.Services;
using Microsoft.AspNetCore.Mvc;

namespace EmployerRequests.Api.Controllers;

/// <summary>
/// Requests from organizations and employers: search, summary, details, history and status changes.
/// The controller only speaks HTTP – every decision is made in the application services.
/// Invalid input is answered with 400 automatically ([ApiController]); errors are mapped to Problem Details
/// in one place (GlobalExceptionHandler).
/// </summary>
[ApiController]
[Route("api/requests")]
[Produces("application/json")]
public class RequestsController : ControllerBase
{
    private readonly IRequestQueryService _queryService;
    private readonly IRequestStatusService _statusService;
    private readonly IRequestSummaryService _summaryService;

    public RequestsController(
        IRequestQueryService queryService,
        IRequestStatusService statusService,
        IRequestSummaryService summaryService)
    {
        _queryService = queryService;
        _statusService = statusService;
        _summaryService = summaryService;
    }

    /// <summary>Searches requests. Filtering, sorting and paging all run in the database; one page is returned.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<RequestListItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PagedResult<RequestListItemDto>>> Search(
        [FromQuery] RequestSearchQuery query,
        CancellationToken cancellationToken)
    {
        return Ok(await _queryService.SearchAsync(query, cancellationToken));
    }

    /// <summary>Counts for the same filters as the search: by status, by priority, and the busiest handlers.</summary>
    [HttpGet("summary")]
    [ProducesResponseType(typeof(RequestsSummaryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<RequestsSummaryDto>> GetSummary(
        [FromQuery] RequestFilter filter,
        CancellationToken cancellationToken)
    {
        return Ok(await _summaryService.GetSummaryAsync(filter, cancellationToken));
    }

    /// <summary>One request, with its current version and the statuses it may move to.</summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(RequestDetailsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RequestDetailsDto>> GetById(int id, CancellationToken cancellationToken)
    {
        return Ok(await _queryService.GetByIdAsync(id, cancellationToken));
    }

    /// <summary>The status history of a request, newest first.</summary>
    [HttpGet("{id:int}/history")]
    [ProducesResponseType(typeof(IReadOnlyList<RequestStatusChangeDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<RequestStatusChangeDto>>> GetHistory(int id, CancellationToken cancellationToken)
    {
        return Ok(await _queryService.GetHistoryAsync(id, cancellationToken));
    }

    /// <summary>
    /// Changes the status of one request. The body carries the version the user saw: if someone else changed
    /// the request since, nothing is saved and the answer is 409. The answer to a successful change carries
    /// the new version.
    /// </summary>
    [HttpPatch("{id:int}/status")]
    [ProducesResponseType(typeof(RequestDetailsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<RequestDetailsDto>> ChangeStatus(
        int id,
        [FromBody] ChangeStatusCommand command,
        CancellationToken cancellationToken)
    {
        return Ok(await _statusService.ChangeStatusAsync(id, command, cancellationToken));
    }

    /// <summary>
    /// Changes the status of up to 100 requests. Partial success: every request gets its own outcome
    /// (Updated, NotFound, Conflict, InvalidTransition). The answer is 200 whenever the batch itself was valid,
    /// even if some – or all – of the requests were not updated; 400 means nothing was changed.
    /// </summary>
    [HttpPatch("status")]
    [ProducesResponseType(typeof(BulkChangeStatusResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<BulkChangeStatusResult>> ChangeStatusBulk(
        [FromBody] BulkChangeStatusCommand command,
        CancellationToken cancellationToken)
    {
        return Ok(await _statusService.ChangeStatusBulkAsync(command, cancellationToken));
    }
}
