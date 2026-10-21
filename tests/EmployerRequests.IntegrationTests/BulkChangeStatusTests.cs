using System.Net;
using System.Net.Http.Json;
using EmployerRequests.Application.DTOs;
using EmployerRequests.Domain;
using EmployerRequests.IntegrationTests.Helpers;

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
        var willUpdate = requests[0];
        var changedMeanwhile = requests[1];
        var alreadyInProgress = requests[2];

        // After the user selected the requests, someone else changes the second one.
        var otherChange = await _api.CreateClientAs("someone-else")
            .ChangeStatusAsync(changedMeanwhile.Id, RequestStatus.Waiting, changedMeanwhile.RowVersion);
        otherChange.EnsureSuccessStatusCode();

        var bulkChange = new BulkChangeStatusDto
        {
            Status = RequestStatus.InProgress,
            Items = new List<BulkChangeStatusItemDto>
            {
                Item(willUpdate.Id, willUpdate.RowVersion),
                Item(changedMeanwhile.Id, changedMeanwhile.RowVersion),
                Item(alreadyInProgress.Id, alreadyInProgress.RowVersion),
                Item(999_999, willUpdate.RowVersion)
            }
        };
        var response = await _api.CreateClientAs("bulk-user").PatchAsJsonAsync("/api/requests/status", bulkChange, ApiFactory.Json);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.ReadJsonAsync<BulkChangeStatusResultDto>();
        Assert.Equal(
            new[] { BulkItemOutcome.Updated, BulkItemOutcome.Conflict, BulkItemOutcome.InvalidTransition, BulkItemOutcome.NotFound },
            result.Items.Select(item => item.Outcome));
        Assert.Equal(4, result.Requested);
        Assert.Equal(1, result.Succeeded);
        Assert.Equal(3, result.Failed);

        // What the database really holds: the update was saved; the newer change of the other user was not overwritten.
        var savedWillUpdate = await _api.GetSavedRequestAsync(willUpdate.Id);
        var savedChangedMeanwhile = await _api.GetSavedRequestAsync(changedMeanwhile.Id);
        Assert.Equal(RequestStatus.InProgress, savedWillUpdate.Status);
        Assert.Equal(RequestStatus.Waiting, savedChangedMeanwhile.Status);

        var bulkHistory = await _api.GetSavedHistoryByUserAsync("bulk-user");
        var historyRow = Assert.Single(bulkHistory);
        Assert.Equal(willUpdate.Id, historyRow.RequestId);
    }

    [Fact]
    public async Task A_batch_of_more_than_100_requests_is_rejected_and_nothing_changes()
    {
        var requests = await _api.ResetAsync(TestRequests.New());
        var request = requests[0];

        var items = new List<BulkChangeStatusItemDto>();
        for (var i = 0; i < 101; i++)
        {
            items.Add(Item(request.Id + i, request.RowVersion));
        }

        var bulkChange = new BulkChangeStatusDto { Status = RequestStatus.InProgress, Items = items };
        var response = await _api.CreateClient().PatchAsJsonAsync("/api/requests/status", bulkChange, ApiFactory.Json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var saved = await _api.GetSavedRequestAsync(request.Id);
        Assert.Equal(RequestStatus.New, saved.Status);
    }

    [Fact]
    public async Task A_batch_that_lists_the_same_request_twice_is_rejected()
    {
        var requests = await _api.ResetAsync(TestRequests.New());
        var request = requests[0];

        var bulkChange = new BulkChangeStatusDto
        {
            Status = RequestStatus.InProgress,
            Items = new List<BulkChangeStatusItemDto>
            {
                Item(request.Id, request.RowVersion),
                Item(request.Id, request.RowVersion)
            }
        };
        var response = await _api.CreateClient().PatchAsJsonAsync("/api/requests/status", bulkChange, ApiFactory.Json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private static BulkChangeStatusItemDto Item(int id, byte[] rowVersion)
    {
        return new BulkChangeStatusItemDto { Id = id, RowVersion = rowVersion };
    }
}
