using System.Net;
using EmployerRequests.Application.Contracts;
using EmployerRequests.Domain;
using EmployerRequests.IntegrationTests.Infrastructure;

namespace EmployerRequests.IntegrationTests;

public class HistoryAndSummaryTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _api;
    private readonly HttpClient _client;

    public HistoryAndSummaryTests(ApiFactory api)
    {
        _api = api;
        _client = api.CreateClient();
    }

    [Fact]
    public async Task History_lists_every_status_change_newest_first()
    {
        var request = (await _api.ResetAsync(TestRequests.New()))[0];
        var user = _api.CreateClientAs("רבקה לוי");

        var afterFirstChange = await (await user.ChangeStatusAsync(request.Id, RequestStatus.InProgress, request.RowVersion))
            .ReadJsonAsync<RequestDetailsDto>();
        (await user.ChangeStatusAsync(request.Id, RequestStatus.Completed, afterFirstChange.RowVersion)).EnsureSuccessStatusCode();

        var history = await _client.GetJsonAsync<List<RequestStatusChangeDto>>($"/api/requests/{request.Id}/history");

        Assert.Collection(
            history,
            newest =>
            {
                Assert.Equal(RequestStatus.InProgress, newest.PreviousStatus);
                Assert.Equal(RequestStatus.Completed, newest.NewStatus);
                Assert.Equal("רבקה לוי", newest.ChangedBy);
            },
            oldest =>
            {
                Assert.Equal(RequestStatus.New, oldest.PreviousStatus);
                Assert.Equal(RequestStatus.InProgress, oldest.NewStatus);
            });
    }

    [Fact]
    public async Task History_of_a_request_that_does_not_exist_returns_404()
    {
        await _api.ResetAsync();

        var response = await _client.GetAsync("/api/requests/999999/history");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task The_summary_counts_exactly_the_requests_the_list_shows()
    {
        await _api.ResetAsync(
            TestRequests.New(status: RequestStatus.New, priority: RequestPriority.High, assignedTo: "שרה כהן"),
            TestRequests.New(status: RequestStatus.InProgress, priority: RequestPriority.High, assignedTo: "שרה כהן"),
            TestRequests.New(status: RequestStatus.Completed, priority: RequestPriority.High, assignedTo: "דוד לוי"),
            TestRequests.New(status: RequestStatus.New, priority: RequestPriority.Low, assignedTo: "דוד לוי"));

        var summary = await _client.GetJsonAsync<RequestsSummaryDto>("/api/requests/summary?priority=High");
        var list = await _client.GetJsonAsync<PagedResult<RequestListItemDto>>("/api/requests?priority=High");

        Assert.Equal(list.TotalCount, summary.TotalCount);
        Assert.Equal(3, summary.TotalCount);
        Assert.Equal(1, CountOf(summary, RequestStatus.New));
        Assert.Equal(1, CountOf(summary, RequestStatus.InProgress));
        Assert.Equal(1, CountOf(summary, RequestStatus.Completed));

        // Only open requests count as workload: דוד לוי's only high-priority request is completed.
        var busiest = Assert.Single(summary.TopAssignees);
        Assert.Equal("שרה כהן", busiest.AssignedTo);
        Assert.Equal(2, busiest.OpenRequests);
    }

    [Fact]
    public async Task The_cached_unfiltered_summary_is_refreshed_right_after_a_status_change()
    {
        var request = (await _api.ResetAsync(TestRequests.New(status: RequestStatus.New)))[0];

        // The unfiltered summary is now calculated and cached.
        var before = await _client.GetJsonAsync<RequestsSummaryDto>("/api/requests/summary");
        Assert.Equal(1, CountOf(before, RequestStatus.New));

        (await _client.ChangeStatusAsync(request.Id, RequestStatus.InProgress, request.RowVersion)).EnsureSuccessStatusCode();

        var after = await _client.GetJsonAsync<RequestsSummaryDto>("/api/requests/summary");
        Assert.Equal(0, CountOf(after, RequestStatus.New));
        Assert.Equal(1, CountOf(after, RequestStatus.InProgress));
    }

    private static int CountOf(RequestsSummaryDto summary, RequestStatus status) =>
        summary.ByStatus.Single(count => count.Status == status).Count;
}
