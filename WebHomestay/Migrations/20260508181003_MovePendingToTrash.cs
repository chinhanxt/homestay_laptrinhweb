using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WebHomestay.Migrations
{
    /// <inheritdoc />
    public partial class MovePendingToTrash : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Move booking #1 and any other 'Pending' or 'PendingPayment' bookings to trash
            migrationBuilder.Sql("UPDATE bookings SET is_deleted = true, deleted_at = NOW() WHERE id = 1 OR status = 'Pending' OR status = 'PendingPayment'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
