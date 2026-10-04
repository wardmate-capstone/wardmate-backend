using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WardMate.Services.ProcedureCatalog.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ProcedureDrafts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "procedure_drafts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    blob_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    pdf_file_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    original_pdf_url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    payload_json = table.Column<string>(type: "jsonb", nullable: false),
                    warnings_json = table.Column<string>(type: "jsonb", nullable: false),
                    extracted_text = table.Column<string>(type: "text", nullable: false),
                    failure_code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    created_by = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    reviewed_by = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    published_procedure_id = table.Column<Guid>(type: "uuid", nullable: true),
                    revision = table.Column<Guid>(type: "uuid", nullable: false),
                    lease_id = table.Column<Guid>(type: "uuid", nullable: true),
                    lease_until = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    attempts = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_procedure_drafts", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_procedure_drafts_status_lease_until",
                table: "procedure_drafts",
                columns: new[] { "status", "lease_until" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "procedure_drafts");
        }
    }
}
