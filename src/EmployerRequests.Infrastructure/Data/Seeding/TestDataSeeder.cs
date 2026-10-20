using System.Data;
using EmployerRequests.Domain;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace EmployerRequests.Infrastructure.Data.Seeding;

/// <summary>
/// Creates the test data: fictional requests generated from a fixed random seed, so every run produces the same
/// requests. Dates are relative to the moment of seeding, so ages such as "opened two days ago" stay realistic.
/// The rows are written with SqlBulkCopy – a few seconds for 100,000 rows, instead of minutes through EF Core.
/// </summary>
public class TestDataSeeder
{
    public const int DefaultCount = 100_000;
    public const int MaxCount = 1_000_000;

    private const int RandomSeed = 20_261_020;
    private const int BatchSize = 10_000;

    // Organization names are combinations of these parts – fictional, not real companies.
    private static readonly string[] OrganizationNames =
        ["אלון", "ארז", "גפן", "דקל", "הדס", "זית", "חרוב", "ערבה", "רימון", "שקד",
         "תאנה", "אורן", "ברוש", "לוטם", "נרקיס", "רקפת", "כלנית", "תמר", "אשל", "שיטה"];

    private static readonly string[] OrganizationFields =
        ["תעשיות", "מערכות", "לוגיסטיקה", "שירותים", "בנייה", "מזון", "אנרגיה", "תקשורת", "חקלאות", "מתכות"];

    private static readonly string[] TitleSubjects =
        ["בקשה לאישור העסקה", "עדכון פרטי מעסיק", "בירור תשלום", "בקשה להארכת מועד", "תלונה על עיכוב בטיפול",
         "בקשה להנפקת אישור", "שינוי כתובת למשלוח דואר", "בקשה לקבלת מידע", "ערעור על החלטה", "דיווח על תקלה במערכת",
         "בקשה לפגישה", "עדכון מספר העובדים", "הגשת מסמכים חסרים", "בקשה לביטול", "בירור מצב בקשה קודמת"];

    private static readonly string[] TitleDetails =
        ["", " – דחוף", " – פנייה חוזרת", " – סניף מרכז", " – סניף צפון", " – סניף דרום", " – רבעון ראשון", " – רבעון שני"];

    private static readonly string[] HandlerFirstNames =
        ["שרה", "רבקה", "רחל", "לאה", "משה", "דוד", "יעקב", "יצחק", "חנה", "אסתר",
         "מרים", "יהודה", "שמעון", "בנימין", "נחמה", "דבורה", "אליהו", "שלמה", "יוסף", "אהרן"];

    private static readonly string[] HandlerLastNames =
        ["כהן", "לוי", "מזרחי", "פרץ", "ביטון", "אברהם", "פרידמן", "שפירא", "גולדברג", "רוזנברג"];

    private const int HandlerCount = 30;

    private readonly AppDbContext _dbContext;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<TestDataSeeder> _logger;

