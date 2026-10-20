using System.Text.Json.Serialization;
using EmployerRequests.Api.ErrorHandling;
using EmployerRequests.Application.Interfaces;
using EmployerRequests.Application.Services;
using EmployerRequests.Infrastructure.Data;
using EmployerRequests.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// ---------- Controllers and JSON ----------

builder.Services
    .AddControllers()
    .AddJsonOptions(options =>
    {
        // Enums travel as names ("InProgress"), not as numbers. A number in the body is rejected (400).
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter(allowIntegerValues: false));
        // On malformed JSON, answer "The input was not valid" with the JSON path – not internal type names.
        options.AllowInputFormatterExceptionMessages = false;
    });

// ---------- Errors: every error answer is Problem Details (RFC 9457), built in one place ----------

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

// ---------- Swagger ----------

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new()
    {
        Title = "Employer Requests API",
        Version = "v1",
        Description = "Requests from organizations and employers: search, summary, details, history and status changes."
    });

    // The XML comments of the controller and of the DTOs become descriptions in Swagger.
    foreach (var assemblyName in new[] { "EmployerRequests.Api", "EmployerRequests.Application" })
    {
        options.IncludeXmlComments(Path.Combine(AppContext.BaseDirectory, $"{assemblyName}.xml"));
    }
});

// ---------- Our classes (dependency injection) ----------

var connectionString = builder.Configuration.GetConnectionString("Requests")
    ?? throw new InvalidOperationException("Connection string 'Requests' is missing.");

// Scoped – one DbContext per HTTP request (the default of AddDbContext). Compatibility level 130 (SQL Server 2016)
// makes EF Core generate only SQL that LocalDB 2016 understands; newer servers run it just as well.
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(connectionString, sqlServer => sqlServer.UseCompatibilityLevel(130)));

// Scoped, like the DbContext they use: one instance per HTTP request.
builder.Services.AddScoped<IRequestRepository, RequestRepository>();
builder.Services.AddScoped<IRequestService, RequestService>();
builder.Services.AddScoped<TestDataSeeder>();

// Singleton: one cached summary for the whole application, shared by all requests.
builder.Services.AddMemoryCache();
builder.Services.AddSingleton<SummaryCache>();

var app = builder.Build();

// ---------- Database ----------

// "dotnet run -- seed [count]": deletes the database, creates it again with fresh test data, and exits.
if (args.Length > 0 && args[0] == "seed")
{
    if (!app.Environment.IsDevelopment())
    {
        Console.Error.WriteLine("The seed command deletes all data, so it runs only in the Development environment.");
        return 1;
    }

    var count = TestDataSeeder.DefaultCount;
    if (args.Length > 1 && (!int.TryParse(args[1], out count) || count < 1 || count > TestDataSeeder.MaxCount))
    {
        Console.Error.WriteLine($"The number of requests must be between 1 and {TestDataSeeder.MaxCount:N0}.");
        return 1;
    }

    await using var seedScope = app.Services.CreateAsyncScope();
    await seedScope.ServiceProvider.GetRequiredService<TestDataSeeder>().RecreateAsync(count);
    return 0;
}

// Development convenience: create or upgrade the database on startup, and insert 100,000 requests on the
// first run. In production, migrations run as a separate deployment step.
if (app.Environment.IsDevelopment())
{
    await using var startupScope = app.Services.CreateAsyncScope();
    await startupScope.ServiceProvider.GetRequiredService<TestDataSeeder>().PrepareDatabaseAsync();
}

// ---------- HTTP pipeline ----------

app.UseExceptionHandler();
// Answers without a body (for example, 404 for an unknown URL) also get a Problem Details body.
app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapControllers();

await app.RunAsync();
return 0;

// Lets the integration tests start the real application with WebApplicationFactory<Program>.
public partial class Program;
