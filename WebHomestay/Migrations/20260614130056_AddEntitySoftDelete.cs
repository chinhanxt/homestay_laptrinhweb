using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WebHomestay.Migrations
{
    /// <inheritdoc />
    public partial class AddEntitySoftDelete : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "IsDeleted",
                table: "admin_chat_sessions",
                newName: "is_deleted");

            migrationBuilder.RenameColumn(
                name: "DeletedAt",
                table: "admin_chat_sessions",
                newName: "deleted_at");

            migrationBuilder.AddColumn<DateTime>(
                name: "deleted_at",
                table: "rooms",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_deleted",
                table: "rooms",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "deleted_at",
                table: "branches",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_deleted",
                table: "branches",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "deleted_at",
                table: "admin_users",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_deleted",
                table: "admin_users",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "deleted_at",
                table: "rooms");

            migrationBuilder.DropColumn(
                name: "is_deleted",
                table: "rooms");

            migrationBuilder.DropColumn(
                name: "deleted_at",
                table: "branches");

            migrationBuilder.DropColumn(
                name: "is_deleted",
                table: "branches");

            migrationBuilder.DropColumn(
                name: "deleted_at",
                table: "admin_users");

            migrationBuilder.DropColumn(
                name: "is_deleted",
                table: "admin_users");

            migrationBuilder.RenameColumn(
                name: "is_deleted",
                table: "admin_chat_sessions",
                newName: "IsDeleted");

            migrationBuilder.RenameColumn(
                name: "deleted_at",
                table: "admin_chat_sessions",
                newName: "DeletedAt");
        }
    }
}
