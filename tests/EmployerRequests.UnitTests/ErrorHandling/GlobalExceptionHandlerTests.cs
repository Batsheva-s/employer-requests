using EmployerRequests.Api.ErrorHandling;
using EmployerRequests.Application.Exceptions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;

namespace EmployerRequests.UnitTests.ErrorHandling;

public class GlobalExceptionHandlerTests
{
    [Fact]
    public async Task A_conflict_is_answered_409_with_problem_details()
    {
        var (handler, written) = CreateHandler();
        var context = new DefaultHttpContext();

        var handled = await handler.TryHandleAsync(context, new ConcurrencyConflictException(7), CancellationToken.None);

        Assert.True(handled);
        Assert.Equal(StatusCodes.Status409Conflict, context.Response.StatusCode);
        Assert.Equal(StatusCodes.Status409Conflict, written.Problem?.Status);
    }

    [Fact]
    public async Task An_unexpected_error_is_answered_500_without_its_internal_details()
    {
        var (handler, written) = CreateHandler();
        var context = new DefaultHttpContext();

        await handler.TryHandleAsync(context, new InvalidOperationException("Login failed for user 'sa'"), CancellationToken.None);

        Assert.Equal(StatusCodes.Status500InternalServerError, context.Response.StatusCode);
        Assert.NotNull(written.Problem);
        Assert.DoesNotContain("Login failed", written.Problem.Detail);
    }

    [Fact]
    public async Task An_error_after_the_client_went_away_is_not_reported_as_a_server_error()
    {
        // A query stopped in the middle by the CancellationToken can surface as an ordinary database error.
        var (handler, written) = CreateHandler();
        var context = new DefaultHttpContext { RequestAborted = new CancellationToken(canceled: true) };

        var handled = await handler.TryHandleAsync(context, new InvalidOperationException("Operation cancelled by user."), CancellationToken.None);

        Assert.True(handled);
        Assert.Equal(StatusCodes.Status499ClientClosedRequest, context.Response.StatusCode);
        Assert.Null(written.Problem); // nobody is listening: no body is written
    }

    private static (GlobalExceptionHandler Handler, RecordingProblemDetailsService Written) CreateHandler()
    {
        var written = new RecordingProblemDetailsService();
        return (new GlobalExceptionHandler(written, NullLogger<GlobalExceptionHandler>.Instance), written);
    }

    /// <summary>Keeps the Problem Details the handler writes, instead of writing them to a response.</summary>
    private sealed class RecordingProblemDetailsService : IProblemDetailsService
    {
        public ProblemDetails? Problem { get; private set; }

        public ValueTask WriteAsync(ProblemDetailsContext context)
        {
            Problem = context.ProblemDetails;
            return ValueTask.CompletedTask;
        }

        public ValueTask<bool> TryWriteAsync(ProblemDetailsContext context)
        {
            Problem = context.ProblemDetails;
            return ValueTask.FromResult(true);
        }
    }
}
