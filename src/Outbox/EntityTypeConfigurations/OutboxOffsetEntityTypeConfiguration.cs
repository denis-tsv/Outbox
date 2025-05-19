using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Outbox.Entities;

namespace Outbox.EntityTypeConfigurations;

public class OutboxOffsetEntityTypeConfiguration : IEntityTypeConfiguration<OutboxOffset>
{
    public void Configure(EntityTypeBuilder<OutboxOffset> builder)
    {
        builder.Property(x => x.Topic).HasMaxLength(128);
        builder.Property(e => e.LastProcessedTransactionId).HasColumnType("xid8").HasDefaultValueSql("'0'::xid8");
        builder.Property(x => x.AvailableAfter).HasDefaultValueSql("now()");
        
        builder.HasIndex(x => new {x.Topic, x.Partition}).IsUnique();

        builder.HasData(new OutboxOffset
        {
            Id = 2,
            Topic = "topic-1", 
            Partition = 0,
            LastProcessedId = 0,
            LastProcessedTransactionId = 0,
            AvailableAfter = DateTimeOffset.Parse("2025-05-18T12:00")
        }, new OutboxOffset
        {
            Id = 3,
            Topic = "topic-1", 
            Partition = 1,
            LastProcessedId = 0,
            LastProcessedTransactionId = 0,
            AvailableAfter = DateTimeOffset.Parse("2025-05-18T12:00")
        });
    }
}
