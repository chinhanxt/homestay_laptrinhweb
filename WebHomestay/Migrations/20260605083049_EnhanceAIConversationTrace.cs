using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WebHomestay.Migrations
{
    /// <inheritdoc />
    public partial class EnhanceAIConversationTrace : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ActionTaken",
                table: "ai_conversation_traces",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AgentOutputs",
                table: "ai_conversation_traces",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ErrorLog",
                table: "ai_conversation_traces",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IntentClassified",
                table: "ai_conversation_traces",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "IntentConfidence",
                table: "ai_conversation_traces",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ModelName",
                table: "ai_conversation_traces",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PerformanceLog",
                table: "ai_conversation_traces",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ResponseTimeMs",
                table: "ai_conversation_traces",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ActionTaken",
                table: "ai_conversation_traces");

            migrationBuilder.DropColumn(
                name: "AgentOutputs",
                table: "ai_conversation_traces");

            migrationBuilder.DropColumn(
                name: "ErrorLog",
                table: "ai_conversation_traces");

            migrationBuilder.DropColumn(
                name: "IntentClassified",
                table: "ai_conversation_traces");

            migrationBuilder.DropColumn(
                name: "IntentConfidence",
                table: "ai_conversation_traces");

            migrationBuilder.DropColumn(
                name: "ModelName",
                table: "ai_conversation_traces");

            migrationBuilder.DropColumn(
                name: "PerformanceLog",
                table: "ai_conversation_traces");

            migrationBuilder.DropColumn(
                name: "ResponseTimeMs",
                table: "ai_conversation_traces");
        }
    }
}
