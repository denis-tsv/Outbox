using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Outbox.Migrations
{
    /// <inheritdoc />
    public partial class AddAvailableAfter : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "available_after",
                schema: "outbox",
                table: "outbox_messages",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()");

            migrationBuilder.CreateIndex(
                name: "ix_outbox_messages_available_after",
                schema: "outbox",
                table: "outbox_messages",
                column: "available_after");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_outbox_messages_available_after",
                schema: "outbox",
                table: "outbox_messages");

            migrationBuilder.DropColumn(
                name: "available_after",
                schema: "outbox",
                table: "outbox_messages");
        }
    }
}
