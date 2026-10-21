using EmployerRequests.Api.ErrorHandling;
using EmployerRequests.Application.Exceptions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;

namespace EmployerRequests.UnitTests.ErrorHandling;

public class GlobalExceptionHandlerTests
{
    // xUnit creates a new instance of this class for every test, so every test starts with fresh objects.
    private readonly RecordingProblemDetailsService _written = new RecordingProblemDetailsService();
    private readonly GlobalExceptionHandler _handler;

    public GlobalExceptionHandlerTests()
    {
        _handler = new GlobalExceptionHandler(_written, NullLogger<GlobalExceptionHandler>.Instance);
    }

    [Fact]
    public async Task A_conflict_is_answered_409_with_problem_details()
    {
        var context = new DefaultHttpContext();

        var handled = await _handler.TryHandleAsync(context, new ConcurrencyConflictException(7), CancellationToken.None);

        Assert.True(handled);
        Assert.Equal(StatusCodes.Status409Conflict, context.Response.StatusCode);
        Assert.NotNull(_written.Problem);
        Assert.Equal(StatusCodes.Status409Conflict, _written.Problem.Status);
    }

    [Fact]
    public async Task An_unexpected_error_is_answered_500_without_its_internal_details()
    {
        var context = new DefaultHttpContext();

        await _handler.TryHandleAsync(context, new InvalidOperationException("Login failed for user 'sa'"), CancellationToken.None);

        Assert.Equal(StatusCodes.Status500InternalServerError, context.Response.StatusCode);
        Assert.NotNull(_written.Problem);
        Assert.DoesNotContain("Login failed", _written.Problem.Detail);
    }

    [Fact]
    public async Task An_error_after_the_client_went_away_is_not_reported_as_a_server_error()
    {
        // A query stopped in the middle by the CancellationToken can surface as an ordinary database error.
        var context = new DefaultHttpContext();
        context.RequestAborted = new CancellationToken(canceled: true);

        var handled = await _handler.TryHandleAsync(context, new InvalidOperationException("Operation cancelled by user."), CancellationToken.None);

        Assert.True(handled);
        Assert.Equal(StatusCodes.Status499ClientClosedRequest, context.Response.StatusCode);
        Assert.Null(_written.Problem); // nobody is listening: no body is written
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
