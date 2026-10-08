using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WardMate.Services.ApplicationWorkflow.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ApplicationReviewVersions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_applications_status",
                table: "applications");

            migrationBuilder.AlterColumn<string>(
                name: "status",
                table: "applications",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "DRAFT",
                oldClrType: typeof(string),
                oldType: "character varying(30)",
                oldMaxLength: 30,
                oldDefaultValue: "DRAFT");

            // Existing drafts received their code only on submit. Allocate stable codes before NOT NULL.
            migrationBuilder.Sql("""
                UPDATE applications SET application_code = 'HS-' || to_char(created_at AT TIME ZONE 'Asia/Ho_Chi_Minh', 'YYYYMMDD')
                    || '-' || lpad(nextval('application_code_sequence')::text, 12, '0')
                WHERE application_code IS NULL;
                """);
            migrationBuilder.AlterColumn<string>(
                name: "application_code",
                table: "applications",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(40)",
                oldMaxLength: 40,
                oldNullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "approved_at",
                table: "applications",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "assigned_officer_id",
                table: "applications",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "notes",
                table: "applications",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "resubmit_count",
                table: "applications",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AlterColumn<string>(
                name: "to_status",
                table: "application_status_history",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(30)",
                oldMaxLength: 30);

            migrationBuilder.AlterColumn<string>(
                name: "from_status",
                table: "application_status_history",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(30)",
                oldMaxLength: 30,
                oldNullable: true);

            migrationBuilder.CreateTable(
                name: "application_versions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    application_id = table.Column<Guid>(type: "uuid", nullable: false),
                    version_number = table.Column<int>(type: "integer", nullable: false),
                    submitted_by = table.Column<Guid>(type: "uuid", nullable: false),
                    submitted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    snapshot_data = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_application_versions", x => x.id);
                    table.UniqueConstraint("ak_application_versions_application_id_id", x => new { x.application_id, x.id });
                    table.CheckConstraint("ck_application_versions_number", "version_number > 0");
                    table.ForeignKey(
                        name: "fk_application_versions_applications_application_id",
                        column: x => x.application_id,
                        principalTable: "applications",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "application_comments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    application_id = table.Column<Guid>(type: "uuid", nullable: false),
                    application_version_id = table.Column<Guid>(type: "uuid", nullable: false),
                    officer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    target_type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    target_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    field_label = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    comment_text = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "OPEN"),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_application_comments", x => x.id);
                    table.CheckConstraint("ck_application_comments_status", "status IN ('OPEN','RESOLVED')");
                    table.CheckConstraint("ck_application_comments_target", "target_type IN ('FORM_FIELD','CHECKLIST_ITEM')");
                    table.ForeignKey(
                        name: "fk_application_comments_application_versions_application_id_ap",
                        columns: x => new { x.application_id, x.application_version_id },
                        principalTable: "application_versions",
                        principalColumns: new[] { "application_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_application_comments_applications_application_id",
                        column: x => x.application_id,
                        principalTable: "applications",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_applications_assigned_officer_id",
                table: "applications",
                column: "assigned_officer_id");

            migrationBuilder.CreateIndex(
                name: "ix_applications_status_submitted_at_id",
                table: "applications",
                columns: new[] { "status", "submitted_at", "id" });

            migrationBuilder.AddCheckConstraint(
                name: "ck_applications_status",
                table: "applications",
                sql: "status IN ('DRAFT','SUBMITTED','UNDER_REVIEW','NEED_REVISION','APPROVED','CANCELLED')");

            migrationBuilder.CreateIndex(
                name: "ix_application_comments_application_id_application_version_id_",
                table: "application_comments",
                columns: new[] { "application_id", "application_version_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_application_versions_application_id_version_number",
                table: "application_versions",
                columns: new[] { "application_id", "version_number" },
                unique: true);
            // Pre-task SUBMITTED data was immutable through the old API; preserve it as version 1.
            migrationBuilder.Sql("""
                INSERT INTO application_versions (id, application_id, version_number, submitted_by, submitted_at, snapshot_data)
                SELECT gen_random_uuid(), a.id, 1, a.user_id, COALESCE(a.submitted_at, a.updated_at),
                    jsonb_build_object('id', a.id, 'applicationCode', a.application_code, 'userId', a.user_id,
                        'procedureId', a.procedure_id, 'procedureTitle', a.procedure_title, 'caseCode', a.case_code,
                        'formData', a.form_data, 'checklists', COALESCE((
                            SELECT jsonb_agg(jsonb_build_object('id', c.id, 'code', c.code, 'title', c.title,
                                'isRequired', c.is_required, 'status', c.status, 'fileUrl', c.file_url, 'note', c.note) ORDER BY c.id)
                            FROM application_checklists c WHERE c.application_id = a.id), '[]'::jsonb))
                FROM applications a WHERE a.status = 'SUBMITTED';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "application_comments");

            migrationBuilder.DropTable(
                name: "application_versions");

            migrationBuilder.DropIndex(
                name: "ix_applications_assigned_officer_id",
                table: "applications");

            migrationBuilder.DropIndex(
                name: "ix_applications_status_submitted_at_id",
                table: "applications");

            migrationBuilder.DropCheckConstraint(
                name: "ck_applications_status",
                table: "applications");

            migrationBuilder.DropColumn(
                name: "approved_at",
                table: "applications");

            migrationBuilder.DropColumn(
                name: "assigned_officer_id",
                table: "applications");

            migrationBuilder.DropColumn(
                name: "notes",
                table: "applications");

            migrationBuilder.DropColumn(
                name: "resubmit_count",
                table: "applications");

            migrationBuilder.AlterColumn<string>(
                name: "status",
                table: "applications",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "DRAFT",
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50,
                oldDefaultValue: "DRAFT");

            migrationBuilder.AlterColumn<string>(
                name: "application_code",
                table: "applications",
                type: "character varying(40)",
                maxLength: 40,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50);

            migrationBuilder.AlterColumn<string>(
                name: "to_status",
                table: "application_status_history",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50);

            migrationBuilder.AlterColumn<string>(
                name: "from_status",
                table: "application_status_history",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50);

            migrationBuilder.AddCheckConstraint(
                name: "ck_applications_status",
                table: "applications",
                sql: "status IN ('DRAFT','SUBMITTED')");
        }
    }
}

