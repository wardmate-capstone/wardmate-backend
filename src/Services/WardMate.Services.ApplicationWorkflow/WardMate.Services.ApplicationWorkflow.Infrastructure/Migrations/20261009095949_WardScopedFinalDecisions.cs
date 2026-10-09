using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WardMate.Services.ApplicationWorkflow.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class WardScopedFinalDecisions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_applications_status",
                table: "applications");

            migrationBuilder.AddColumn<string>(
                name: "ward_code",
                table: "applications",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_applications_ward_code_status_submitted_at",
                table: "applications",
                columns: new[] { "ward_code", "status", "submitted_at" });

            migrationBuilder.AddCheckConstraint(
                name: "ck_applications_status",
                table: "applications",
                sql: "status IN ('DRAFT','SUBMITTED','UNDER_REVIEW','NEED_REVISION','APPROVED','CANCELLED','REJECTED')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_applications_ward_code_status_submitted_at",
                table: "applications");

            migrationBuilder.DropCheckConstraint(
                name: "ck_applications_status",
                table: "applications");

            migrationBuilder.DropColumn(
                name: "ward_code",
                table: "applications");

            migrationBuilder.AddCheckConstraint(
                name: "ck_applications_status",
                table: "applications",
                sql: "status IN ('DRAFT','SUBMITTED','UNDER_REVIEW','NEED_REVISION','APPROVED','CANCELLED')");
        }
    }
}
