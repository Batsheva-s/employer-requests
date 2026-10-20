namespace EmployerRequests.Infrastructure.Caching;

/// <summary>Settings of the summary cache – the "SummaryCache" section of appsettings.json.</summary>
public class SummaryCacheOptions
{
    public const string SectionName = "SummaryCache";

    /// <summary>
    /// How long a cached summary may be served. Every status change made through the API clears the cache
    /// immediately; the expiration is a safety net for changes made outside the API (a script, another system).
    /// </summary>
    public int ExpirationSeconds { get; set; } = 60;
}
