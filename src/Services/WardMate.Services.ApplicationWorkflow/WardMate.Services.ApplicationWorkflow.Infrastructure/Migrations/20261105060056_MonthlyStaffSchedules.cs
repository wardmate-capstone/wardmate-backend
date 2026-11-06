using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WardMate.Services.ApplicationWorkflow.Infrastructure.AuditMigrations
{
    /// <inheritdoc />
    public partial class MonthlyStaffSchedules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "version",
                table: "officer_shifts",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "officer_schedule_receipts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    ward_code = table.Column<string>(type: "text", nullable: false),
                    month = table.Column<DateOnly>(type: "date", nullable: false),
                    officer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    fingerprint = table.Column<string>(type: "text", nullable: false),
                    confirmed_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_officer_schedule_receipts", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "officer_shift_change_requests",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    shift_id = table.Column<Guid>(type: "uuid", nullable: false),
                    officer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    ward_code = table.Column<string>(type: "text", nullable: false),
                    original_start = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    original_end = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    original_version = table.Column<int>(type: "integer", nullable: false),
                    proposed_start = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    proposed_end = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    reason = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    decided_by = table.Column<Guid>(type: "uuid", nullable: true),
                    decided_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    decision_reason = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_officer_shift_change_requests", x => x.id);
                    table.ForeignKey(
                        name: "fk_officer_shift_change_requests_shifts_shift_id",
                        column: x => x.shift_id,
                        principalTable: "officer_shifts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ward_schedule_revisions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    ward_code = table.Column<string>(type: "text", nullable: false),
                    month = table.Column<DateOnly>(type: "date", nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    base_fingerprint = table.Column<string>(type: "text", nullable: false),
                    slots_json = table.Column<string>(type: "jsonb", nullable: false),
                    actor_id = table.Column<Guid>(type: "uuid", nullable: false),
                    reason = table.Column<string>(type: "text", nullable: false),
                    published_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_ward_schedule_revisions", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "ward_week_patterns",
                columns: table => new
                {
                    ward_code = table.Column<string>(type: "text", nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false),
                    entries_json = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_ward_week_patterns", x => x.ward_code);
                });

            migrationBuilder.CreateIndex(
                name: "ix_officer_schedule_receipts_ward_code_month_officer_id_finger",
                table: "officer_schedule_receipts",
                columns: new[] { "ward_code", "month", "officer_id", "fingerprint" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_officer_shift_change_requests_shift_id",
                table: "officer_shift_change_requests",
                column: "shift_id",
                unique: true,
                filter: "status = 'PENDING'");

            migrationBuilder.CreateIndex(
                name: "ix_ward_schedule_revisions_ward_code_month",
                table: "ward_schedule_revisions",
                columns: new[] { "ward_code", "month" },
                unique: true,
                filter: "status = 'DRAFT'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "officer_schedule_receipts");

            migrationBuilder.DropTable(
                name: "officer_shift_change_requests");

            migrationBuilder.DropTable(
                name: "ward_schedule_revisions");

            migrationBuilder.DropTable(
                name: "ward_week_patterns");

            migrationBuilder.DropColumn(
                name: "version",
                table: "officer_shifts");
        }
    }
}
