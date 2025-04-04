using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Outbox.EntityTypeConfigurations;

public class VirtualPartitionEntityTypeConfiguration : IEntityTypeConfiguration<VirtualPartition>
{
    public void Configure(EntityTypeBuilder<VirtualPartition> builder)
    {
        builder.HasKey(x => x.Id);
        
        builder.Property(e => e.LastProcessedTransactionId).HasColumnType("xid8");
        builder.Property(e => e.RetryAt).HasDefaultValueSql("now()");
        builder.Property(e => e.Topic).HasMaxLength(128);

        builder.HasIndex(x => new {x.Topic, x.Partition}).IsUnique();
    }
}