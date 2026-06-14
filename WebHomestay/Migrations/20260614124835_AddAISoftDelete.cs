using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WebHomestay.Migrations
{
    /// <inheritdoc />
    public partial class AddAISoftDelete : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "deleted_at",
                table: "ai_knowledge_units",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_deleted",
                table: "ai_knowledge_units",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "deleted_at",
                table: "ai_graph_nodes",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_deleted",
                table: "ai_graph_nodes",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "deleted_at",
                table: "ai_graph_edges",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_deleted",
                table: "ai_graph_edges",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "deleted_at",
                table: "ai_knowledge_units");

            migrationBuilder.DropColumn(
                name: "is_deleted",
                table: "ai_knowledge_units");

            migrationBuilder.DropColumn(
                name: "deleted_at",
                table: "ai_graph_nodes");

            migrationBuilder.DropColumn(
                name: "is_deleted",
                table: "ai_graph_nodes");

            migrationBuilder.DropColumn(
                name: "deleted_at",
                table: "ai_graph_edges");

            migrationBuilder.DropColumn(
                name: "is_deleted",
                table: "ai_graph_edges");
        }
    }
}
