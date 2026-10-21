using System.Text.Json.Serialization;
using EmployerRequests.Api.ErrorHandling;
using EmployerRequests.Application.Interfaces;
using EmployerRequests.Application.Services;
using EmployerRequests.Infrastructure.Data;
using EmployerRequests.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi;

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
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Employer Requests API",
        Version = "v1",
        Description = "Requests from organizations and employers: search, summary, details, history and status changes."
    });

    // The XML comments of the controller and of the DTOs become descriptions in Swagger.
    options.IncludeXmlComments(Path.Combine(AppContext.BaseDirectory, "EmployerRequests.Api.xml"));
    options.IncludeXmlComments(Path.Combine(AppContext.BaseDirectory, "EmployerRequests.Application.xml"));
});

// ---------- Our classes (dependency injection) ----------

var connectionString = builder.Configuration.GetConnectionString("Requests");
if (connectionString == null)
{
    throw new InvalidOperationException("Connection string 'Requests' is missing.");
}

// Scoped – one DbContext per HTTP request (the default of AddDbContext).
builder.Services.AddDbContext<AppDbContext>(options =>
{
    options.UseSqlServer(connectionString, sqlServer =>
    {
        // Compatibility level 130 (SQL Server 2016): EF Core generates only SQL that LocalDB 2016 understands;
        // newer servers run it just as well.
        sqlServer.UseCompatibilityLevel(130);

        // A list of values in a query (?status=New&status=Waiting) is written into the SQL itself: Status IN (1, 3).
        // So SQL Server knows exactly which values it looks for and can use the index. (By default, EF Core 9 sends
        // the list as one JSON parameter, and SQL Server has to guess how many rows will match.)
        sqlServer.TranslateParameterizedCollectionsToConstants();

        // A database error that passes by itself – a dropped connection, a deadlock – is tried again up to 3 times,
        // a moment apart, before it becomes a 500. Conflicts and invalid requests are never "transient": no retry.
        sqlServer.EnableRetryOnFailure(maxRetryCount: 3, maxRetryDelay: TimeSpan.FromSeconds(2), errorNumbersToAdd: null);
    });
});

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
    if (args.Length > 1)
    {
        var isNumber = int.TryParse(args[1], out count);
        if (!isNumber || count < 1 || count > TestDataSeeder.MaxCount)
        {
            Console.Error.WriteLine($"The number of requests must be between 1 and {TestDataSeeder.MaxCount:N0}.");
            return 1;
        }
    }

    // TestDataSeeder is scoped (it uses the DbContext), and outside an HTTP request there is no scope –
    // so one is created here.
    await using var seedScope = app.Services.CreateAsyncScope();
    var seeder = seedScope.ServiceProvider.GetRequiredService<TestDataSeeder>();
    await seeder.RecreateAsync(count);
    return 0;
}

// Development convenience: create or upgrade the database on startup, and insert 100,000 requests on the
// first run. In production, migrations run as a separate deployment step.
if (app.Environment.IsDevelopment())
{
    await using var startupScope = app.Services.CreateAsyncScope();
    var seeder = startupScope.ServiceProvider.GetRequiredService<TestDataSeeder>();
    await seeder.PrepareDatabaseAsync();
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
public partial class Program
{
}
