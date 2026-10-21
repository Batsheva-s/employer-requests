using System.Data;
using EmployerRequests.Domain;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace EmployerRequests.Infrastructure.Data;

/// <summary>
/// Creates the test data the exam asks for: at least 100,000 requests, and the ability to create them again.
/// The requests are fictional, generated from a fixed random seed, so every run produces the same requests. Dates are
/// relative to the moment of seeding, so ages such as "opened two days ago" stay realistic.
/// The rows are written with SqlBulkCopy – a few seconds for 100,000 rows, instead of minutes through EF Core.
/// The status history starts empty: it records the changes made through the system from now on.
/// </summary>
public class TestDataSeeder
{
    public const int DefaultCount = 100_000;
    public const int MaxCount = 1_000_000;

    // The same seed gives the same sequence of random numbers – and so the same requests – on every run.
    private const int RandomSeed = 20_261_020;
    private const int BatchSize = 10_000;
    private const int HandlerCount = 30;

    // Organization names are combinations of these parts – fictional, not real companies.
    private static readonly string[] OrganizationNames = new[]
    {
        "אלון", "ארז", "גפן", "דקל", "הדס", "זית", "חרוב", "ערבה", "רימון", "שקד",
        "תאנה", "אורן", "ברוש", "לוטם", "נרקיס", "רקפת", "כלנית", "תמר", "אשל", "שיטה"
    };

    private static readonly string[] OrganizationFields = new[]
    {
        "תעשיות", "מערכות", "לוגיסטיקה", "שירותים", "בנייה", "מזון", "אנרגיה", "תקשורת", "חקלאות", "מתכות"
    };

    private static readonly string[] TitleSubjects = new[]
    {
        "בקשה לאישור העסקה", "עדכון פרטי מעסיק", "בירור תשלום", "בקשה להארכת מועד", "תלונה על עיכוב בטיפול",
        "בקשה להנפקת אישור", "שינוי כתובת למשלוח דואר", "בקשה לקבלת מידע", "ערעור על החלטה", "דיווח על תקלה במערכת",
        "בקשה לפגישה", "עדכון מספר העובדים", "הגשת מסמכים חסרים", "בקשה לביטול", "בירור מצב בקשה קודמת"
    };

    private static readonly string[] TitleDetails = new[]
    {
        "", " – דחוף", " – פנייה חוזרת", " – סניף מרכז", " – סניף צפון", " – סניף דרום", " – רבעון ראשון", " – רבעון שני"
    };

    private static readonly string[] HandlerFirstNames = new[]
    {
        "שרה", "רבקה", "רחל", "לאה", "משה", "דוד", "יעקב", "יצחק", "חנה", "אסתר",
        "מרים", "יהודה", "שמעון", "בנימין", "נחמה", "דבורה", "אליהו", "שלמה", "יוסף", "אהרן"
    };

    private static readonly string[] HandlerLastNames = new[]
    {
        "כהן", "לוי", "מזרחי", "פרץ", "ביטון", "אברהם", "פרידמן", "שפירא", "גולדברג", "רוזנברג"
    };

    private readonly AppDbContext _dbContext;
    private readonly ILogger<TestDataSeeder> _logger;

