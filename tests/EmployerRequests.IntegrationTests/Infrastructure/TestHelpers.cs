using System.Net.Http.Json;
using EmployerRequests.Domain;

namespace EmployerRequests.IntegrationTests.Infrastructure;

internal static class TestRequests
{
    public static ServiceRequest New(
        string title = "בקשה לבדיקה",
        string organization = "ארגון בדיקה בע\"מ",
        RequestStatus status = RequestStatus.New,
        RequestPriority priority = RequestPriority.Medium,
        string? assignedTo = "שרה כהן",
        DateTimeOffset? createdAt = null) =>
        new(title, organization, priority, assignedTo, createdAt ?? new DateTimeOffset(2026, 1, 1, 8, 0, 0, TimeSpan.Zero), status);
}

internal static class ApiClientExtensions
{
    public static Task<HttpResponseMessage> ChangeStatusAsync(this HttpClient client, int id, RequestStatus status, byte[] rowVersion) =>
        client.PatchAsJsonAsync($"/api/requests/{id}/status", new { status, rowVersion }, ApiFactory.Json);

    public static async Task<T> GetJsonAsync<T>(this HttpClient client, string url) =>
        (await client.GetFromJsonAsync<T>(url, ApiFactory.Json))!;

    public static async Task<T> ReadJsonAsync<T>(this HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<T>(ApiFactory.Json))!;
}
