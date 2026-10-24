using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WardMate.Services.AnalyticsSystem.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class NotificationDeliveryAdministration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "revision",
                table: "notification_deliveries",
                type: "uuid",
                nullable: false,
                defaultValueSql: "gen_random_uuid()");

            migrationBuilder.AddColumn<DateTime>(
                name: "updated_at",
                table: "notification_deliveries",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()");

            migrationBuilder.CreateTable(
                name: "notification_delivery_audits",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DeliveryId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: false),
                    Action = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    BeforeState = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    AfterState = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    BeforeAttempts = table.Column<int>(type: "integer", nullable: false),
                    BeforeRevision = table.Column<Guid>(type: "uuid", nullable: false),
                    AfterRevision = table.Column<Guid>(type: "uuid", nullable: false),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Reference = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    BeforeMessageId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    AfterMessageId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_notification_delivery_audits", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_notification_deliveries_State_updated_at",
                table: "notification_deliveries",
                columns: new[] { "State", "updated_at" });

            migrationBuilder.CreateIndex(
                name: "IX_notification_delivery_audits_DeliveryId_CreatedAt",
                table: "notification_delivery_audits",
                columns: new[] { "DeliveryId", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Rolling back must not silently destroy reconciliation evidence.
            migrationBuilder.Sql("""
                DO $rollback$
                BEGIN
                  IF EXISTS (SELECT 1 FROM notification_delivery_audits) THEN
                    RAISE EXCEPTION 'notification_delivery_audits has rows; export them before rolling back NotificationDeliveryAdministration.';
                  END IF;
                END $rollback$;
                """);
            migrationBuilder.DropTable(
                name: "notification_delivery_audits");

            migrationBuilder.DropIndex(
                name: "IX_notification_deliveries_State_updated_at",
                table: "notification_deliveries");

            migrationBuilder.DropColumn(
                name: "revision",
                table: "notification_deliveries");

            migrationBuilder.DropColumn(
                name: "updated_at",
                table: "notification_deliveries");
        }
    }
}
