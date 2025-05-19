using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Outbox.Entities;

namespace Outbox.EntityTypeConfigurations;

public class OutboxMessageEntityTypeConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.HasKey(x => x.Id);
        
        builder.Property(x => x.TransactionId).HasDefaultValueSql("pg_current_xact_id()").HasColumnType("xid8");
        builder.Property(x => x.Payload).HasColumnType("jsonb");
        builder.Property(x => x.Headers).HasColumnType("jsonb");
        builder.Property(x => x.Key).HasMaxLength(128);
        builder.Property(x => x.Type).HasMaxLength(128);
        builder.Property(x => x.Topic).HasMaxLength(128);
        builder.Property(x => x.CreatedAt).HasDefaultValueSql("now()");

        builder.HasIndex(x => new {x.Topic, x.Partition, x.TransactionId, x.Id});
    }
}