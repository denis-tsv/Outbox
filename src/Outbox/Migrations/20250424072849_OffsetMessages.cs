using System;
using System.Collections.Generic;
using EFCore.MigrationExtensions.SqlObjects;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Outbox.Migrations
{
    /// <inheritdoc />
    public partial class OffsetMessages : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "outbox_offset_messages",
                schema: "outbox",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    topic = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    partition = table.Column<int>(type: "integer", nullable: false),
                    offset = table.Column<long>(type: "bigint", nullable: false),
                    key = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    type = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    payload = table.Column<string>(type: "jsonb", nullable: false),
                    headers = table.Column<Dictionary<string, string>>(type: "jsonb", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_outbox_offset_messages", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "outbox_offsets",
                schema: "outbox",
                columns: table => new
                {
                    value = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                });

            migrationBuilder.CreateIndex(
                name: "ix_outbox_offset_messages_topic_partition_offset",
                schema: "outbox",
                table: "outbox_offset_messages",
                columns: new[] { "topic", "partition", "offset" });

            migrationBuilder.CreateOrUpdateSqlObject(
                name: "insert_outbox_offset_message.sql",
                sqlCode: "CREATE OR REPLACE PROCEDURE outbox.insert_outbox_offset_message(topic varchar, partition int, type varchar, key varchar, payload jsonb, headers jsonb)\nLANGUAGE plpgsql\nAS $$\nDECLARE\ncounter BIGINT;\n    tid TID;\nBEGIN\ninsert into outbox.outbox_offset_messages(topic, partition, type, key, payload, headers, \"offset\")\nvalues (topic, partition, type, key, payload, headers, 0)\n    returning ctid into tid;\nupdate outbox.outbox_offsets set value = value + 1 returning value into counter;\nupdate outbox.outbox_offset_messages set \"offset\" = counter where ctid = tid;\n\nEND;\n$$;",
                order: 2147483647);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropSqlObject(
                name: "insert_outbox_offset_message.sql",
                sqlCode: "DROP procedure outbox.insert_outbox_offset_message",
                order: 2147483647);

            migrationBuilder.DropTable(
                name: "outbox_offset_messages",
                schema: "outbox");

            migrationBuilder.DropTable(
                name: "outbox_offsets",
                schema: "outbox");
        }
    }
}
