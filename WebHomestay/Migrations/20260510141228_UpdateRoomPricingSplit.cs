using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WebHomestay.Migrations
{
    /// <inheritdoc />
    public partial class UpdateRoomPricingSplit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "price_weekend",
                table: "rooms",
                newName: "price_weekend_per_hour");

            migrationBuilder.RenameColumn(
                name: "price_holiday",
                table: "rooms",
                newName: "price_weekend_per_day");

            migrationBuilder.AddColumn<decimal>(
                name: "price_holiday_per_day",
                table: "rooms",
                type: "numeric(10,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "price_holiday_per_hour",
                table: "rooms",
                type: "numeric(10,2)",
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "price_holiday_per_day",
                table: "rooms");

            migrationBuilder.DropColumn(
                name: "price_holiday_per_hour",
                table: "rooms");

            migrationBuilder.RenameColumn(
                name: "price_weekend_per_hour",
                table: "rooms",
                newName: "price_weekend");

            migrationBuilder.RenameColumn(
                name: "price_weekend_per_day",
                table: "rooms",
                newName: "price_holiday");
        }
    }
}
