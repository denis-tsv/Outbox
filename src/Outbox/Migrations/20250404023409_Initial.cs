using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Outbox.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "outbox");

            migrationBuilder.CreateTable(
                name: "outbox_messages",
                schema: "outbox",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    transaction_id = table.Column<ulong>(type: "xid8", nullable: false, defaultValueSql: "pg_current_xact_id()"),
                    topic = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    partition = table.Column<int>(type: "integer", nullable: false),
                    key = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    type = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    payload = table.Column<string>(type: "jsonb", nullable: false),
                    headers = table.Column<Dictionary<string, string>>(type: "jsonb", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_outbox_messages", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "virtual_partitions",
                schema: "outbox",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    partition = table.Column<int>(type: "integer", nullable: false),
                    topic = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    last_processed_transaction_id = table.Column<ulong>(type: "xid8", nullable: false),
                    last_processed_id = table.Column<int>(type: "integer", nullable: false),
                    retry_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_virtual_partitions", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_outbox_messages_topic_partition_transaction_id_id",
                schema: "outbox",
                table: "outbox_messages",
                columns: new[] { "topic", "partition", "transaction_id", "id" });

            migrationBuilder.CreateIndex(
                name: "ix_virtual_partitions_topic_partition",
                schema: "outbox",
                table: "virtual_partitions",
                columns: new[] { "topic", "partition" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "outbox_messages",
                schema: "outbox");

            migrationBuilder.DropTable(
                name: "virtual_partitions",
                schema: "outbox");
        }
    }
}
