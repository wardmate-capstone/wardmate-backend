using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WardMate.Services.DocumentForm.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SupportingEvidenceRequirements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "document_copy_type",
                schema: "document",
                table: "supporting_documents",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "quantity",
                schema: "document",
                table: "supporting_documents",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<string>(
                name: "submission_type",
                schema: "document",
                table: "supporting_documents",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "document_copy_type",
                schema: "document",
                table: "supporting_documents");

            migrationBuilder.DropColumn(
                name: "quantity",
                schema: "document",
                table: "supporting_documents");

            migrationBuilder.DropColumn(
                name: "submission_type",
                schema: "document",
                table: "supporting_documents");
        }
    }
}
