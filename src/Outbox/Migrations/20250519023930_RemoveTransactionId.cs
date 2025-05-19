using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Outbox.Migrations
{
    /// <inheritdoc />
    public partial class RemoveTransactionId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("CREATE SEQUENCE outbox.outbox_messages_id_sequence;");
            
            migrationBuilder.DropIndex(
                name: "ix_outbox_messages_topic_partition_transaction_id_id",
                schema: "outbox",
                table: "outbox_messages");

            migrationBuilder.DropColumn(
                name: "last_processed_transaction_id",
                schema: "outbox",
                table: "outbox_offsets");

            migrationBuilder.DropColumn(
                name: "transaction_id",
                schema: "outbox",
                table: "outbox_messages");

            migrationBuilder.AlterColumn<int>(
                name: "id",
                schema: "outbox",
                table: "outbox_messages",
                type: "integer",
                nullable: false,
                defaultValueSql: "nextval('outbox.outbox_messages_id_sequence')",
                oldClrType: typeof(int),
                oldType: "integer")
                .OldAnnotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn);

            migrationBuilder.CreateIndex(
                name: "ix_outbox_messages_topic_partition_id",
                schema: "outbox",
                table: "outbox_messages",
                columns: new[] { "topic", "partition", "id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_outbox_messages_topic_partition_id",
                schema: "outbox",
                table: "outbox_messages");

            migrationBuilder.AddColumn<ulong>(
                name: "last_processed_transaction_id",
                schema: "outbox",
                table: "outbox_offsets",
                type: "xid8",
                nullable: false,
                defaultValueSql: "'0'::xid8");

            migrationBuilder.AlterColumn<int>(
                name: "id",
                schema: "outbox",
                table: "outbox_messages",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldDefaultValueSql: "nextval('outbox.outbox_messages_id_sequence')")
                .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn);

            migrationBuilder.AddColumn<ulong>(
                name: "transaction_id",
                schema: "outbox",
                table: "outbox_messages",
                type: "xid8",
                nullable: false,
                defaultValueSql: "pg_current_xact_id()");

            migrationBuilder.UpdateData(
                schema: "outbox",
                table: "outbox_offsets",
                keyColumn: "id",
                keyValue: 2,
                columns: new string[0],
                values: new object[0]);

            migrationBuilder.UpdateData(
                schema: "outbox",
                table: "outbox_offsets",
                keyColumn: "id",
                keyValue: 3,
                columns: new string[0],
                values: new object[0]);

            migrationBuilder.CreateIndex(
                name: "ix_outbox_messages_topic_partition_transaction_id_id",
                schema: "outbox",
                table: "outbox_messages",
                columns: new[] { "topic", "partition", "transaction_id", "id" });
        }
    }
}
