using System.Net;
using System.Net.Http.Json;
using System.Text;
using EmployerRequests.Application.DTOs;
using EmployerRequests.Application.Exceptions;
using EmployerRequests.Application.Interfaces;
using EmployerRequests.Domain;
using EmployerRequests.IntegrationTests.Helpers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace EmployerRequests.IntegrationTests;

public class ChangeStatusTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _api;

    public ChangeStatusTests(ApiFactory api)
    {
        _api = api;
    }

    [Fact]
    public async Task A_valid_change_is_saved_with_a_new_version_and_a_history_row()
    {
        var requests = await _api.ResetAsync(TestRequests.New(status: RequestStatus.New));
        var request = requests[0];

        var client = _api.CreateClientAs("שרה כהן");
        var response = await client.ChangeStatusAsync(request.Id, RequestStatus.InProgress, request.RowVersion);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await response.ReadJsonAsync<RequestDetailsDto>();
        Assert.Equal(RequestStatus.InProgress, updated.Status);
        Assert.NotEqual(request.RowVersion, updated.RowVersion);
        Assert.True(updated.UpdatedAt > request.UpdatedAt);
        Assert.Equal(new[] { RequestStatus.Waiting, RequestStatus.Completed }, updated.AllowedNextStatuses);

        var history = await _api.GetSavedHistoryAsync(request.Id);
        var change = Assert.Single(history);
        Assert.Equal(RequestStatus.New, change.PreviousStatus);
        Assert.Equal(RequestStatus.InProgress, change.NewStatus);
        Assert.Equal("שרה כהן", change.ChangedBy);
    }

    [Fact]
    public async Task A_change_based_on_an_old_version_gets_409_and_does_not_overwrite_the_newer_change()
    {
        var requests = await _api.ResetAsync(TestRequests.New());
        var request = requests[0];

        var first = await _api.CreateClientAs("first").ChangeStatusAsync(request.Id, RequestStatus.InProgress, request.RowVersion);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        // The second user still holds the version from before the first change.
        var second = await _api.CreateClientAs("second").ChangeStatusAsync(request.Id, RequestStatus.Waiting, request.RowVersion);

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        var saved = await _api.GetSavedRequestAsync(request.Id);
        Assert.Equal(RequestStatus.InProgress, saved.Status);
        var history = await _api.GetSavedHistoryAsync(request.Id);
        var change = Assert.Single(history);
        Assert.Equal("first", change.ChangedBy);
    }

    [Fact]
    public async Task Of_many_simultaneous_changes_with_the_same_version_exactly_one_is_saved()
    {
        var requests = await _api.ResetAsync(TestRequests.New());
        var request = requests[0];

        // Ten users press "save" at the same moment, all holding the same version.
        // Each call starts sending at once; Task.WhenAll waits until all ten have answered.
        var saves = new List<Task<HttpResponseMessage>>();
        for (var user = 1; user <= 10; user++)
        {
            RequestStatus newStatus;
            if (user % 2 == 0)
            {
                newStatus = RequestStatus.InProgress;
            }
            else
            {
                newStatus = RequestStatus.Waiting;
            }

            var client = _api.CreateClientAs($"user-{user}");
            saves.Add(client.ChangeStatusAsync(request.Id, newStatus, request.RowVersion));
        }

        var responses = await Task.WhenAll(saves);

        var succeeded = responses.Count(response => response.StatusCode == HttpStatusCode.OK);
        var conflicts = responses.Count(response => response.StatusCode == HttpStatusCode.Conflict);
        Assert.Equal(1, succeeded);
        Assert.Equal(9, conflicts);

        // Exactly one change reached the database – no lost update, no double history. Depending on timing, a
        // loser is stopped by the version check in the service or by the database itself; the next test checks
        // the database alone.
        var history = await _api.GetSavedHistoryAsync(request.Id);
        Assert.Single(history);
    }

    [Fact]
    public async Task A_save_whose_version_changed_after_loading_is_rejected_by_the_database()
    {
        var requests = await _api.ResetAsync(TestRequests.New());
        var request = requests[0];

        // A slow change, made exactly as the service makes it: the request is loaded and the version check passes.
        await using var scope = _api.Services.CreateAsyncScope();
        var repository = scope.ServiceProvider.GetRequiredService<IRequestRepository>();
        var loaded = await repository.GetForUpdateAsync(request.Id, CancellationToken.None);
        Assert.NotNull(loaded);
        Assert.Equal(request.RowVersion, loaded.RowVersion);
        loaded.ChangeStatus(RequestStatus.Waiting, "slow", DateTimeOffset.UtcNow);

        // Before it is saved, another user changes the same request through the API.
        var fast = await _api.CreateClientAs("fast").ChangeStatusAsync(request.Id, RequestStatus.InProgress, request.RowVersion);
        Assert.Equal(HttpStatusCode.OK, fast.StatusCode);

        // Only the database can see this: UPDATE ... WHERE RowVersion = <the version loaded above> matches no row.
        await Assert.ThrowsAsync<ConcurrencyConflictException>(() => repository.SaveChangesAsync(CancellationToken.None));

        // Nothing of the slow change was saved – neither the status nor its history row.
        var saved = await _api.GetSavedRequestAsync(request.Id);
        Assert.Equal(RequestStatus.InProgress, saved.Status);
        var history = await _api.GetSavedHistoryAsync(request.Id);
        var change = Assert.Single(history);
        Assert.Equal("fast", change.ChangedBy);
    }

    [Fact]
    public async Task A_move_the_workflow_does_not_allow_returns_422_with_the_allowed_statuses()
    {
        var requests = await _api.ResetAsync(TestRequests.New(status: RequestStatus.New));
        var request = requests[0];

        var response = await _api.CreateClient().ChangeStatusAsync(request.Id, RequestStatus.Completed, request.RowVersion);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var answer = await response.ReadJsonAsync<InvalidTransitionAnswer>();
        Assert.Equal("New", answer.CurrentStatus);
        Assert.Equal(new[] { "InProgress", "Waiting" }, answer.AllowedStatuses);
    }

    [Fact]
    public async Task Changing_a_request_that_does_not_exist_returns_404()
    {
        await _api.ResetAsync();

        var anyVersion = new byte[] { 0, 0, 0, 0, 0, 0, 0, 1 };
        var response = await _api.CreateClient().ChangeStatusAsync(999_999, RequestStatus.InProgress, anyVersion);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // """...""" is a raw string: the quotes of the JSON need no escaping inside it.
    [Theory]
    [InlineData("""{ "status": "InProgress" }""")]                              // no version
    [InlineData("""{ "status": "Archived", "rowVersion": "AAAAAAAAB9E=" }""")]  // not a status
    [InlineData("""{ "status": 2, "rowVersion": "AAAAAAAAB9E=" }""")]           // a number instead of a name
    [InlineData("""{ "status": "InProgress", "rowVersion": "AAAA" }""")]        // a version is 8 bytes
    public async Task An_invalid_body_returns_400_and_changes_nothing(string body)
    {
        var requests = await _api.ResetAsync(TestRequests.New());
        var request = requests[0];

        var content = new StringContent(body, Encoding.UTF8, "application/json");
        var response = await _api.CreateClient().PatchAsync($"/api/requests/{request.Id}/status", content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.NotNull(problem);
        var saved = await _api.GetSavedRequestAsync(request.Id);
        Assert.Equal(RequestStatus.New, saved.Status);
    }

    /// <summary>The fields of the 422 answer that the test reads (extra fields of its Problem Details).</summary>
    public class InvalidTransitionAnswer
    {
        public string? CurrentStatus { get; set; }

        public string[]? AllowedStatuses { get; set; }
    }
}
