using System.Net;
using System.Net.Http.Json;
using EmployerRequests.Application.DTOs;
using EmployerRequests.Domain;
using EmployerRequests.IntegrationTests.Helpers;
using Microsoft.AspNetCore.Mvc;

namespace EmployerRequests.IntegrationTests;

public class SearchRequestsTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _api;
    private readonly HttpClient _client;

    public SearchRequestsTests(ApiFactory api)
    {
        _api = api;
        _client = api.CreateClient();
    }

    [Fact]
    public async Task Filters_are_combined_and_the_page_is_sorted_and_cut_by_the_database()
    {
        var day = new DateTimeOffset(2026, 3, 1, 0, 0, 0, TimeSpan.Zero);
        await _api.ResetAsync(
            TestRequests.New("A1", "אלון תעשיות בע\"מ", RequestStatus.New, RequestPriority.High, createdAt: day.AddHours(1)),
            TestRequests.New("A2", "אלון תעשיות בע\"מ", RequestStatus.Waiting, RequestPriority.High, createdAt: day.AddHours(2)),
            TestRequests.New("A3", "אלון מערכות בע\"מ", RequestStatus.New, RequestPriority.High, createdAt: day.AddHours(3)),
            TestRequests.New("wrong status", "אלון תעשיות בע\"מ", RequestStatus.Completed, RequestPriority.High, createdAt: day),
            TestRequests.New("wrong priority", "אלון תעשיות בע\"מ", RequestStatus.New, RequestPriority.Low, createdAt: day),
            TestRequests.New("wrong organization", "ארז מערכות בע\"מ", RequestStatus.New, RequestPriority.High, createdAt: day),
            TestRequests.New("too early", "אלון תעשיות בע\"מ", RequestStatus.New, RequestPriority.High, createdAt: day.AddDays(-1)));

        var page = await _client.GetJsonAsync<PagedResultDto<RequestListItemDto>>(
            "/api/requests?status=New&status=Waiting&priority=High&organizationName=אלון" +
            "&createdFrom=2026-03-01T00:00:00Z&createdTo=2026-03-02T00:00:00Z" +
            "&sortBy=createdAt&sortDirection=desc&page=1&pageSize=2");

        Assert.Equal(3, page.TotalCount);
        Assert.Equal(2, page.TotalPages);
        Assert.Equal(new[] { "A3", "A2" }, page.Items.Select(item => item.Title));
    }

    [Fact]
    public async Task Text_search_finds_the_text_in_the_title_or_in_the_organization_name()
    {
        await _api.ResetAsync(
            TestRequests.New("בקשה לאישור העסקה", "גפן שירותים בע\"מ"),
            TestRequests.New("בירור תשלום", "אישורים ושות' בע\"מ"),
            TestRequests.New("בירור תשלום", "גפן שירותים בע\"מ"));

        var page = await _client.GetJsonAsync<PagedResultDto<RequestListItemDto>>("/api/requests?search=אישור");

        Assert.Equal(2, page.TotalCount);
    }

    [Fact]
    public async Task Text_search_treats_like_wildcards_as_plain_characters()
    {
        await _api.ResetAsync(
            TestRequests.New("הנחה של 50% לעובדים"),
            TestRequests.New("הנחה רגילה"));

        // "%" alone would match everything if it reached LIKE as a wildcard.
        var page = await _client.GetJsonAsync<PagedResultDto<RequestListItemDto>>("/api/requests?search=%25");

        var item = Assert.Single(page.Items);
        Assert.Equal("הנחה של 50% לעובדים", item.Title);
    }

    [Fact]
    public async Task Paging_over_equal_sort_values_returns_every_request_exactly_once()
    {
        // 25 requests created at the same moment: only the Id tie-breaker gives them a stable order.
        var requests = new List<ServiceRequest>();
        for (var i = 1; i <= 25; i++)
        {
            requests.Add(TestRequests.New($"פנייה {i}"));
        }

        await _api.ResetAsync(requests.ToArray());

        var seen = new List<int>();
        for (var page = 1; page <= 3; page++)
        {
            var result = await _client.GetJsonAsync<PagedResultDto<RequestListItemDto>>(
                $"/api/requests?sortBy=createdAt&sortDirection=desc&pageSize=10&page={page}");
            seen.AddRange(result.Items.Select(item => item.Id));
        }

        Assert.Equal(25, seen.Count);
        Assert.Equal(25, seen.Distinct().Count());
    }

    [Theory]
    [InlineData("pageSize=101")]
    [InlineData("page=0")]
    [InlineData("status=99")]
    [InlineData("status=Archived")]
    [InlineData("sortBy=Password")]
    [InlineData("createdFrom=2026-02-01T00:00:00Z&createdTo=2026-01-01T00:00:00Z")]
    public async Task Invalid_query_parameters_return_400_with_the_invalid_field(string queryString)
    {
        var response = await _client.GetAsync($"/api/requests?{queryString}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.NotNull(problem);
        Assert.NotEmpty(problem.Errors);
    }
}
