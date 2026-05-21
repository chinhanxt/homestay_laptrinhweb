using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WebHomestay.Migrations
{
    /// <inheritdoc />
    public partial class AddRolePermissionTemplates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "role_permission_templates",
                columns: table => new
                {
                    role = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    permissions_json = table.Column<string>(type: "text", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_role_permission_templates", x => x.role);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "role_permission_templates");
        }
    }
}
