using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WardMate.Services.DocumentForm.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class OnlineFormDrafts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "form_data",
                schema: "document",
                table: "user_submissions",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "template_version_id",
                schema: "document",
                table: "user_submissions",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "form_template_versions",
                schema: "document",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    original_blob_url = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    original_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    mapping_definition = table.Column<string>(type: "jsonb", nullable: false),
                    template_id = table.Column<Guid>(type: "uuid", nullable: false),
                    version_number = table.Column<int>(type: "integer", nullable: false),
                    schema_definition = table.Column<string>(type: "jsonb", nullable: false),
                    created_by = table.Column<string>(type: "text", nullable: true),
                    updated_by = table.Column<string>(type: "text", nullable: true),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    deleted_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_form_template_versions", x => x.id);
                    table.ForeignKey(
                        name: "fk_form_template_versions_form_templates_template_id",
                        column: x => x.template_id,
                        principalSchema: "document",
                        principalTable: "form_templates",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_user_submissions_template_version_id",
                schema: "document",
                table: "user_submissions",
                column: "template_version_id");

            migrationBuilder.CreateIndex(
                name: "ix_form_template_versions_template_id_version_number",
                schema: "document",
                table: "form_template_versions",
                columns: new[] { "template_id", "version_number" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "fk_user_submissions_form_template_versions_template_version_id",
                schema: "document",
                table: "user_submissions",
                column: "template_version_id",
                principalSchema: "document",
                principalTable: "form_template_versions",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_user_submissions_form_template_versions_template_version_id",
                schema: "document",
                table: "user_submissions");

            migrationBuilder.DropTable(
                name: "form_template_versions",
                schema: "document");

            migrationBuilder.DropIndex(
                name: "ix_user_submissions_template_version_id",
                schema: "document",
                table: "user_submissions");

            migrationBuilder.DropColumn(
                name: "form_data",
                schema: "document",
                table: "user_submissions");

            migrationBuilder.DropColumn(
                name: "template_version_id",
                schema: "document",
                table: "user_submissions");
        }
    }
}
