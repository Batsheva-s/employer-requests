using EmployerRequests.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EmployerRequests.Infrastructure.Data.Configurations;

public class ServiceRequestConfiguration : IEntityTypeConfiguration<ServiceRequest>
{
    public void Configure(EntityTypeBuilder<ServiceRequest> builder)
    {
        builder.ToTable("Requests");
        builder.HasKey(request => request.Id);

        builder.Property(request => request.Title)
            .HasMaxLength(ServiceRequest.TitleMaxLength)
            .IsRequired();

        builder.Property(request => request.OrganizationName)
            .HasMaxLength(ServiceRequest.OrganizationNameMaxLength)
            .IsRequired();

        builder.Property(request => request.AssignedTo)
            .HasMaxLength(ServiceRequest.AssignedToMaxLength);

        // Optimistic concurrency: a SQL Server rowversion column, which the database changes on every update.
        // EF Core adds "WHERE RowVersion = @originalValue" to every UPDATE of this table, and reports a
        // concurrency conflict when no row matched.
        builder.Property(request => request.RowVersion)
            .IsRowVersion();

        // The history rows are added through the request (ChangeStatus) and saved together with it.
        builder.HasMany(request => request.StatusChanges)
            .WithOne()
            .HasForeignKey(change => change.RequestId)
            .OnDelete(DeleteBehavior.Cascade);

        // ---------- Indexes: one for each main query pattern (measured in docs/PERFORMANCE.md) ----------

        // The default list: newest first, and the created-date range filter.
        builder.HasIndex(request => request.CreatedAt)
            .HasDatabaseName("IX_Requests_CreatedAt");

        // Filtering by status with the default sort. Priority is included so that the summary's
        // GROUP BY Status, Priority is answered from this narrow index alone, without reading the table.
        builder.HasIndex(request => new { request.Status, request.CreatedAt })
            .IncludeProperties(request => request.Priority)
            .HasDatabaseName("IX_Requests_Status_CreatedAt");

        // The organization filter is "starts with" (LIKE 'text%'), which can seek on this index.
        builder.HasIndex(request => request.OrganizationName)
            .HasDatabaseName("IX_Requests_OrganizationName");

        // The handler filter ("starts with") and the summary's busiest handlers (open requests per handler).
        builder.HasIndex(request => new { request.AssignedTo, request.Status })
            .HasDatabaseName("IX_Requests_AssignedTo_Status");
    }
}
