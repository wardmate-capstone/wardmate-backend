using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WardMate.Services.AnalyticsSystem.Infrastructure.Migrations.Analytics
{
    /// <inheritdoc />
    public partial class DurableHealthChecks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "health_check_requests",
                schema: "analytics",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CompletedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_health_check_requests", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_health_check_requests_CompletedAtUtc_RequestedAtUtc",
                schema: "analytics",
                table: "health_check_requests",
                columns: new[] { "CompletedAtUtc", "RequestedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "health_check_requests",
                schema: "analytics");
        }
    }
}
