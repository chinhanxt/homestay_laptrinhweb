using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WebHomestay.Migrations
{
    /// <inheritdoc />
    public partial class AddCancellationNotificationAudit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "notification_email_subject",
                table: "booking_cancellation_requests",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "notification_email_body",
                table: "booking_cancellation_requests",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "notification_email_sent_at",
                table: "booking_cancellation_requests",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "notification_email_attachment_name",
                table: "booking_cancellation_requests",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "notification_email_type",
                table: "booking_cancellation_requests",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "notification_email_subject",
                table: "booking_cancellation_requests");

            migrationBuilder.DropColumn(
                name: "notification_email_body",
                table: "booking_cancellation_requests");

            migrationBuilder.DropColumn(
                name: "notification_email_sent_at",
                table: "booking_cancellation_requests");

            migrationBuilder.DropColumn(
                name: "notification_email_attachment_name",
                table: "booking_cancellation_requests");

            migrationBuilder.DropColumn(
                name: "notification_email_type",
                table: "booking_cancellation_requests");
        }
    }
}
