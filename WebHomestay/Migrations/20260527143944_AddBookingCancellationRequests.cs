using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace WebHomestay.Migrations
{
    /// <inheritdoc />
    public partial class AddBookingCancellationRequests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "booking_cancellation_requests",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    chat_session_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    booking_id = table.Column<int>(type: "integer", nullable: true),
                    submitted_booking_code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    customer_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    customer_phone = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    customer_email = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    confirmation_email_proof_path = table.Column<string>(type: "text", nullable: false),
                    refund_qr_image_path = table.Column<string>(type: "text", nullable: true),
                    refund_bank_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    refund_bank_account_number = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    refund_bank_account_holder = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    suggested_booking_ids_json = table.Column<string>(type: "text", nullable: true),
                    policy_notice_hours_snapshot = table.Column<int>(type: "integer", nullable: false),
                    refund_percent_before_notice_snapshot = table.Column<int>(type: "integer", nullable: false),
                    refund_percent_after_notice_snapshot = table.Column<int>(type: "integer", nullable: false),
                    policy_message_snapshot = table.Column<string>(type: "text", nullable: false),
                    applied_refund_percent = table.Column<int>(type: "integer", nullable: true),
                    refund_status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    refund_bill_proof_path = table.Column<string>(type: "text", nullable: true),
                    staff_reason = table.Column<string>(type: "text", nullable: true),
                    processed_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    processed_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_booking_cancellation_requests", x => x.id);
                    table.ForeignKey(
                        name: "FK_booking_cancellation_requests_bookings_booking_id",
                        column: x => x.booking_id,
                        principalTable: "bookings",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_booking_cancellation_requests_booking_id",
                table: "booking_cancellation_requests",
                column: "booking_id");

            migrationBuilder.CreateIndex(
                name: "IX_booking_cancellation_requests_chat_session_id_status",
                table: "booking_cancellation_requests",
                columns: new[] { "chat_session_id", "status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "booking_cancellation_requests");
        }
    }
}
