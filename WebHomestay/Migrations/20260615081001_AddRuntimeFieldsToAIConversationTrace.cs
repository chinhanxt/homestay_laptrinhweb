using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WebHomestay.Migrations
{
    /// <inheritdoc />
    public partial class AddRuntimeFieldsToAIConversationTrace : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "RuntimeName",
                table: "ai_conversation_traces",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RuntimeVersion",
                table: "ai_conversation_traces",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RuntimeName",
                table: "ai_conversation_traces");

            migrationBuilder.DropColumn(
                name: "RuntimeVersion",
                table: "ai_conversation_traces");
        }
    }
}
