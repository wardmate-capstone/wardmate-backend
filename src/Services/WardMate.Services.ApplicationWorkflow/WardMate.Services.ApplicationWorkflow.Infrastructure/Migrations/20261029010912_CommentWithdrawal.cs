using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WardMate.Services.ApplicationWorkflow.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CommentWithdrawal : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_application_comments_status",
                table: "application_comments");

            migrationBuilder.AddColumn<string>(
                name: "withdraw_reason",
                table: "application_comments",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "withdrawn_at",
                table: "application_comments",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "withdrawn_by",
                table: "application_comments",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_application_comments_status",
                table: "application_comments",
                sql: "status IN ('OPEN','RESOLVED','WITHDRAWN')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_application_comments_withdrawal",
                table: "application_comments",
                sql: "(status = 'WITHDRAWN' AND withdrawn_by IS NOT NULL AND withdrawn_at IS NOT NULL AND withdraw_reason IS NOT NULL) OR (status <> 'WITHDRAWN' AND withdrawn_by IS NULL AND withdrawn_at IS NULL AND withdraw_reason IS NULL)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Rolling back would drop who/when/why a comment was withdrawn; refuse instead of losing that evidence.
            migrationBuilder.Sql("DO $$ BEGIN IF EXISTS (SELECT 1 FROM application_comments WHERE status = 'WITHDRAWN') THEN RAISE EXCEPTION 'application_comments has WITHDRAWN rows; rollback would lose withdrawal audit.'; END IF; END $$;");

            migrationBuilder.DropCheckConstraint(
                name: "ck_application_comments_status",
                table: "application_comments");

            migrationBuilder.DropCheckConstraint(
                name: "ck_application_comments_withdrawal",
                table: "application_comments");

            migrationBuilder.DropColumn(
                name: "withdraw_reason",
                table: "application_comments");

            migrationBuilder.DropColumn(
                name: "withdrawn_at",
                table: "application_comments");

            migrationBuilder.DropColumn(
                name: "withdrawn_by",
                table: "application_comments");

            migrationBuilder.AddCheckConstraint(
                name: "ck_application_comments_status",
                table: "application_comments",
                sql: "status IN ('OPEN','RESOLVED')");
        }
    }
}
