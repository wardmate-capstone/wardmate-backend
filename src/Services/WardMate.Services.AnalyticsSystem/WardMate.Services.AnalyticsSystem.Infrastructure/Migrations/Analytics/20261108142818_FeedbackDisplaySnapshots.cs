using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WardMate.Services.AnalyticsSystem.Infrastructure.Migrations.Analytics
{
    /// <inheritdoc />
    public partial class FeedbackDisplaySnapshots : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ApplicantName",
                schema: "analytics",
                table: "citizen_feedback",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ApplicationCode",
                schema: "analytics",
                table: "citizen_feedback",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProcedureTitle",
                schema: "analytics",
                table: "citizen_feedback",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ApplicantName",
                schema: "analytics",
                table: "citizen_feedback");

            migrationBuilder.DropColumn(
                name: "ApplicationCode",
                schema: "analytics",
                table: "citizen_feedback");

            migrationBuilder.DropColumn(
                name: "ProcedureTitle",
                schema: "analytics",
                table: "citizen_feedback");
        }
    }
}
