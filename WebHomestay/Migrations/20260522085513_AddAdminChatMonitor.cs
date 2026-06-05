using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WebHomestay.Migrations
{
    /// <inheritdoc />
    public partial class AddAdminChatMonitor : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "admin_chat_messages",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    session_id = table.Column<string>(type: "text", nullable: false),
                    role = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    content = table.Column<string>(type: "text", nullable: false),
                    form_block_json = table.Column<string>(type: "text", nullable: true),
                    form_block_type = table.Column<string>(type: "text", nullable: true),
                    created_by = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    is_read = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_admin_chat_messages", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "admin_chat_sessions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    session_id = table.Column<string>(type: "text", nullable: false),
                    customer_name = table.Column<string>(type: "text", nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    paused_by = table.Column<string>(type: "text", nullable: true),
                    paused_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    auto_reply_message = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    last_activity_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_admin_chat_sessions", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_admin_chat_messages_session_id_created_at",
                table: "admin_chat_messages",
                columns: new[] { "session_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_admin_chat_sessions_session_id",
                table: "admin_chat_sessions",
                column: "session_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "admin_chat_messages");

            migrationBuilder.DropTable(
                name: "admin_chat_sessions");
        }
    }
}
