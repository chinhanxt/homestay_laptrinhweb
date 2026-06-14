using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WebHomestay.Migrations
{
    /// <inheritdoc />
    public partial class AddAdminChatSessionArchiveFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "deleted_at",
                table: "admin_chat_sessions",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "deleted_by",
                table: "admin_chat_sessions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_deleted",
                table: "admin_chat_sessions",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "pause_reason",
                table: "admin_chat_sessions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "taken_over_at",
                table: "admin_chat_sessions",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "taken_over_by",
                table: "admin_chat_sessions",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "deleted_at",
                table: "admin_chat_sessions");

            migrationBuilder.DropColumn(
                name: "deleted_by",
                table: "admin_chat_sessions");

            migrationBuilder.DropColumn(
                name: "is_deleted",
                table: "admin_chat_sessions");

            migrationBuilder.DropColumn(
                name: "pause_reason",
                table: "admin_chat_sessions");

            migrationBuilder.DropColumn(
                name: "taken_over_at",
                table: "admin_chat_sessions");

            migrationBuilder.DropColumn(
                name: "taken_over_by",
                table: "admin_chat_sessions");
        }
    }
}
