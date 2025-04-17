using Microsoft.EntityFrameworkCore;
using Outbox.Entities;

namespace Outbox;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<OutboxMessage> OutboxMessages { get; set; }
    public DbSet<FailedOutboxMessage> FailedOutboxMessages { get; set; }
    public DbSet<VirtualPartition> VirtualPartitions { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("outbox");

        modelBuilder.ApplyConfigurationsFromAssembly(GetType().Assembly);
    }
}