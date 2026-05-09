using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace WebHomestay.Migrations
{
    /// <inheritdoc />
    public partial class AddHourlyDailyBookingModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "booking_mode",
                table: "bookings",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "room_slot_inventory_id",
                table: "bookings",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "slot_label",
                table: "bookings",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "room_slot_overrides",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    room_id = table.Column<int>(type: "integer", nullable: false),
                    target_date = table.Column<DateOnly>(type: "date", nullable: false),
                    template_id = table.Column<int>(type: "integer", nullable: true),
                    inventory_id = table.Column<int>(type: "integer", nullable: true),
                    override_type = table.Column<string>(type: "text", nullable: false),
                    reason = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_room_slot_overrides", x => x.id);
                    table.ForeignKey(
                        name: "FK_room_slot_overrides_rooms_room_id",
                        column: x => x.room_id,
                        principalTable: "rooms",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "room_slot_templates",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    name = table.Column<string>(type: "text", nullable: false),
                    code = table.Column<string>(type: "text", nullable: false),
                    duration_minutes = table.Column<int>(type: "integer", nullable: false),
                    cleanup_minutes = table.Column<int>(type: "integer", nullable: false),
                    seed_start_time = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
                    fixed_start_time = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
                    fixed_end_time = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
                    crosses_midnight = table.Column<bool>(type: "boolean", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_room_slot_templates", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "room_slot_inventories",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    room_id = table.Column<int>(type: "integer", nullable: false),
                    template_id = table.Column<int>(type: "integer", nullable: false),
                    slot_date = table.Column<DateOnly>(type: "date", nullable: false),
                    slot_label = table.Column<string>(type: "text", nullable: false),
                    start_time = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    end_time = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    booking_id = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_room_slot_inventories", x => x.id);
                    table.ForeignKey(
                        name: "FK_room_slot_inventories_room_slot_templates_template_id",
                        column: x => x.template_id,
                        principalTable: "room_slot_templates",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_room_slot_inventories_rooms_room_id",
                        column: x => x.room_id,
                        principalTable: "rooms",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "room_slot_template_assignments",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    room_id = table.Column<int>(type: "integer", nullable: false),
                    template_id = table.Column<int>(type: "integer", nullable: false),
                    effective_from = table.Column<DateOnly>(type: "date", nullable: false),
                    effective_to = table.Column<DateOnly>(type: "date", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_room_slot_template_assignments", x => x.id);
                    table.ForeignKey(
                        name: "FK_room_slot_template_assignments_room_slot_templates_template~",
                        column: x => x.template_id,
                        principalTable: "room_slot_templates",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_room_slot_template_assignments_rooms_room_id",
                        column: x => x.room_id,
                        principalTable: "rooms",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_bookings_room_slot_inventory_id",
                table: "bookings",
                column: "room_slot_inventory_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_room_slot_inventories_room_id",
                table: "room_slot_inventories",
                column: "room_id");

            migrationBuilder.CreateIndex(
                name: "IX_room_slot_inventories_template_id",
                table: "room_slot_inventories",
                column: "template_id");

            migrationBuilder.CreateIndex(
                name: "IX_room_slot_overrides_room_id",
                table: "room_slot_overrides",
                column: "room_id");

            migrationBuilder.CreateIndex(
                name: "IX_room_slot_template_assignments_room_id",
                table: "room_slot_template_assignments",
                column: "room_id");

            migrationBuilder.CreateIndex(
                name: "IX_room_slot_template_assignments_template_id",
                table: "room_slot_template_assignments",
                column: "template_id");

            migrationBuilder.AddForeignKey(
                name: "FK_bookings_room_slot_inventories_room_slot_inventory_id",
                table: "bookings",
                column: "room_slot_inventory_id",
                principalTable: "room_slot_inventories",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_bookings_room_slot_inventories_room_slot_inventory_id",
                table: "bookings");

            migrationBuilder.DropTable(
                name: "room_slot_inventories");

            migrationBuilder.DropTable(
                name: "room_slot_overrides");

            migrationBuilder.DropTable(
                name: "room_slot_template_assignments");

            migrationBuilder.DropTable(
                name: "room_slot_templates");

            migrationBuilder.DropIndex(
                name: "IX_bookings_room_slot_inventory_id",
                table: "bookings");

            migrationBuilder.DropColumn(
                name: "booking_mode",
                table: "bookings");

            migrationBuilder.DropColumn(
                name: "room_slot_inventory_id",
                table: "bookings");

            migrationBuilder.DropColumn(
                name: "slot_label",
                table: "bookings");
        }
    }
}