    public TestDataSeeder(AppDbContext dbContext, ILogger<TestDataSeeder> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    /// <summary>
    /// Runs on every start in Development: creates or upgrades the database (migrations), and on the first run –
    /// when there are no requests yet – inserts the test data.
    /// </summary>
    public async Task PrepareDatabaseAsync(CancellationToken cancellationToken = default)
    {
        await _dbContext.Database.MigrateAsync(cancellationToken);

        var hasRequests = await _dbContext.Requests.AnyAsync(cancellationToken);
        if (!hasRequests)
        {
            await InsertAsync(DefaultCount, cancellationToken);
        }
    }

    /// <summary>
    /// The "seed" command: deletes the database, creates it again from the migrations and inserts
    /// <paramref name="count"/> requests.
    /// </summary>
    public async Task RecreateAsync(int count, CancellationToken cancellationToken = default)
    {
        if (count < 1 || count > MaxCount)
        {
            throw new ArgumentOutOfRangeException(nameof(count), $"The number of requests must be between 1 and {MaxCount}.");
        }

        _logger.LogInformation("Deleting the database and creating it again");
        await _dbContext.Database.EnsureDeletedAsync(cancellationToken);
        await _dbContext.Database.MigrateAsync(cancellationToken);

        await InsertAsync(count, cancellationToken);
    }

    private async Task InsertAsync(int count, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Inserting {Count} test requests", count);

        var random = new Random(RandomSeed);
        var now = DateTimeOffset.UtcNow;
        var organizations = BuildOrganizations(random);
        var handlers = BuildHandlers(random);

        // SqlBulkCopy works on the SQL Server connection that EF Core already holds.
        var connection = (SqlConnection)_dbContext.Database.GetDbConnection();
        await connection.OpenAsync(cancellationToken);
        try
        {
            for (var inserted = 0; inserted < count; inserted += BatchSize)
            {
                // The last batch may be smaller than the others.
                var batchSize = Math.Min(BatchSize, count - inserted);
                var rows = BuildBatch(batchSize, random, now, organizations, handlers);

                using var bulkCopy = new SqlBulkCopy(connection);
                bulkCopy.DestinationTableName = "Requests";
                bulkCopy.BulkCopyTimeout = 300;

                // Every column of the batch goes to the table column with the same name.
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
        // For a million rows this can take longer than the default command timeout of 30 seconds.
        _dbContext.Database.SetCommandTimeout(TimeSpan.FromMinutes(5));
        await _dbContext.Database.ExecuteSqlRawAsync("ALTER INDEX ALL ON Requests REBUILD", cancellationToken);

        _logger.LogInformation("Inserted {Count} test requests", count);
    }

    /// <summary>Every name with every field ("אלון תעשיות בע"מ", ...), in a random order.</summary>
    private static string[] BuildOrganizations(Random random)
    {
        var organizations = new List<string>();
        foreach (var name in OrganizationNames)
        {
            foreach (var field in OrganizationFields)
            {
                organizations.Add($"{name} {field} בע\"מ");
            }
        }

        // Shuffle: every organization gets a random number, and they are sorted by it.
        return organizations.OrderBy(organization => random.Next()).ToArray();
    }

    /// <summary>30 handlers, chosen at random from every first name with every last name.</summary>
    private static string[] BuildHandlers(Random random)
    {
        var names = new List<string>();
        foreach (var firstName in HandlerFirstNames)
        {
            foreach (var lastName in HandlerLastNames)
            {
                names.Add($"{firstName} {lastName}");
            }
        }

        // Shuffle (as above) and keep the first 30.
        return names.OrderBy(name => random.Next()).Take(HandlerCount).ToArray();
    }

    /// <summary>
    /// One batch of requests, as a DataTable for SqlBulkCopy.
    /// The order of the random calls must not change: it is what makes every run produce the same requests.
    /// </summary>
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

            // Created at a random minute within the last two years.
            var createdAt = now.AddMinutes(-random.Next(1, 2 * 365 * 24 * 60));

            // A new request has not been handled yet: often (60%) nobody is assigned.
            string? assignedTo;
            if (status == RequestStatus.New && random.Next(100) < 60)
            {
                assignedTo = null;
            }
            else
            {
                assignedTo = handlers[random.Next(handlers.Length)];
            }

            // A new request was never updated. Any other request was updated up to 30 days after it was created –
            // but never in the future.
            DateTimeOffset updatedAt;
            if (status == RequestStatus.New)
            {
                updatedAt = createdAt;
            }
            else
            {
                updatedAt = createdAt.AddMinutes(random.Next(1, 30 * 24 * 60));
                if (updatedAt > now)
                {
                    updatedAt = now;
                }
            }

            var subject = TitleSubjects[random.Next(TitleSubjects.Length)];
            var detail = TitleDetails[random.Next(TitleDetails.Length)];
            var title = subject + detail;

            var organization = organizations[random.Next(organizations.Length)];

            // A DataTable marks "no value" with DBNull, not with null.
            object assignedToValue;
            if (assignedTo == null)
            {
                assignedToValue = DBNull.Value;
            }
            else
            {
                assignedToValue = assignedTo;
            }

            table.Rows.Add(title, organization, (byte)status, (byte)priority, assignedToValue, createdAt, updatedAt);
        }

        return table;
    }

    // 20% new, 30% in progress, 15% waiting, 35% completed.
    private static RequestStatus PickStatus(Random random)
    {
        var percent = random.Next(100);   // 0 to 99

        if (percent < 20)
        {
            return RequestStatus.New;
        }

        if (percent < 50)
        {
            return RequestStatus.InProgress;
        }

        if (percent < 65)
        {
            return RequestStatus.Waiting;
        }

        return RequestStatus.Completed;
    }

    // 40% low, 40% medium, 20% high.
    private static RequestPriority PickPriority(Random random)
    {
        var percent = random.Next(100);   // 0 to 99

        if (percent < 40)
        {
            return RequestPriority.Low;
        }

        if (percent < 80)
        {
            return RequestPriority.Medium;
        }

        return RequestPriority.High;
    }
}
