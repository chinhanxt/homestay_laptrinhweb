using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WebHomestay.Migrations
{
    /// <inheritdoc />
    public partial class AddFloatEmbeddingsSupport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "taken_over_by",
                table: "admin_chat_sessions",
                newName: "TakenOverBy");

            migrationBuilder.RenameColumn(
                name: "taken_over_at",
                table: "admin_chat_sessions",
                newName: "TakenOverAt");

            migrationBuilder.RenameColumn(
                name: "pause_reason",
                table: "admin_chat_sessions",
                newName: "PauseReason");

            migrationBuilder.RenameColumn(
                name: "is_deleted",
                table: "admin_chat_sessions",
                newName: "IsDeleted");

            migrationBuilder.RenameColumn(
                name: "deleted_by",
                table: "admin_chat_sessions",
                newName: "DeletedBy");

            migrationBuilder.RenameColumn(
                name: "deleted_at",
                table: "admin_chat_sessions",
                newName: "DeletedAt");

            migrationBuilder.AddColumn<float[]>(
                name: "embedding",
                table: "rooms",
                type: "real[]",
                nullable: true);

            migrationBuilder.AddColumn<float[]>(
                name: "embedding",
                table: "ai_knowledge_units",
                type: "real[]",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "embedding",
                table: "rooms");

            migrationBuilder.DropColumn(
                name: "embedding",
                table: "ai_knowledge_units");

            migrationBuilder.RenameColumn(
                name: "TakenOverBy",
                table: "admin_chat_sessions",
                newName: "taken_over_by");

            migrationBuilder.RenameColumn(
                name: "TakenOverAt",
                table: "admin_chat_sessions",
                newName: "taken_over_at");

            migrationBuilder.RenameColumn(
                name: "PauseReason",
                table: "admin_chat_sessions",
                newName: "pause_reason");

            migrationBuilder.RenameColumn(
                name: "IsDeleted",
                table: "admin_chat_sessions",
                newName: "is_deleted");

            migrationBuilder.RenameColumn(
                name: "DeletedBy",
                table: "admin_chat_sessions",
                newName: "deleted_by");

            migrationBuilder.RenameColumn(
                name: "DeletedAt",
                table: "admin_chat_sessions",
                newName: "deleted_at");
        }
    }
}
