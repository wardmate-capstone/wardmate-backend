using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WardMate.Services.ApplicationWorkflow.Infrastructure.AuditMigrations
{
    /// <inheritdoc />
    public partial class LeaveDraftAudit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "submitted_officer_name",
                table: "officer_leave_drafts",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "officer_leave_draft_audit",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    draft_id = table.Column<Guid>(type: "uuid", nullable: false),
                    actor_id = table.Column<Guid>(type: "uuid", nullable: false),
                    ward_code = table.Column<string>(type: "text", nullable: false),
                    action = table.Column<string>(type: "text", nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false),
                    revision_id = table.Column<Guid>(type: "uuid", nullable: true),
                    occurred_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_officer_leave_draft_audit", x => x.id);
                    table.ForeignKey(
                        name: "fk_officer_leave_draft_audit_leave_drafts_draft_id",
                        column: x => x.draft_id,
                        principalTable: "officer_leave_drafts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_officer_leave_draft_audit_draft_id_version",
                table: "officer_leave_draft_audit",
                columns: new[] { "draft_id", "version" },
                unique: true);
            // Record effective legacy shifts at cutover without rewriting shift IDs or claiming a human publication.
            migrationBuilder.Sql("""
                INSERT INTO ward_schedule_revisions
                    (id, ward_code, month, version, status, base_fingerprint, slots_json, actor_id, reason, published_at_utc)
                SELECT gen_random_uuid(), ward_code,
                       date_trunc('month', starts_at_utc AT TIME ZONE 'Asia/Ho_Chi_Minh')::date,
                       1, 'BASELINE', '', jsonb_agg(jsonb_build_object(
                           'ShiftId', id, 'OfficerId', officer_id, 'StartsAt', starts_at_utc,
                           'EndsAt', ends_at_utc, 'ShiftVersion', version) ORDER BY starts_at_utc, id),
                       '00000000-0000-0000-0000-000000000000'::uuid, 'Migration ca có hiệu lực', now()
                FROM officer_shifts WHERE cancelled_at_utc IS NULL
                GROUP BY ward_code, date_trunc('month', starts_at_utc AT TIME ZONE 'Asia/Ho_Chi_Minh')::date;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DELETE FROM ward_schedule_revisions WHERE status = 'BASELINE'");
            migrationBuilder.DropTable(
                name: "officer_leave_draft_audit");

            migrationBuilder.DropColumn(
                name: "submitted_officer_name",
                table: "officer_leave_drafts");
        }
    }
}
