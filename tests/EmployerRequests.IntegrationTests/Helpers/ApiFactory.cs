using System.Text.Json;
using System.Text.Json.Serialization;
using EmployerRequests.Api.Controllers;
using EmployerRequests.Application.Services;
using EmployerRequests.Domain;
using EmployerRequests.Infrastructure.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EmployerRequests.IntegrationTests.Helpers;

/// <summary>
/// Runs the real API in memory against a real SQL Server database – HTTP, validation, services, EF Core,
/// rowversion and the schema are all tested together. (EF Core's in-memory provider does not generate rowversion
/// values, so a concurrency test against it would not test anything.)
/// Every test class gets its own database, created from the real migrations and deleted at the end, so the
/// test classes run in parallel without seeing each other's data.
/// The server: the TESTS_SQLSERVER_CONNECTION environment variable (for example the CI container), otherwise LocalDB.
/// </summary>
public class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    /// <summary>The same JSON as the API: camelCase names, and enums as names ("InProgress").</summary>
    public static readonly JsonSerializerOptions Json = CreateJsonOptions();

    // A new database name for every test class.
    private readonly string _connectionString = BuildConnectionString($"EmployerRequestsTests_{Guid.NewGuid():N}");

    /// <summary>Runs before the first test of the class: creates the database from the migrations.</summary>
    public async Task InitializeAsync()
    {
        await using var scope = Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await dbContext.Database.MigrateAsync();
    }

    /// <summary>Runs after the last test of the class: deletes the database, then stops the API.</summary>
    /// <remarks>
    /// Written as IAsyncLifetime.DisposeAsync because WebApplicationFactory already has a DisposeAsync of its own –
    /// the one called at the end.
    /// </remarks>
    async Task IAsyncLifetime.DisposeAsync()
    {
        await using (var scope = Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await dbContext.Database.EnsureDeletedAsync();
        }

        await DisposeAsync();
    }

    /// <summary>
    /// Deletes all the data and inserts <paramref name="requests"/>.
    /// They come back with the Id and RowVersion the database gave them.
    /// </summary>
    public async Task<ServiceRequest[]> ResetAsync(params ServiceRequest[] requests)
    {
        await using var scope = Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        await dbContext.RequestStatusChanges.ExecuteDeleteAsync();
        await dbContext.Requests.ExecuteDeleteAsync();
        dbContext.Requests.AddRange(requests);
        await dbContext.SaveChangesAsync();

        // The data changed behind the API's back, so the cached summary is outdated.
        var summaryCache = Services.GetRequiredService<SummaryCache>();
        summaryCache.Invalidate();

        return requests;
    }

    /// <summary>A client that acts as <paramref name="userName"/> – the name recorded in the history.</summary>
    public HttpClient CreateClientAs(string userName)
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Add(RequestsController.UserNameHeader, Uri.EscapeDataString(userName));
        return client;
    }

    // ---------- Reading the database directly: what was really saved, not what the API says ----------

    public async Task<ServiceRequest> GetSavedRequestAsync(int id)
    {
        await using var scope = Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await dbContext.Requests.AsNoTracking().SingleAsync(request => request.Id == id);
    }

    public async Task<List<RequestStatusChange>> GetSavedHistoryAsync(int requestId)
    {
        await using var scope = Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await dbContext.RequestStatusChanges.AsNoTracking().Where(change => change.RequestId == requestId).ToListAsync();
    }

    public async Task<List<RequestStatusChange>> GetSavedHistoryByUserAsync(string changedBy)
    {
        await using var scope = Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await dbContext.RequestStatusChanges.AsNoTracking().Where(change => change.ChangedBy == changedBy).ToListAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:Requests", _connectionString);
    }

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }

    private static string BuildConnectionString(string database)
    {
        var configured = Environment.GetEnvironmentVariable("TESTS_SQLSERVER_CONNECTION");

        SqlConnectionStringBuilder connection;
        if (string.IsNullOrWhiteSpace(configured))
        {
            // Nothing configured: LocalDB, which comes with Visual Studio.
            connection = new SqlConnectionStringBuilder();
            connection.DataSource = @"(localdb)\MSSQLLocalDB";
            connection.IntegratedSecurity = true;
            connection.TrustServerCertificate = true;
        }
        else
        {
            connection = new SqlConnectionStringBuilder(configured);
        }

        connection.InitialCatalog = database;
        return connection.ConnectionString;
    }
}
