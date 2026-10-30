using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WardMate.Services.ApplicationWorkflow.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddStaffingTablesOnly : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "application_consultations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    application_id = table.Column<Guid>(type: "uuid", nullable: false),
                    specialist_id = table.Column<Guid>(type: "uuid", nullable: false),
                    requested_by = table.Column<Guid>(type: "uuid", nullable: false),
                    application_version = table.Column<int>(type: "integer", nullable: false),
                    assignment_version = table.Column<long>(type: "bigint", nullable: false),
                    question = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    answer = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    answered_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    closed_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    closed_by = table.Column<Guid>(type: "uuid", nullable: true),
                    close_reason = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_application_consultations", x => x.id);
                    table.CheckConstraint("ck_consultation_status", "status IN ('OPEN','ANSWERED','CLOSED')");
                    table.ForeignKey(
                        name: "fk_application_consultations_applications_application_id",
                        column: x => x.application_id,
                        principalTable: "applications",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "officer_leaves",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    officer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    ward_code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    starts_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ends_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    reason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    decided_by = table.Column<Guid>(type: "uuid", nullable: true),
                    decided_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    decision_reason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_officer_leaves", x => x.id);
                    table.CheckConstraint("ck_leave_range", "ends_at_utc > starts_at_utc");
                    table.CheckConstraint("ck_leave_status", "status IN ('PENDING','APPROVED','REJECTED','CANCELLED')");
                });

            migrationBuilder.CreateTable(
                name: "officer_shifts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    officer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    ward_code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    starts_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ends_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    cancelled_by = table.Column<Guid>(type: "uuid", nullable: true),
                    cancelled_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    cancellation_reason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_officer_shifts", x => x.id);
                    table.CheckConstraint("ck_shift_range", "ends_at_utc > starts_at_utc");
                });

            migrationBuilder.CreateTable(
                name: "officer_leave_decisions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    leave_id = table.Column<Guid>(type: "uuid", nullable: false),
                    actor_id = table.Column<Guid>(type: "uuid", nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false),
                    from_status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    to_status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    reason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_officer_leave_decisions", x => x.id);
                    table.ForeignKey(
                        name: "fk_officer_leave_decisions_officer_leaves_leave_id",
                        column: x => x.leave_id,
                        principalTable: "officer_leaves",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_application_consultations_application_id_specialist_id",
                table: "application_consultations",
                columns: new[] { "application_id", "specialist_id" },
                unique: true,
                filter: "status <> 'CLOSED'");

            migrationBuilder.CreateIndex(
                name: "ix_officer_leave_decisions_leave_id_version",
                table: "officer_leave_decisions",
                columns: new[] { "leave_id", "version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_officer_leaves_ward_code_officer_id_starts_at_utc",
                table: "officer_leaves",
                columns: new[] { "ward_code", "officer_id", "starts_at_utc" });

            migrationBuilder.CreateIndex(
                name: "ix_officer_shifts_ward_code_officer_id_starts_at_utc",
                table: "officer_shifts",
                columns: new[] { "ward_code", "officer_id", "starts_at_utc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "application_consultations");

            migrationBuilder.DropTable(
                name: "officer_leave_decisions");

            migrationBuilder.DropTable(
                name: "officer_shifts");

            migrationBuilder.DropTable(
                name: "officer_leaves");
        }
    }
}
