using System.Text.Json.Serialization;
using EmployerRequests.Api.ErrorHandling;
using EmployerRequests.Api.Identity;
using EmployerRequests.Application;
using EmployerRequests.Application.Abstractions;
using EmployerRequests.Infrastructure;
using EmployerRequests.Infrastructure.Data.Seeding;

var builder = WebApplication.CreateBuilder(args);

// --- Services -------------------------------------------------------------

builder.Services
    .AddControllers()
    .AddJsonOptions(options =>
    {
        // Enums travel as names ("InProgress"), not as numbers. A number in the body is rejected (400).
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter(allowIntegerValues: false));
        // On malformed JSON, answer "The input was not valid" with the JSON path – not internal type names.
        options.AllowInputFormatterExceptionMessages = false;
    });

// Every error answer is Problem Details (RFC 9457); exceptions are mapped in one place.
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new()
    {
        Title = "Employer Requests API",
        Version = "v1",
        Description = "Requests from organizations and employers: search, summary, details, history and status changes."
    });

    // The XML comments of the controllers and of the contracts become descriptions in Swagger.
    foreach (var assemblyName in new[] { "EmployerRequests.Api", "EmployerRequests.Application" })
    {
        options.IncludeXmlComments(Path.Combine(AppContext.BaseDirectory, $"{assemblyName}.xml"));
    }
});

// Who changed what – read from the X-User-Name header (the exam has no login).
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, HeaderCurrentUser>();

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

var app = builder.Build();

// --- Database -------------------------------------------------------------

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

    await app.Services.RecreateTestDataAsync(count);
    return 0;
}

// Development convenience: create or upgrade the database on startup, and insert 100,000 requests on the
// first run. In production, migrations run as a separate deployment step.
if (app.Environment.IsDevelopment())
{
    await app.Services.PrepareDatabaseAsync();
}

// --- HTTP pipeline --------------------------------------------------------

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
