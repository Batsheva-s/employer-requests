using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using EmployerRequests.Application.Contracts;
using EmployerRequests.Domain;
using EmployerRequests.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

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
        var request = (await _api.ResetAsync(TestRequests.New(status: RequestStatus.New)))[0];

        var response = await _api.CreateClientAs("שרה כהן").ChangeStatusAsync(request.Id, RequestStatus.InProgress, request.RowVersion);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await response.ReadJsonAsync<RequestDetailsDto>();
        Assert.Equal(RequestStatus.InProgress, updated.Status);
        Assert.NotEqual(request.RowVersion, updated.RowVersion);
        Assert.True(updated.UpdatedAt > request.UpdatedAt);
        Assert.Equal(new[] { RequestStatus.Waiting, RequestStatus.Completed }, updated.AllowedNextStatuses);

        var history = await _api.QueryAsync(db => db.RequestStatusChanges.Where(change => change.RequestId == request.Id).ToListAsync());
        var change = Assert.Single(history);
        Assert.Equal(RequestStatus.New, change.PreviousStatus);
        Assert.Equal(RequestStatus.InProgress, change.NewStatus);
        Assert.Equal("שרה כהן", change.ChangedBy);
    }

    [Fact]
    public async Task A_change_based_on_an_old_version_gets_409_and_does_not_overwrite_the_newer_change()
    {
        var request = (await _api.ResetAsync(TestRequests.New()))[0];

        var first = await _api.CreateClientAs("first").ChangeStatusAsync(request.Id, RequestStatus.InProgress, request.RowVersion);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        // The second user still holds the version from before the first change.
        var second = await _api.CreateClientAs("second").ChangeStatusAsync(request.Id, RequestStatus.Waiting, request.RowVersion);

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        var stored = await _api.QueryAsync(db => db.Requests.AsNoTracking().SingleAsync(r => r.Id == request.Id));
        Assert.Equal(RequestStatus.InProgress, stored.Status);
        var history = await _api.QueryAsync(db => db.RequestStatusChanges.Where(change => change.RequestId == request.Id).ToListAsync());
        Assert.Equal("first", Assert.Single(history).ChangedBy);
    }

    [Fact]
    public async Task Of_many_simultaneous_changes_with_the_same_version_exactly_one_is_saved()
    {
        var request = (await _api.ResetAsync(TestRequests.New()))[0];

        // Ten users press "save" at the same moment, all holding the same version.
        var responses = await Task.WhenAll(Enumerable.Range(1, 10).Select(user =>
            _api.CreateClientAs($"user-{user}").ChangeStatusAsync(
                request.Id,
                user % 2 == 0 ? RequestStatus.InProgress : RequestStatus.Waiting,
                request.RowVersion)));

        Assert.Single(responses, response => response.StatusCode == HttpStatusCode.OK);
        Assert.All(
            responses.Where(response => response.StatusCode != HttpStatusCode.OK),
            response => Assert.Equal(HttpStatusCode.Conflict, response.StatusCode));

        // Exactly one change reached the database – no lost update, no double history.
        var history = await _api.QueryAsync(db => db.RequestStatusChanges.Where(change => change.RequestId == request.Id).ToListAsync());
        Assert.Single(history);
    }

    [Fact]
    public async Task A_move_the_workflow_does_not_allow_returns_422_with_the_allowed_statuses()
    {
        var request = (await _api.ResetAsync(TestRequests.New(status: RequestStatus.New)))[0];

        var response = await _api.CreateClient().ChangeStatusAsync(request.Id, RequestStatus.Completed, request.RowVersion);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var problem = await response.ReadJsonAsync<ProblemDetails>();
        var allowed = ((JsonElement)problem.Extensions["allowedStatuses"]!).EnumerateArray().Select(status => status.GetString());
        Assert.Equal(new[] { "InProgress", "Waiting" }, allowed);
    }

    [Fact]
    public async Task Changing_a_request_that_does_not_exist_returns_404()
    {
        await _api.ResetAsync();

        var response = await _api.CreateClient().ChangeStatusAsync(999_999, RequestStatus.InProgress, [0, 0, 0, 0, 0, 0, 0, 1]);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Theory]
    [InlineData("""{ "status": "InProgress" }""")]                              // no version
    [InlineData("""{ "status": "Archived", "rowVersion": "AAAAAAAAB9E=" }""")]  // not a status
    [InlineData("""{ "status": 2, "rowVersion": "AAAAAAAAB9E=" }""")]           // a number instead of a name
    [InlineData("""{ "status": "InProgress", "rowVersion": "AAAA" }""")]        // a version is 8 bytes
    public async Task An_invalid_body_returns_400_and_changes_nothing(string body)
    {
        var request = (await _api.ResetAsync(TestRequests.New()))[0];

        var response = await _api.CreateClient().PatchAsync(
            $"/api/requests/{request.Id}/status",
            new StringContent(body, Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.NotNull(await response.Content.ReadFromJsonAsync<ValidationProblemDetails>());
        var stored = await _api.QueryAsync(db => db.Requests.AsNoTracking().SingleAsync(r => r.Id == request.Id));
        Assert.Equal(RequestStatus.New, stored.Status);
    }
}
