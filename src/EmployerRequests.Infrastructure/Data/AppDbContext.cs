using EmployerRequests.Domain;
using Microsoft.EntityFrameworkCore;

namespace EmployerRequests.Infrastructure.Data;

/// <summary>The EF Core unit of work. Code First: the schema comes from the entity configurations and the migrations.</summary>
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
        // Picks up every IEntityTypeConfiguration in this assembly (the Configurations folder).
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
