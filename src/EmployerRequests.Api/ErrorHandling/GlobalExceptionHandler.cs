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
/// <item>499 – the client went away before the answer: not an error.</item>
/// <item>500 – anything unexpected: logged in full, returned without internal details.</item>
/// </list>
/// Invalid input never gets here: [ApiController] answers it with 400 before the action runs.
/// </summary>
public class GlobalExceptionHandler : IExceptionHandler
{
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
        // old search – and the CancellationToken stopped the database query. ASP.NET Core itself answers 499 when
        // that surfaces as an OperationCanceledException; a query stopped in the middle can also come back as a
        // SqlException ("Operation cancelled by user"). Whatever the exception, it is not a server error.
        if (httpContext.RequestAborted.IsCancellationRequested)
        {
            _logger.LogInformation("{Method} {Path} was cancelled by the client", method, path);
            httpContext.Response.StatusCode = StatusCodes.Status499ClientClosedRequest;
            return true;
        }

        var problem = CreateProblem(exception);
        var statusCode = problem.Status ?? StatusCodes.Status500InternalServerError;

        if (statusCode == StatusCodes.Status500InternalServerError)
        {
            // A bug or an infrastructure failure: the full exception goes to the log.
            _logger.LogError(exception, "Unhandled exception for {Method} {Path}", method, path);
        }
        else
        {
            // An expected business answer (404, 409, 422): one line is enough.
            _logger.LogWarning("{StatusCode} for {Method} {Path}: {Detail}", statusCode, method, path, problem.Detail);
        }

        httpContext.Response.StatusCode = statusCode;

        var problemContext = new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = problem,
            Exception = exception
        };
        return await _problemDetailsService.TryWriteAsync(problemContext);
    }

    /// <summary>The answer for each kind of exception.</summary>
    private static ProblemDetails CreateProblem(Exception exception)
    {
        if (exception is NotFoundException)
        {
            return new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Title = "Not found",
                Detail = exception.Message
            };
        }

        if (exception is ConcurrencyConflictException)
        {
            return new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "The request was changed by another user",
                Detail = exception.Message
            };
        }

        if (exception is InvalidStatusTransitionException transition)
        {
            var problem = new ProblemDetails
            {
                Status = StatusCodes.Status422UnprocessableEntity,
                Title = "Status change not allowed",
                Detail = exception.Message
            };

            // Extra fields in the JSON answer, so the client can tell the user what is allowed instead.
            problem.Extensions["currentStatus"] = transition.CurrentStatus.ToString();
            problem.Extensions["requestedStatus"] = transition.RequestedStatus.ToString();
            problem.Extensions["allowedStatuses"] = transition.AllowedStatuses.Select(status => status.ToString()).ToArray();
            return problem;
        }

        // Anything else is unexpected. Its message may contain internal details (server names, SQL), so the
        // answer says only that something went wrong; the details are in the log.
        return new ProblemDetails
        {
            Status = StatusCodes.Status500InternalServerError,
            Title = "An unexpected error occurred",
            Detail = "The request could not be completed. The error was logged on the server."
        };
    }
}
