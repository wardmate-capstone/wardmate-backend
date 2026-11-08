using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WardMate.Services.ApplicationWorkflow.Infrastructure.AuditMigrations
{
    /// <inheritdoc />
    public partial class DisplayNameSnapshots : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "cancelled_by_name",
                table: "calendar_closures",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "created_by_name",
                table: "calendar_closures",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ward_name",
                table: "calendar_closures",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "application_code",
                table: "application_consultations",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "closed_by_name",
                table: "application_consultations",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "requested_by_name",
                table: "application_consultations",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "specialist_name",
                table: "application_consultations",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "actor_name",
                table: "application_assignment_history",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "from_officer_name",
                table: "application_assignment_history",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "to_officer_name",
                table: "application_assignment_history",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "cancelled_by_name",
                table: "calendar_closures");

            migrationBuilder.DropColumn(
                name: "created_by_name",
                table: "calendar_closures");

            migrationBuilder.DropColumn(
                name: "ward_name",
                table: "calendar_closures");

            migrationBuilder.DropColumn(
                name: "application_code",
                table: "application_consultations");

            migrationBuilder.DropColumn(
                name: "closed_by_name",
                table: "application_consultations");

            migrationBuilder.DropColumn(
                name: "requested_by_name",
                table: "application_consultations");

            migrationBuilder.DropColumn(
                name: "specialist_name",
                table: "application_consultations");

            migrationBuilder.DropColumn(
                name: "actor_name",
                table: "application_assignment_history");

            migrationBuilder.DropColumn(
                name: "from_officer_name",
                table: "application_assignment_history");

            migrationBuilder.DropColumn(
                name: "to_officer_name",
                table: "application_assignment_history");
        }
    }
}
