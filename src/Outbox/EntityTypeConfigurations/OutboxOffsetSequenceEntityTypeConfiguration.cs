using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Outbox.Entities;

namespace Outbox.EntityTypeConfigurations;

public class OutboxOffsetSequenceEntityTypeConfiguration : IEntityTypeConfiguration<OutboxOffsetSequence>
{
    public void Configure(EntityTypeBuilder<OutboxOffsetSequence> builder)
    {
        builder.Property(x => x.Topic).HasMaxLength(128);
        
        builder.HasData(new OutboxOffsetSequence
        {
            Id = 1,
            Topic = "topic-1", 
            Partition = 0,
        },new OutboxOffsetSequence
        {
            Id = 2,
            Topic = "topic-1", 
            Partition = 1
        });
    }
}
