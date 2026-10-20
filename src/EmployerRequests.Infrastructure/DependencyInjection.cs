using EmployerRequests.Application.Abstractions;
using EmployerRequests.Infrastructure.Caching;
using EmployerRequests.Infrastructure.Data;
using EmployerRequests.Infrastructure.Data.Seeding;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace EmployerRequests.Infrastructure;

public static class DependencyInjection
{
    /// <summary>Registers data access (EF Core + SQL Server), the summary cache and the test data seeder.</summary>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Requests")
            ?? throw new InvalidOperationException("Connection string 'Requests' is missing.");

        // Scoped (one per HTTP request) – the default of AddDbContext. Compatibility level 130 (SQL Server 2016)
        // makes EF Core generate only SQL that LocalDB 2016 understands; newer servers run it just as well.
        services.AddDbContext<AppDbContext>(options =>
            options.UseSqlServer(connectionString, sqlServer => sqlServer.UseCompatibilityLevel(130)));

        services.AddScoped<IRequestRepository, RequestRepository>();
        services.AddScoped<TestDataSeeder>();

        services.AddMemoryCache();
        services.Configure<SummaryCacheOptions>(configuration.GetSection(SummaryCacheOptions.SectionName));
        // Singleton: the cached summary and its version counter must be shared by all requests.
        services.AddSingleton<ISummaryCache, MemorySummaryCache>();

        return services;
    }

    /// <summary>Applies pending migrations, and on the first run inserts the test data.</summary>
    public static async Task PrepareDatabaseAsync(this IServiceProvider services, CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();

        await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.MigrateAsync(cancellationToken);
        await scope.ServiceProvider.GetRequiredService<TestDataSeeder>().SeedIfEmptyAsync(cancellationToken);
    }

    /// <summary>Creates the database again with <paramref name="count"/> fresh test requests.</summary>
    public static async Task RecreateTestDataAsync(this IServiceProvider services, int count, CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();

        await scope.ServiceProvider.GetRequiredService<TestDataSeeder>().RecreateAsync(count, cancellationToken);

        // The data changed behind the API's back.
        services.GetRequiredService<ISummaryCache>().Invalidate();
    }
}
