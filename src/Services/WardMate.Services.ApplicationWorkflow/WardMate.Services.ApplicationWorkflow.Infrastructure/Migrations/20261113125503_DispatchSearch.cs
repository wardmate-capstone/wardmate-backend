using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WardMate.Services.ApplicationWorkflow.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class DispatchSearch : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:unaccent", ",,");

            migrationBuilder.CreateIndex(
                name: "ix_applications_ward_code_created_at_id",
                table: "applications",
                columns: new[] { "ward_code", "created_at", "id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_applications_ward_code_created_at_id",
                table: "applications");

            migrationBuilder.AlterDatabase()
                .OldAnnotation("Npgsql:PostgresExtension:unaccent", ",,");
        }
    }
}
