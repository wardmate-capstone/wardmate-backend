using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WardMate.Services.DocumentForm.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SubmissionFileVersions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "submission_file_versions",
                schema: "document",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    application_id = table.Column<Guid>(type: "uuid", nullable: false),
                    checklist_id = table.Column<Guid>(type: "uuid", nullable: true),
                    source_type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    source_id = table.Column<Guid>(type: "uuid", nullable: false),
                    application_version_number = table.Column<int>(type: "integer", nullable: false),
                    blob_url = table.Column<string>(type: "text", nullable: false),
                    file_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    file_size_bytes = table.Column<long>(type: "bigint", nullable: false),
                    content_type = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    form_data = table.Column<string>(type: "jsonb", nullable: true),
                    source_updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_submission_file_versions", x => x.id);
                    table.CheckConstraint("ck_submission_file_versions_source", "source_type IN ('SUBMISSION','SUPPORTING_DOCUMENT')");
                    table.CheckConstraint("ck_submission_file_versions_version", "application_version_number >= 1");
                });

            migrationBuilder.CreateIndex(
                name: "ux_submission_file_versions_source",
                schema: "document",
                table: "submission_file_versions",
                columns: new[] { "application_id", "application_version_number", "source_type", "source_id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "submission_file_versions",
                schema: "document");
        }
    }
}
