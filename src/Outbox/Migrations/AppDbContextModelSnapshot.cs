﻿// <auto-generated />
using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;
using Outbox;

#nullable disable

namespace Outbox.Migrations
{
    [DbContext(typeof(AppDbContext))]
    partial class AppDbContextModelSnapshot : ModelSnapshot
    {
        protected override void BuildModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .HasDefaultSchema("outbox")
                .HasAnnotation("ProductVersion", "9.0.3")
                .HasAnnotation("Relational:MaxIdentifierLength", 63);

            NpgsqlModelBuilderExtensions.UseIdentityByDefaultColumns(modelBuilder);

            modelBuilder.Entity("Outbox.Entities.FailedOutboxMessage", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("integer")
                        .HasColumnName("id");

                    NpgsqlPropertyBuilderExtensions.UseIdentityByDefaultColumn(b.Property<int>("Id"));

                    b.Property<DateTimeOffset>("CreatedAt")
                        .HasColumnType("timestamp with time zone")
                        .HasColumnName("created_at");

                    b.Property<bool>("Failed")
                        .HasColumnType("boolean")
                        .HasColumnName("failed");

                    b.Property<Dictionary<string, string>>("Headers")
                        .IsRequired()
                        .HasColumnType("jsonb")
                        .HasColumnName("headers");

                    b.Property<string>("Key")
                        .HasMaxLength(128)
                        .HasColumnType("character varying(128)")
                        .HasColumnName("key");

                    b.Property<int>("Partition")
                        .HasColumnType("integer")
                        .HasColumnName("partition");

                    b.Property<string>("Payload")
                        .IsRequired()
                        .HasColumnType("jsonb")
                        .HasColumnName("payload");

                    b.Property<DateTimeOffset?>("RetryAfter")
                        .HasColumnType("timestamp with time zone")
                        .HasColumnName("retry_after");

                    b.Property<int>("RetryCount")
                        .HasColumnType("integer")
                        .HasColumnName("retry_count");

                    b.Property<string>("Topic")
                        .IsRequired()
                        .HasMaxLength(128)
                        .HasColumnType("character varying(128)")
                        .HasColumnName("topic");

                    b.Property<ulong>("TransactionId")
                        .HasColumnType("xid8")
                        .HasColumnName("transaction_id");

                    b.Property<string>("Type")
                        .IsRequired()
                        .HasMaxLength(128)
                        .HasColumnType("character varying(128)")
                        .HasColumnName("type");

                    b.HasKey("Id")
                        .HasName("pk_failed_outbox_messages");

                    b.HasIndex("CreatedAt", "RetryAfter", "Failed")
                        .HasDatabaseName("ix_failed_outbox_messages_created_at_retry_after_failed");

                    b.HasIndex("Topic", "Partition", "TransactionId", "Id")
                        .HasDatabaseName("ix_failed_outbox_messages_topic_partition_transaction_id_id");

                    b.ToTable("failed_outbox_messages", "outbox");
                });

            modelBuilder.Entity("Outbox.Entities.OutboxMessage", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("integer")
                        .HasColumnName("id");

                    NpgsqlPropertyBuilderExtensions.UseIdentityByDefaultColumn(b.Property<int>("Id"));

                    b.Property<DateTimeOffset>("CreatedAt")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("timestamp with time zone")
                        .HasColumnName("created_at")
                        .HasDefaultValueSql("now()");

                    b.Property<bool>("Failed")
                        .HasColumnType("boolean")
                        .HasColumnName("failed");

                    b.Property<Dictionary<string, string>>("Headers")
                        .IsRequired()
                        .HasColumnType("jsonb")
                        .HasColumnName("headers");

                    b.Property<string>("Key")
                        .HasMaxLength(128)
                        .HasColumnType("character varying(128)")
                        .HasColumnName("key");

                    b.Property<int>("Partition")
                        .HasColumnType("integer")
                        .HasColumnName("partition");

                    b.Property<string>("Payload")
                        .IsRequired()
                        .HasColumnType("jsonb")
                        .HasColumnName("payload");

                    b.Property<DateTimeOffset?>("RetryAfter")
                        .HasColumnType("timestamp with time zone")
                        .HasColumnName("retry_after");

                    b.Property<int>("RetryCount")
                        .HasColumnType("integer")
                        .HasColumnName("retry_count");

                    b.Property<string>("Topic")
                        .IsRequired()
                        .HasMaxLength(128)
                        .HasColumnType("character varying(128)")
                        .HasColumnName("topic");

                    b.Property<ulong>("TransactionId")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("xid8")
                        .HasColumnName("transaction_id")
                        .HasDefaultValueSql("pg_current_xact_id()");

                    b.Property<string>("Type")
                        .IsRequired()
                        .HasMaxLength(128)
                        .HasColumnType("character varying(128)")
                        .HasColumnName("type");

                    b.HasKey("Id")
                        .HasName("pk_outbox_messages");

                    b.HasIndex("CreatedAt", "RetryAfter", "Failed")
                        .HasDatabaseName("ix_outbox_messages_created_at_retry_after_failed");

                    b.HasIndex("Topic", "Partition", "TransactionId", "Id")
                        .HasDatabaseName("ix_outbox_messages_topic_partition_transaction_id_id");

                    b.ToTable("outbox_messages", "outbox");
                });

            modelBuilder.Entity("Outbox.Entities.VirtualPartition", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("integer")
                        .HasColumnName("id");

                    NpgsqlPropertyBuilderExtensions.UseIdentityByDefaultColumn(b.Property<int>("Id"));

                    b.Property<int>("LastProcessedId")
                        .HasColumnType("integer")
                        .HasColumnName("last_processed_id");

                    b.Property<ulong>("LastProcessedTransactionId")
                        .HasColumnType("xid8")
                        .HasColumnName("last_processed_transaction_id");

                    b.Property<int>("Partition")
                        .HasColumnType("integer")
                        .HasColumnName("partition");

                    b.Property<DateTimeOffset>("RetryAfter")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("timestamp with time zone")
                        .HasColumnName("retry_after")
                        .HasDefaultValueSql("now()");

                    b.Property<string>("Topic")
                        .IsRequired()
                        .HasMaxLength(128)
                        .HasColumnType("character varying(128)")
                        .HasColumnName("topic");

                    b.HasKey("Id")
                        .HasName("pk_virtual_partitions");

                    b.HasIndex("Topic", "Partition")
                        .IsUnique()
                        .HasDatabaseName("ix_virtual_partitions_topic_partition");

                    b.ToTable("virtual_partitions", "outbox");
                });
#pragma warning restore 612, 618
        }
    }
}
