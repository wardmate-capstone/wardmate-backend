using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WardMate.Services.DocumentForm.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class LinkSubmissionsToApplications : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "application_id",
                schema: "document",
                table: "user_submissions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "checklist_id",
                schema: "document",
                table: "user_submissions",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_user_submissions_application_id_checklist_id",
                schema: "document",
                table: "user_submissions",
                columns: new[] { "application_id", "checklist_id" },
                unique: true,
                filter: "application_id IS NOT NULL AND is_deleted = false");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_user_submissions_application_id_checklist_id",
                schema: "document",
                table: "user_submissions");

            migrationBuilder.DropColumn(
                name: "application_id",
                schema: "document",
                table: "user_submissions");

            migrationBuilder.DropColumn(
                name: "checklist_id",
                schema: "document",
                table: "user_submissions");
        }
    }
}
