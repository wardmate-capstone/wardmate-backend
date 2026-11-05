using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WardMate.Services.ApplicationWorkflow.Infrastructure.AuditMigrations
{
    /// <inheritdoc />
    public partial class OfficerLeaveWordDrafts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "officer_leave_document_revisions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    draft_id = table.Column<Guid>(type: "uuid", nullable: false),
                    metadata_version = table.Column<int>(type: "integer", nullable: false),
                    sha256 = table.Column<string>(type: "text", nullable: false),
                    size_bytes = table.Column<int>(type: "integer", nullable: false),
                    saved_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_officer_leave_document_revisions", x => x.id);
                    table.UniqueConstraint("ak_officer_leave_document_revisions_draft_id_id", x => new { x.draft_id, x.id });
                });

            migrationBuilder.CreateTable(
                name: "officer_leave_drafts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    officer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    ward_code = table.Column<string>(type: "text", nullable: false),
                    starts_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ends_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    reason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    template_version_id = table.Column<Guid>(type: "uuid", nullable: false),
                    template_hash = table.Column<string>(type: "text", nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false),
                    metadata_version = table.Column<int>(type: "integer", nullable: false),
                    creation_key = table.Column<Guid>(type: "uuid", nullable: false),
                    creation_hash = table.Column<string>(type: "text", nullable: false),
                    current_revision_id = table.Column<Guid>(type: "uuid", nullable: true),
                    submitted_leave_id = table.Column<Guid>(type: "uuid", nullable: true),
                    submission_key = table.Column<Guid>(type: "uuid", nullable: true),
                    submission_hash = table.Column<string>(type: "text", nullable: true),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    deleted_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_officer_leave_drafts", x => x.id);
                    table.ForeignKey(
                        name: "fk_officer_leave_drafts_leaves_submitted_leave_id",
                        column: x => x.submitted_leave_id,
                        principalTable: "officer_leaves",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_officer_leave_drafts_officer_leave_document_revisions_id_cu",
                        columns: x => new { x.id, x.current_revision_id },
                        principalTable: "officer_leave_document_revisions",
                        principalColumns: new[] { "draft_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_officer_leave_drafts_id_current_revision_id",
                table: "officer_leave_drafts",
                columns: new[] { "id", "current_revision_id" });

            migrationBuilder.CreateIndex(
                name: "ix_officer_leave_drafts_officer_id_creation_key",
                table: "officer_leave_drafts",
                columns: new[] { "officer_id", "creation_key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_officer_leave_drafts_officer_id_submission_key",
                table: "officer_leave_drafts",
                columns: new[] { "officer_id", "submission_key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_officer_leave_drafts_submitted_leave_id",
                table: "officer_leave_drafts",
                column: "submitted_leave_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_officer_leave_drafts_ward_code_officer_id_updated_at_utc",
                table: "officer_leave_drafts",
                columns: new[] { "ward_code", "officer_id", "updated_at_utc" });

            migrationBuilder.AddForeignKey(
                name: "fk_officer_leave_document_revisions_officer_leave_drafts_draft",
                table: "officer_leave_document_revisions",
                column: "draft_id",
                principalTable: "officer_leave_drafts",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_officer_leave_document_revisions_officer_leave_drafts_draft",
                table: "officer_leave_document_revisions");

            migrationBuilder.DropTable(
                name: "officer_leave_drafts");

            migrationBuilder.DropTable(
                name: "officer_leave_document_revisions");
        }
    }
}
