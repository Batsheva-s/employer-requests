using EmployerRequests.Domain;
using Microsoft.EntityFrameworkCore;

namespace EmployerRequests.Infrastructure.Data;

/// <summary>
/// The EF Core database context – Code First: the tables, columns and indexes below become the migrations.
/// One instance per HTTP request (Scoped), shared by everything that request does.
/// </summary>
public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    public DbSet<ServiceRequest> Requests => Set<ServiceRequest>();

    public DbSet<RequestStatusChange> RequestStatusChanges => Set<RequestStatusChange>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ConfigureRequests(modelBuilder);
        ConfigureStatusHistory(modelBuilder);
    }

    private static void ConfigureRequests(ModelBuilder modelBuilder)
    {
        var request = modelBuilder.Entity<ServiceRequest>();

        request.ToTable("Requests");
        request.HasKey(r => r.Id);

        request.Property(r => r.Title)
            .HasMaxLength(ServiceRequest.TitleMaxLength)
            .IsRequired();

        request.Property(r => r.OrganizationName)
            .HasMaxLength(ServiceRequest.OrganizationNameMaxLength)
            .IsRequired();

        request.Property(r => r.AssignedTo)
            .HasMaxLength(ServiceRequest.AssignedToMaxLength);

        // Optimistic concurrency: a SQL Server rowversion column, which the database changes on every update.
        // EF Core adds "WHERE RowVersion = @originalValue" to every UPDATE of this table, and reports a
        // concurrency conflict when no row matched.
        request.Property(r => r.RowVersion)
            .IsRowVersion();

        // The history rows are added through the request (ChangeStatus) and saved together with it.
        request.HasMany(r => r.StatusChanges)
            .WithOne()
            .HasForeignKey(change => change.RequestId)
            .OnDelete(DeleteBehavior.Cascade);

        // ---------- Indexes: one for each main query pattern (measured in docs/PERFORMANCE.md) ----------

        // The default list: newest first, and the created-date range filter.
        request.HasIndex(r => r.CreatedAt)
            .HasDatabaseName("IX_Requests_CreatedAt");

        // Filtering by status with the default sort. Priority is included so that the summary's
        // GROUP BY Status, Priority is answered from this narrow index alone, without reading the table.
        request.HasIndex(r => new { r.Status, r.CreatedAt })
            .IncludeProperties(r => r.Priority)
            .HasDatabaseName("IX_Requests_Status_CreatedAt");

        // The organization filter is "starts with" (LIKE 'text%'), which can seek on this index.
        request.HasIndex(r => r.OrganizationName)
            .HasDatabaseName("IX_Requests_OrganizationName");

        // The handler filter ("starts with") and the summary's busiest handlers (open requests per handler).
        request.HasIndex(r => new { r.AssignedTo, r.Status })
            .HasDatabaseName("IX_Requests_AssignedTo_Status");
    }

    private static void ConfigureStatusHistory(ModelBuilder modelBuilder)
    {
        var change = modelBuilder.Entity<RequestStatusChange>();

        change.ToTable("RequestStatusChanges");
        change.HasKey(c => c.Id);

        change.Property(c => c.ChangedBy)
            .HasMaxLength(RequestStatusChange.ChangedByMaxLength)
            .IsRequired();

        // The history of one request, newest first: WHERE RequestId = @id ORDER BY ChangedAt DESC.
        // It also serves the foreign key, so EF Core does not create a separate index on RequestId.
        change.HasIndex(c => new { c.RequestId, c.ChangedAt })
            .HasDatabaseName("IX_RequestStatusChanges_RequestId_ChangedAt");
    }
}
