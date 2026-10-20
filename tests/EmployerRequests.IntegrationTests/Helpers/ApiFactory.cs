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
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly string _connectionString = BuildConnectionString($"EmployerRequestsTests_{Guid.NewGuid():N}");

    public async Task InitializeAsync()
    {
        await using var scope = Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.MigrateAsync();
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await using (var scope = Services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.EnsureDeletedAsync();
        }

        await DisposeAsync();
    }

    /// <summary>Deletes all the data and inserts <paramref name="requests"/>. They come back with the Id and RowVersion the database gave them.</summary>
    public async Task<ServiceRequest[]> ResetAsync(params ServiceRequest[] requests)
    {
        await using var scope = Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        await dbContext.RequestStatusChanges.ExecuteDeleteAsync();
        await dbContext.Requests.ExecuteDeleteAsync();
        dbContext.Requests.AddRange(requests);
        await dbContext.SaveChangesAsync();

        // The data changed behind the API's back, so the cached summary is outdated.
        Services.GetRequiredService<SummaryCache>().Invalidate();
        return requests;
    }

    /// <summary>A client that acts as <paramref name="userName"/> – the name recorded in the history.</summary>
    public HttpClient CreateClientAs(string userName)
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Add(RequestsController.UserNameHeader, Uri.EscapeDataString(userName));
        return client;
    }

    /// <summary>Reads the database directly – to check what was really saved, not what the API says.</summary>
    public async Task<T> QueryAsync<T>(Func<AppDbContext, Task<T>> query)
    {
        await using var scope = Services.CreateAsyncScope();
        return await query(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:Requests", _connectionString);
    }

    private static string BuildConnectionString(string database)
    {
        var configured = Environment.GetEnvironmentVariable("TESTS_SQLSERVER_CONNECTION");

        var connection = string.IsNullOrWhiteSpace(configured)
            ? new SqlConnectionStringBuilder
            {
                DataSource = @"(localdb)\MSSQLLocalDB",
                IntegratedSecurity = true,
                TrustServerCertificate = true
            }
            : new SqlConnectionStringBuilder(configured);

        connection.InitialCatalog = database;
        return connection.ConnectionString;
    }
}
