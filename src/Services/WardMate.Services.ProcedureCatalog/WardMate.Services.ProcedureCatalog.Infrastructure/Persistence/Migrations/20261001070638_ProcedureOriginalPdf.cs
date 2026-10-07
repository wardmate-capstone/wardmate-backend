using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WardMate.Services.ProcedureCatalog.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ProcedureOriginalPdf : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "original_pdf_url",
                table: "procedures",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "pdf_file_name",
                table: "procedures",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "original_pdf_url",
                table: "procedure_versions",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "pdf_file_name",
                table: "procedure_versions",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.UpdateData(
                table: "procedures",
                keyColumn: "id",
                keyValue: new Guid("07000000-0000-0000-0000-000000000001"),
                columns: new[] { "original_pdf_url", "pdf_file_name" },
                values: new object[] { null, null });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "original_pdf_url",
                table: "procedures");

            migrationBuilder.DropColumn(
                name: "pdf_file_name",
                table: "procedures");

            migrationBuilder.DropColumn(
                name: "original_pdf_url",
                table: "procedure_versions");

            migrationBuilder.DropColumn(
                name: "pdf_file_name",
                table: "procedure_versions");
        }
    }
}
