using EFCore.MigrationExtensions.SqlObjects;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Outbox.Migrations
{
    /// <inheritdoc />
    public partial class AddPartitionNumber : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_outbox_messages_topic_partition_id",
                schema: "outbox",
                table: "outbox_messages");

            migrationBuilder.RenameColumn(
                name: "last_processed_id",
                schema: "outbox",
                table: "outbox_offsets",
                newName: "last_processed_number");

            migrationBuilder.AddColumn<int>(
                name: "number",
                schema: "outbox",
                table: "outbox_messages",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "outbox_offset_sequences",
                schema: "outbox",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    topic = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    partition = table.Column<int>(type: "integer", nullable: false),
                    value = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_outbox_offset_sequences", x => x.id);
                });

            migrationBuilder.InsertData(
                schema: "outbox",
                table: "outbox_offset_sequences",
                columns: new[] { "id", "partition", "topic", "value" },
                values: new object[,]
                {
                    { 1, 0, "topic-1", 0 },
                    { 2, 1, "topic-1", 0 }
                });

            migrationBuilder.CreateIndex(
                name: "ix_outbox_messages_topic_partition_number",
                schema: "outbox",
                table: "outbox_messages",
                columns: new[] { "topic", "partition", "number" });

            migrationBuilder.CreateOrUpdateSqlObject(
                name: "insert_outbox_message.sql",
                sqlCode: "CREATE OR REPLACE PROCEDURE outbox.insert_outbox_message(IN ptopic character varying, IN ppartition integer, IN type character varying, IN key character varying, IN payload jsonb, IN headers jsonb)\n LANGUAGE plpgsql\nAS $procedure$\nDECLARE\ncounter int;\nBEGIN\nupdate outbox.outbox_offset_sequences\nset value = value + 1\nwhere topic = ptopic and partition = ppartition\nreturning value into counter;\ninsert into outbox.outbox_messages(topic, partition, type, key, payload, headers, number)\nvalues (ptopic, ppartition, type, key, payload, headers, counter);\nEND;\n$procedure$\n;\n",
                order: 2147483647);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropSqlObject(
                name: "insert_outbox_message.sql",
                sqlCode: "DROP PROCEDURE outbox.insert_outbox_message",
                order: 2147483647);

            migrationBuilder.DropTable(
                name: "outbox_offset_sequences",
                schema: "outbox");

            migrationBuilder.DropIndex(
                name: "ix_outbox_messages_topic_partition_number",
                schema: "outbox",
                table: "outbox_messages");

            migrationBuilder.DropColumn(
                name: "number",
                schema: "outbox",
                table: "outbox_messages");

            migrationBuilder.RenameColumn(
                name: "last_processed_number",
                schema: "outbox",
                table: "outbox_offsets",
                newName: "last_processed_id");

            migrationBuilder.CreateIndex(
                name: "ix_outbox_messages_topic_partition_id",
                schema: "outbox",
                table: "outbox_messages",
                columns: new[] { "topic", "partition", "id" });
        }
    }
}
