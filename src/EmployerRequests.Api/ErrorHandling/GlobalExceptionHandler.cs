using EmployerRequests.Application.Exceptions;
using EmployerRequests.Domain;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace EmployerRequests.Api.ErrorHandling;

/// <summary>
/// The one place that turns exceptions into HTTP answers, as Problem Details (RFC 9457):
/// <list type="bullet">
/// <item>404 – the request does not exist (<see cref="NotFoundException"/>).</item>
/// <item>409 – someone else changed the request first (<see cref="ConcurrencyConflictException"/>).</item>
/// <item>422 – the workflow does not allow the status change (<see cref="InvalidStatusTransitionException"/>).</item>
/// <item>500 – anything unexpected: logged in full, returned without internal details.</item>
/// </list>
/// Invalid input never gets here: [ApiController] answers it with 400 before the action runs.
/// </summary>
public class GlobalExceptionHandler : IExceptionHandler
{
    // A de facto standard (nginx) for "the client closed the request". Nobody receives it; it keeps the logs honest.
    private const int ClientClosedRequest = 499;

    private readonly IProblemDetailsService _problemDetailsService;
    private readonly ILogger<GlobalExceptionHandler> _logger;

    public GlobalExceptionHandler(IProblemDetailsService problemDetailsService, ILogger<GlobalExceptionHandler> logger)
    {
        _problemDetailsService = problemDetailsService;
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var method = httpContext.Request.Method;
        var path = httpContext.Request.Path;

        // The browser gave up on the request – for example, the user kept typing and the client cancelled the
        // old search. The database query was cancelled too, through the CancellationToken. This is not an error.
        if (exception is OperationCanceledException && httpContext.RequestAborted.IsCancellationRequested)
        {
            _logger.LogInformation("{Method} {Path} was cancelled by the client", method, path);
            httpContext.Response.StatusCode = ClientClosedRequest;
            return true;
        }

        var problem = CreateProblem(exception);

        if (problem.Status == StatusCodes.Status500InternalServerError)
        {
            _logger.LogError(exception, "Unhandled exception for {Method} {Path}", method, path);
        }
        else
        {
            _logger.LogWarning("{StatusCode} for {Method} {Path}: {Detail}", problem.Status, method, path, problem.Detail);
        }

        httpContext.Response.StatusCode = problem.Status!.Value;

        return await _problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = problem,
            Exception = exception
        });
    }

    private static ProblemDetails CreateProblem(Exception exception) => exception switch
    {
        NotFoundException => new ProblemDetails
        {
            Status = StatusCodes.Status404NotFound,
            Title = "Not found",
            Detail = exception.Message
        },

        ConcurrencyConflictException => new ProblemDetails
        {
            Status = StatusCodes.Status409Conflict,
            Title = "The request was changed by another user",
            Detail = exception.Message
        },

        InvalidStatusTransitionException transition => new ProblemDetails
        {
            Status = StatusCodes.Status422UnprocessableEntity,
            Title = "Status change not allowed",
            Detail = exception.Message,
            Extensions =
            {
                ["currentStatus"] = transition.CurrentStatus.ToString(),
                ["requestedStatus"] = transition.RequestedStatus.ToString(),
                ["allowedStatuses"] = transition.AllowedStatuses.Select(status => status.ToString()).ToArray()
            }
        },

        _ => new ProblemDetails
        {
            Status = StatusCodes.Status500InternalServerError,
            Title = "An unexpected error occurred",
            Detail = "The request could not be completed. The error was logged on the server."
        }
    };
}
