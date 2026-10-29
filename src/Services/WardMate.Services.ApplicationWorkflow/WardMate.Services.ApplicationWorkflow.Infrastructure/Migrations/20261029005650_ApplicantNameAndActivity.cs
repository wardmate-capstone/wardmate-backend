using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WardMate.Services.ApplicationWorkflow.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ApplicantNameAndActivity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "applicant_name",
                table: "applications",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_application_status_history_changed_by_created_at",
                table: "application_status_history",
                columns: new[] { "changed_by", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_application_assignment_history_actor_id_created_at",
                table: "application_assignment_history",
                columns: new[] { "actor_id", "created_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_application_status_history_changed_by_created_at",
                table: "application_status_history");

            migrationBuilder.DropIndex(
                name: "ix_application_assignment_history_actor_id_created_at",
                table: "application_assignment_history");

            migrationBuilder.DropColumn(
                name: "applicant_name",
                table: "applications");
        }
    }
}
