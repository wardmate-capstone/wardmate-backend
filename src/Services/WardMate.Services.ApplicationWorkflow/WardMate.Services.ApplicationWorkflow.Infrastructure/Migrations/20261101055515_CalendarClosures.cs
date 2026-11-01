using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WardMate.Services.ApplicationWorkflow.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CalendarClosures : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "calendar_closures",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    ward_code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    closure_date = table.Column<DateOnly>(type: "date", nullable: false),
                    effective_from_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    cancelled_by = table.Column<Guid>(type: "uuid", nullable: true),
                    cancelled_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    cancellation_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_calendar_closures", x => x.id);
                    table.CheckConstraint("ck_closure_cancellation", "(cancelled_by IS NULL AND cancelled_at_utc IS NULL AND cancellation_reason IS NULL) OR (cancelled_by IS NOT NULL AND cancelled_at_utc IS NOT NULL AND cancellation_reason IS NOT NULL)");
                    table.CheckConstraint("ck_closure_reason", "length(btrim(reason)) BETWEEN 1 AND 500");
                });

            migrationBuilder.CreateIndex(
                name: "ix_calendar_closures_closure_date_id",
                table: "calendar_closures",
                columns: new[] { "closure_date", "id" });

            migrationBuilder.CreateIndex(
                name: "ix_calendar_closures_ward_code_closure_date",
                table: "calendar_closures",
                columns: new[] { "ward_code", "closure_date" },
                unique: true,
                filter: "cancelled_at_utc IS NULL AND ward_code IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ux_calendar_closures_national",
                table: "calendar_closures",
                column: "closure_date",
                unique: true,
                filter: "cancelled_at_utc IS NULL AND ward_code IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "calendar_closures");
        }
    }
}
