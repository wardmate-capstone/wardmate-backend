using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WardMate.Services.AnalyticsSystem.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class EsmsNotificationDelivery : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "external_message_id",
                table: "notification_deliveries",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_sandbox",
                table: "notification_deliveries",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "provider",
                table: "notification_deliveries",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_notification_deliveries_provider_external_message_id",
                table: "notification_deliveries",
                columns: new[] { "provider", "external_message_id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_notification_deliveries_provider_external_message_id",
                table: "notification_deliveries");

            migrationBuilder.DropColumn(
                name: "external_message_id",
                table: "notification_deliveries");

            migrationBuilder.DropColumn(
                name: "is_sandbox",
                table: "notification_deliveries");

            migrationBuilder.DropColumn(
                name: "provider",
                table: "notification_deliveries");
        }
    }
}
