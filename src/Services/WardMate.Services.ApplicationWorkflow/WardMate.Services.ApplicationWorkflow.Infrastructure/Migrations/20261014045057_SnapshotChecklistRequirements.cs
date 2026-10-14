using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WardMate.Services.ApplicationWorkflow.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SnapshotChecklistRequirements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "case_code",
                table: "application_checklists",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "condition_note",
                table: "application_checklists",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "document_copy_type",
                table: "application_checklists",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "quantity",
                table: "application_checklists",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "sort_order",
                table: "application_checklists",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "submission_type",
                table: "application_checklists",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "case_code",
                table: "application_checklists");

            migrationBuilder.DropColumn(
                name: "condition_note",
                table: "application_checklists");

            migrationBuilder.DropColumn(
                name: "document_copy_type",
                table: "application_checklists");

            migrationBuilder.DropColumn(
                name: "quantity",
                table: "application_checklists");

            migrationBuilder.DropColumn(
                name: "sort_order",
                table: "application_checklists");

            migrationBuilder.DropColumn(
                name: "submission_type",
                table: "application_checklists");
        }
    }
}
