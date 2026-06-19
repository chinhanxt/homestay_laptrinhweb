using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WebHomestay.Migrations
{
    /// <inheritdoc />
    public partial class AddBranchLeadTimeValueAndUnit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "booking_lead_time_unit",
                table: "branches",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "booking_lead_time_value",
                table: "branches",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.Sql("""
                UPDATE branches
                SET booking_lead_time_value = COALESCE(booking_lead_time_hours, 2),
                    booking_lead_time_unit = 'Hours';
            """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "booking_lead_time_unit",
                table: "branches");

            migrationBuilder.DropColumn(
                name: "booking_lead_time_value",
                table: "branches");
        }
    }
}
