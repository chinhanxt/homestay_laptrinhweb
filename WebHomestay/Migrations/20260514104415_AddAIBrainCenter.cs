using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WebHomestay.Migrations
{
    /// <inheritdoc />
    public partial class AddAIBrainCenter : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ai_agent_definitions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    role = table.Column<string>(type: "text", nullable: false),
                    system_prompt = table.Column<string>(type: "text", nullable: false),
                    order = table.Column<int>(type: "integer", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    last_updated = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ai_agent_definitions", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "ai_brain_scopes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    description = table.Column<string>(type: "text", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    order = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ai_brain_scopes", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "ai_conversation_traces",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    session_id = table.Column<string>(type: "text", nullable: false),
                    customer_message = table.Column<string>(type: "text", nullable: false),
                    persona_summary = table.Column<string>(type: "text", nullable: false),
                    live_system_snapshot = table.Column<string>(type: "text", nullable: false),
                    retrieved_knowledge_json = table.Column<string>(type: "text", nullable: false),
                    graph_reasoning_json = table.Column<string>(type: "text", nullable: false),
                    guard_result = table.Column<string>(type: "text", nullable: false),
                    final_answer = table.Column<string>(type: "text", nullable: false),
                    model_provider = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ai_conversation_traces", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "ai_graph_nodes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    node_type = table.Column<string>(type: "text", nullable: false),
                    label = table.Column<string>(type: "text", nullable: false),
                    summary = table.Column<string>(type: "text", nullable: false),
                    metadata_json = table.Column<string>(type: "text", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ai_graph_nodes", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "ai_knowledge_units",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    scope_id = table.Column<Guid>(type: "uuid", nullable: false),
                    title = table.Column<string>(type: "text", nullable: false),
                    content = table.Column<string>(type: "text", nullable: false),
                    tags = table.Column<string>(type: "text", nullable: false),
                    priority = table.Column<int>(type: "integer", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    last_updated = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ai_knowledge_units", x => x.id);
                    table.ForeignKey(
                        name: "FK_ai_knowledge_units_ai_brain_scopes_scope_id",
                        column: x => x.scope_id,
                        principalTable: "ai_brain_scopes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ai_graph_edges",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    from_node_id = table.Column<Guid>(type: "uuid", nullable: false),
                    to_node_id = table.Column<Guid>(type: "uuid", nullable: false),
                    relationship_type = table.Column<string>(type: "text", nullable: false),
                    weight = table.Column<decimal>(type: "numeric", nullable: false),
                    evidence = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ai_graph_edges", x => x.id);
                    table.ForeignKey(
                        name: "FK_ai_graph_edges_ai_graph_nodes_from_node_id",
                        column: x => x.from_node_id,
                        principalTable: "ai_graph_nodes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ai_graph_edges_ai_graph_nodes_to_node_id",
                        column: x => x.to_node_id,
                        principalTable: "ai_graph_nodes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ai_graph_edges_from_node_id",
                table: "ai_graph_edges",
                column: "from_node_id");

            migrationBuilder.CreateIndex(
                name: "IX_ai_graph_edges_to_node_id",
                table: "ai_graph_edges",
                column: "to_node_id");

            migrationBuilder.CreateIndex(
                name: "IX_ai_knowledge_units_scope_id",
                table: "ai_knowledge_units",
                column: "scope_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ai_agent_definitions");

            migrationBuilder.DropTable(
                name: "ai_conversation_traces");

            migrationBuilder.DropTable(
                name: "ai_graph_edges");

            migrationBuilder.DropTable(
                name: "ai_knowledge_units");

            migrationBuilder.DropTable(
                name: "ai_graph_nodes");

            migrationBuilder.DropTable(
                name: "ai_brain_scopes");
        }
    }
}
