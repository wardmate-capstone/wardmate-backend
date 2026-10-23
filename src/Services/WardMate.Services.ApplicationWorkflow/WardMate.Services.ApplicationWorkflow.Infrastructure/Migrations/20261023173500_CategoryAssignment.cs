using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WardMate.Services.ApplicationWorkflow.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CategoryAssignment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "assigned_at",
                table: "applications",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "assigned_officer_name",
                table: "applications",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "assignment_version",
                table: "applications",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<int>(
                name: "category_id",
                table: "applications",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "category_name",
                table: "applications",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "application_assignment_history",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    application_id = table.Column<Guid>(type: "uuid", nullable: false),
                    action = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    from_officer_id = table.Column<Guid>(type: "uuid", nullable: true),
                    to_officer_id = table.Column<Guid>(type: "uuid", nullable: true),
                    actor_id = table.Column<Guid>(type: "uuid", nullable: false),
                    reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_application_assignment_history", x => x.id);
                    table.ForeignKey(
                        name: "fk_application_assignment_history_applications_application_id",
                        column: x => x.application_id,
                        principalTable: "applications",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_applications_ward_code_category_id_status",
                table: "applications",
                columns: new[] { "ward_code", "category_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_application_assignment_history_application_id_created_at",
                table: "application_assignment_history",
                columns: new[] { "application_id", "created_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "application_assignment_history");

            migrationBuilder.DropIndex(
                name: "ix_applications_ward_code_category_id_status",
                table: "applications");

            migrationBuilder.DropColumn(
                name: "assigned_at",
                table: "applications");

            migrationBuilder.DropColumn(
                name: "assigned_officer_name",
                table: "applications");

            migrationBuilder.DropColumn(
                name: "assignment_version",
                table: "applications");

            migrationBuilder.DropColumn(
                name: "category_id",
                table: "applications");

            migrationBuilder.DropColumn(
                name: "category_name",
                table: "applications");
        }
    }
}
