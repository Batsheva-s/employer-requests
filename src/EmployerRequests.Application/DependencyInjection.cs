using EmployerRequests.Application.Services;
using Microsoft.Extensions.DependencyInjection;

namespace EmployerRequests.Application;

public static class DependencyInjection
{
    /// <summary>Registers the application services.</summary>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        // Singleton: the system clock has no state. Tests can replace it with a fixed time.
        services.AddSingleton(TimeProvider.System);

        // Scoped: one instance per HTTP request, like the DbContext they use through the repository.
        services.AddScoped<IRequestQueryService, RequestQueryService>();
        services.AddScoped<IRequestStatusService, RequestStatusService>();
        services.AddScoped<IRequestSummaryService, RequestSummaryService>();

        return services;
    }
}
