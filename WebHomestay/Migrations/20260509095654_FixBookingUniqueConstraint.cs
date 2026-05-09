using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WebHomestay.Migrations
{
    /// <inheritdoc />
    public partial class FixBookingUniqueConstraint : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_bookings_room_slot_inventory_id",
                table: "bookings");

            migrationBuilder.CreateIndex(
                name: "IX_bookings_room_slot_inventory_id",
                table: "bookings",
                column: "room_slot_inventory_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_bookings_room_slot_inventory_id",
                table: "bookings");

            migrationBuilder.CreateIndex(
                name: "IX_bookings_room_slot_inventory_id",
                table: "bookings",
                column: "room_slot_inventory_id",
                unique: true);
        }
    }
}
