using EmployerRequests.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EmployerRequests.Infrastructure.Data.Configurations;

public class RequestStatusChangeConfiguration : IEntityTypeConfiguration<RequestStatusChange>
{
    public void Configure(EntityTypeBuilder<RequestStatusChange> builder)
    {
        builder.ToTable("RequestStatusChanges");
        builder.HasKey(change => change.Id);

        builder.Property(change => change.ChangedBy)
            .HasMaxLength(RequestStatusChange.ChangedByMaxLength)
            .IsRequired();

        // The history of one request, newest first: WHERE RequestId = @id ORDER BY ChangedAt DESC.
        // It also serves the foreign key, so EF Core does not create a separate index on RequestId.
        builder.HasIndex(change => new { change.RequestId, change.ChangedAt })
            .HasDatabaseName("IX_RequestStatusChanges_RequestId_ChangedAt");
    }
}
