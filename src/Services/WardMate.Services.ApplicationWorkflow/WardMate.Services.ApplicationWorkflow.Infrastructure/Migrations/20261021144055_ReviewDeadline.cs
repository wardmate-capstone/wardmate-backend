using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WardMate.Services.ApplicationWorkflow.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ReviewDeadline : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "deadline_calendar_code",
                table: "applications",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "deadline_calendar_version",
                table: "applications",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "deadline_history",
                table: "applications",
                type: "jsonb",
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.AddColumn<DateTime>(
                name: "deadline_paused_at_utc",
                table: "applications",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "deadline_policy_code",
                table: "applications",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "deadline_policy_snapshot",
                table: "applications",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "deadline_policy_version",
                table: "applications",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "deadline_status",
                table: "applications",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "UNKNOWN");

            migrationBuilder.AddColumn<DateTime>(
                name: "due_at_utc",
                table: "applications",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "first_submitted_at_utc",
                table: "applications",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_applications_ward_code_deadline_status_due_at_utc",
                table: "applications",
                columns: new[] { "ward_code", "deadline_status", "due_at_utc" });

            migrationBuilder.AddCheckConstraint(
                name: "ck_applications_deadline",
                table: "applications",
                sql: "(deadline_status = 'KNOWN' AND due_at_utc IS NOT NULL AND first_submitted_at_utc IS NOT NULL AND deadline_policy_snapshot IS NOT NULL AND deadline_policy_code IS NOT NULL AND deadline_policy_version IS NOT NULL AND deadline_calendar_code IS NOT NULL AND deadline_calendar_version IS NOT NULL) OR (deadline_status IN ('UNKNOWN','NOT_APPLICABLE') AND due_at_utc IS NULL AND deadline_paused_at_utc IS NULL)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DO $$ BEGIN IF EXISTS (SELECT 1 FROM applications WHERE first_submitted_at_utc IS NOT NULL OR deadline_status <> 'UNKNOWN' OR deadline_history <> '[]'::jsonb) THEN RAISE EXCEPTION 'Preserve deadline evidence before rollback.'; END IF; END $$;");
            migrationBuilder.DropIndex(
                name: "ix_applications_ward_code_deadline_status_due_at_utc",
                table: "applications");

            migrationBuilder.DropCheckConstraint(
                name: "ck_applications_deadline",
                table: "applications");

            migrationBuilder.DropColumn(
                name: "deadline_calendar_code",
                table: "applications");

            migrationBuilder.DropColumn(
                name: "deadline_calendar_version",
                table: "applications");

            migrationBuilder.DropColumn(
                name: "deadline_history",
                table: "applications");

            migrationBuilder.DropColumn(
                name: "deadline_paused_at_utc",
                table: "applications");

            migrationBuilder.DropColumn(
                name: "deadline_policy_code",
                table: "applications");

            migrationBuilder.DropColumn(
                name: "deadline_policy_snapshot",
                table: "applications");

            migrationBuilder.DropColumn(
                name: "deadline_policy_version",
                table: "applications");

            migrationBuilder.DropColumn(
                name: "deadline_status",
                table: "applications");

            migrationBuilder.DropColumn(
                name: "due_at_utc",
                table: "applications");

            migrationBuilder.DropColumn(
                name: "first_submitted_at_utc",
                table: "applications");
        }
    }
}

