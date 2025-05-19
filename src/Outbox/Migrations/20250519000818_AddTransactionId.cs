using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Outbox.Migrations
{
    /// <inheritdoc />
    public partial class AddTransactionId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_outbox_messages_available_after",
                schema: "outbox",
                table: "outbox_messages");

            migrationBuilder.DeleteData(
                schema: "outbox",
                table: "outbox_offsets",
                keyColumn: "id",
                keyValue: 1);

            migrationBuilder.DropColumn(
                name: "available_after",
                schema: "outbox",
                table: "outbox_messages");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "available_after",
                schema: "outbox",
                table: "outbox_offsets",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()");

            migrationBuilder.AddColumn<ulong>(
                name: "last_processed_transaction_id",
                schema: "outbox",
                table: "outbox_offsets",
                type: "xid8",
                nullable: false,
                defaultValueSql: "'0'::xid8");

            migrationBuilder.AddColumn<int>(
                name: "partition",
                schema: "outbox",
                table: "outbox_offsets",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "topic",
                schema: "outbox",
                table: "outbox_offsets",
                type: "character varying(128)",
                maxLength: 128,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "partition",
                schema: "outbox",
                table: "outbox_messages",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<ulong>(
                name: "transaction_id",
                schema: "outbox",
                table: "outbox_messages",
                type: "xid8",
                nullable: false,
                defaultValueSql: "pg_current_xact_id()");

            migrationBuilder.InsertData(
                schema: "outbox",
                table: "outbox_offsets",
                columns: new[] { "id", "available_after", "last_processed_id", "partition", "topic" },
                values: new object[,]
                {
                    { 2, new DateTimeOffset(new DateTime(2025, 5, 18, 12, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 7, 0, 0, 0)), 0, 0, "topic-1" },
                    { 3, new DateTimeOffset(new DateTime(2025, 5, 18, 12, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 7, 0, 0, 0)), 0, 1, "topic-1" }
                });

            migrationBuilder.CreateIndex(
                name: "ix_outbox_offsets_topic_partition",
                schema: "outbox",
                table: "outbox_offsets",
                columns: new[] { "topic", "partition" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_outbox_messages_topic_partition_transaction_id_id",
                schema: "outbox",
                table: "outbox_messages",
                columns: new[] { "topic", "partition", "transaction_id", "id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_outbox_offsets_topic_partition",
                schema: "outbox",
                table: "outbox_offsets");

            migrationBuilder.DropIndex(
                name: "ix_outbox_messages_topic_partition_transaction_id_id",
                schema: "outbox",
                table: "outbox_messages");

            migrationBuilder.DeleteData(
                schema: "outbox",
                table: "outbox_offsets",
                keyColumn: "id",
                keyValue: 2);

            migrationBuilder.DeleteData(
                schema: "outbox",
                table: "outbox_offsets",
                keyColumn: "id",
                keyValue: 3);

            migrationBuilder.DropColumn(
                name: "available_after",
                schema: "outbox",
                table: "outbox_offsets");

            migrationBuilder.DropColumn(
                name: "last_processed_transaction_id",
                schema: "outbox",
                table: "outbox_offsets");

            migrationBuilder.DropColumn(
                name: "partition",
                schema: "outbox",
                table: "outbox_offsets");

            migrationBuilder.DropColumn(
                name: "topic",
                schema: "outbox",
                table: "outbox_offsets");

            migrationBuilder.DropColumn(
                name: "partition",
                schema: "outbox",
                table: "outbox_messages");

            migrationBuilder.DropColumn(
                name: "transaction_id",
                schema: "outbox",
                table: "outbox_messages");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "available_after",
                schema: "outbox",
                table: "outbox_messages",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()");

            migrationBuilder.InsertData(
                schema: "outbox",
                table: "outbox_offsets",
                columns: new[] { "id", "last_processed_id" },
                values: new object[] { 1, 0 });

            migrationBuilder.CreateIndex(
                name: "ix_outbox_messages_available_after",
                schema: "outbox",
                table: "outbox_messages",
                column: "available_after");
        }
    }
}