    public TestDataSeeder(AppDbContext dbContext, TimeProvider timeProvider, ILogger<TestDataSeeder> logger)
    {
        _dbContext = dbContext;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    /// <summary>On the first run only: inserts the test data when there are no requests yet.</summary>
    public async Task SeedIfEmptyAsync(CancellationToken cancellationToken)
    {
        if (await _dbContext.Requests.AnyAsync(cancellationToken))
        {
            return;
        }

        await InsertAsync(DefaultCount, cancellationToken);
    }

    /// <summary>Deletes the database, creates it again from the migrations and inserts <paramref name="count"/> requests.</summary>
    public async Task RecreateAsync(int count, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(count, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(count, MaxCount);

        _logger.LogInformation("Deleting the database and creating it again");
        await _dbContext.Database.EnsureDeletedAsync(cancellationToken);
        await _dbContext.Database.MigrateAsync(cancellationToken);

        await InsertAsync(count, cancellationToken);
    }

    private async Task InsertAsync(int count, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Inserting {Count} test requests", count);

        var random = new Random(RandomSeed);
        var now = _timeProvider.GetUtcNow();
        var organizations = BuildOrganizations(random);
        var handlers = BuildHandlers(random);

        var connection = (SqlConnection)_dbContext.Database.GetDbConnection();
        await connection.OpenAsync(cancellationToken);
        try
        {
            for (var inserted = 0; inserted < count; inserted += BatchSize)
            {
                var rows = BuildBatch(Math.Min(BatchSize, count - inserted), random, now, organizations, handlers);

                using var bulkCopy = new SqlBulkCopy(connection) { DestinationTableName = "Requests", BulkCopyTimeout = 300 };
                foreach (DataColumn column in rows.Columns)
                {
                    bulkCopy.ColumnMappings.Add(column.ColumnName, column.ColumnName);
                }

                await bulkCopy.WriteToServerAsync(rows, cancellationToken);
            }
        }
        finally
        {
            await connection.CloseAsync();
        }

        // Inserting many rows into a table that already has indexes leaves them fragmented (page splits).
        // Rebuilding them after the bulk load restores full, ordered pages and refreshes the statistics.
        await _dbContext.Database.ExecuteSqlRawAsync("ALTER INDEX ALL ON Requests REBUILD", cancellationToken);

        _logger.LogInformation("Inserted {Count} test requests", count);
    }

    private static string[] BuildOrganizations(Random random) =>
        OrganizationNames
            .SelectMany(name => OrganizationFields.Select(field => $"{name} {field} בע\"מ"))
            .OrderBy(_ => random.Next())
            .ToArray();

    private static string[] BuildHandlers(Random random) =>
        HandlerFirstNames
            .SelectMany(first => HandlerLastNames.Select(last => $"{first} {last}"))
            .OrderBy(_ => random.Next())
            .Take(HandlerCount)
            .ToArray();

    private static DataTable BuildBatch(int size, Random random, DateTimeOffset now, string[] organizations, string[] handlers)
    {
        var table = new DataTable();
        table.Columns.Add("Title", typeof(string));
        table.Columns.Add("OrganizationName", typeof(string));
        table.Columns.Add("Status", typeof(byte));
        table.Columns.Add("Priority", typeof(byte));
        table.Columns.Add("AssignedTo", typeof(string));
        table.Columns.Add("CreatedAt", typeof(DateTimeOffset));
        table.Columns.Add("UpdatedAt", typeof(DateTimeOffset));

        for (var i = 0; i < size; i++)
        {
            var status = PickStatus(random);
            var priority = PickPriority(random);
            var createdAt = now.AddMinutes(-random.Next(1, 2 * 365 * 24 * 60));   // within the last two years

            // A new request has not been handled yet: often nobody is assigned, and it was never updated.
            var assignedTo = status == RequestStatus.New && random.Next(100) < 60
                ? null
                : handlers[random.Next(handlers.Length)];
            var updatedAt = status == RequestStatus.New
                ? createdAt
                : Earliest(createdAt.AddMinutes(random.Next(1, 30 * 24 * 60)), now);

            var title = TitleSubjects[random.Next(TitleSubjects.Length)] + TitleDetails[random.Next(TitleDetails.Length)];

            table.Rows.Add(
                title,
                organizations[random.Next(organizations.Length)],
                (byte)status,
                (byte)priority,
                (object?)assignedTo ?? DBNull.Value,
                createdAt,
                updatedAt);
        }

        return table;
    }

    // 20% new, 30% in progress, 15% waiting, 35% completed.
    private static RequestStatus PickStatus(Random random) => random.Next(100) switch
    {
        < 20 => RequestStatus.New,
        < 50 => RequestStatus.InProgress,
        < 65 => RequestStatus.Waiting,
        _ => RequestStatus.Completed
    };

    // 40% low, 40% medium, 20% high.
    private static RequestPriority PickPriority(Random random) => random.Next(100) switch
    {
        < 40 => RequestPriority.Low,
        < 80 => RequestPriority.Medium,
        _ => RequestPriority.High
    };

    private static DateTimeOffset Earliest(DateTimeOffset a, DateTimeOffset b) => a < b ? a : b;
}
