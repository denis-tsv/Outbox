using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Outbox.Entities;

namespace Outbox.EntityTypeConfigurations;

public class OutboxOffsetEntityTypeConfiguration : IEntityTypeConfiguration<OutboxOffset>
{
    public void Configure(EntityTypeBuilder<OutboxOffset> builder)
    {
        builder.HasNoKey();
    }
}