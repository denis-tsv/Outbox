using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Outbox.Entities;

namespace Outbox.EntityTypeConfigurations;

public class FailedOutboxMessageEntityTypeConfiguration : IEntityTypeConfiguration<FailedOutboxMessage>
{
    public void Configure(EntityTypeBuilder<FailedOutboxMessage> builder)
    {
        builder.ToTable("failed_outbox_messages");
        
        builder.Property(x => x.TransactionId).HasColumnType("xid8");
        builder.Property(x => x.Payload).HasColumnType("jsonb");
        builder.Property(x => x.Headers).HasColumnType("jsonb");
        builder.Property(x => x.Key).HasMaxLength(128);
        builder.Property(x => x.Type).HasMaxLength(128);
        builder.Property(x => x.Topic).HasMaxLength(128);
        builder.Property(x => x.CreatedAt);

        builder.HasIndex(x => new {x.Topic, x.Partition, x.TransactionId, x.Id});
        builder.HasIndex(x => new {x.CreatedAt, x.RetryAfter, x.Failed});
    }
}