using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WardMate.Services.ApplicationWorkflow.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ApplicationCaseName : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "case_name",
                table: "applications",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "case_name",
                table: "applications");
        }
    }
}
