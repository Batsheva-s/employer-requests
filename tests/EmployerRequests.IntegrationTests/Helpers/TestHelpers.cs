using System.Net.Http.Json;
using EmployerRequests.Application.DTOs;
using EmployerRequests.Domain;

namespace EmployerRequests.IntegrationTests.Helpers;

internal static class TestRequests
{
    /// <summary>A request for a test. Every value has a default, so a test sets only what it checks.</summary>
    public static ServiceRequest New(
        string title = "בקשה לבדיקה",
        string organization = "ארגון בדיקה בע\"מ",
        RequestStatus status = RequestStatus.New,
        RequestPriority priority = RequestPriority.Medium,
        string? assignedTo = "שרה כהן",
        DateTimeOffset? createdAt = null)
    {
        // Without a date, every test request is created at the same fixed moment.
        if (createdAt == null)
        {
            createdAt = new DateTimeOffset(2026, 1, 1, 8, 0, 0, TimeSpan.Zero);
        }

        return new ServiceRequest(title, organization, priority, assignedTo, createdAt.Value, status);
    }
}

/// <summary>Calls to the API that many tests make.</summary>
internal static class ApiClientExtensions
{
    /// <summary>PATCH /api/requests/{id}/status, as the client sends it.</summary>
    public static async Task<HttpResponseMessage> ChangeStatusAsync(this HttpClient client, int id, RequestStatus status, byte[] rowVersion)
    {
        var body = new ChangeStatusDto { Status = status, RowVersion = rowVersion };
        return await client.PatchAsJsonAsync($"/api/requests/{id}/status", body, ApiFactory.Json);
    }

    /// <summary>GET a URL and read its JSON answer.</summary>
    public static async Task<T> GetJsonAsync<T>(this HttpClient client, string url)
    {
        var result = await client.GetFromJsonAsync<T>(url, ApiFactory.Json);
        if (result == null)
        {
            throw new InvalidOperationException($"GET {url} returned no JSON.");
        }

        return result;
    }

    /// <summary>Read the JSON body of an answer.</summary>
    public static async Task<T> ReadJsonAsync<T>(this HttpResponseMessage response)
    {
        var result = await response.Content.ReadFromJsonAsync<T>(ApiFactory.Json);
        if (result == null)
        {
            throw new InvalidOperationException("The answer has no JSON body.");
        }

        return result;
    }
}
