using System.Net;
using System.Net.Http.Json;
using EmployerRequests.Application.Contracts;
using EmployerRequests.Domain;
using EmployerRequests.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace EmployerRequests.IntegrationTests;

public class BulkChangeStatusTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _api;

    public BulkChangeStatusTests(ApiFactory api)
    {
        _api = api;
    }

    [Fact]
    public async Task Every_request_in_the_batch_gets_its_own_outcome_and_only_valid_ones_are_saved()
    {
        var requests = await _api.ResetAsync(
            TestRequests.New("will be updated"),
            TestRequests.New("changed by someone else meanwhile"),
            TestRequests.New("already in progress", status: RequestStatus.InProgress));
        var (willUpdate, changedMeanwhile, alreadyInProgress) = (requests[0], requests[1], requests[2]);

        // After the user selected the requests, someone else changes the second one.
        var otherChange = await _api.CreateClientAs("someone-else")
            .ChangeStatusAsync(changedMeanwhile.Id, RequestStatus.Waiting, changedMeanwhile.RowVersion);
        otherChange.EnsureSuccessStatusCode();

        var response = await _api.CreateClientAs("bulk-user").PatchAsJsonAsync("/api/requests/status", new
        {
            status = RequestStatus.InProgress,
            items = new[]
            {
                new { id = willUpdate.Id, rowVersion = willUpdate.RowVersion },
                new { id = changedMeanwhile.Id, rowVersion = changedMeanwhile.RowVersion },
                new { id = alreadyInProgress.Id, rowVersion = alreadyInProgress.RowVersion },
                new { id = 999_999, rowVersion = willUpdate.RowVersion }
            }
        }, ApiFactory.Json);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.ReadJsonAsync<BulkChangeStatusResult>();
        Assert.Equal(
            new[] { BulkItemOutcome.Updated, BulkItemOutcome.Conflict, BulkItemOutcome.InvalidTransition, BulkItemOutcome.NotFound },
            result.Items.Select(item => item.Outcome));
        Assert.Equal(4, result.Requested);
        Assert.Equal(1, result.Succeeded);
        Assert.Equal(3, result.Failed);

        // What the database really holds: the update was saved; the newer change of the other user was not overwritten.
        var stored = await _api.QueryAsync(db => db.Requests.AsNoTracking().ToDictionaryAsync(r => r.Id, r => r.Status));
        Assert.Equal(RequestStatus.InProgress, stored[willUpdate.Id]);
        Assert.Equal(RequestStatus.Waiting, stored[changedMeanwhile.Id]);

        var bulkHistory = await _api.QueryAsync(db => db.RequestStatusChanges.Where(change => change.ChangedBy == "bulk-user").ToListAsync());
        Assert.Equal(willUpdate.Id, Assert.Single(bulkHistory).RequestId);
    }

    [Fact]
    public async Task A_batch_of_more_than_100_requests_is_rejected_and_nothing_changes()
    {
        var request = (await _api.ResetAsync(TestRequests.New()))[0];
        var items = Enumerable.Range(0, 101)
            .Select(i => new { id = request.Id + i, rowVersion = request.RowVersion })
            .ToArray();

        var response = await _api.CreateClient().PatchAsJsonAsync(
            "/api/requests/status", new { status = RequestStatus.InProgress, items }, ApiFactory.Json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var stored = await _api.QueryAsync(db => db.Requests.AsNoTracking().SingleAsync(r => r.Id == request.Id));
        Assert.Equal(RequestStatus.New, stored.Status);
    }

    [Fact]
    public async Task A_batch_that_lists_the_same_request_twice_is_rejected()
    {
        var request = (await _api.ResetAsync(TestRequests.New()))[0];
        var item = new { id = request.Id, rowVersion = request.RowVersion };

        var response = await _api.CreateClient().PatchAsJsonAsync(
            "/api/requests/status", new { status = RequestStatus.InProgress, items = new[] { item, item } }, ApiFactory.Json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
