using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WardMate.Services.AnalyticsSystem.Infrastructure.Migrations.Analytics
{
    /// <inheritdoc />
    public partial class SystemOperationsHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "service_health_logs",
                schema: "analytics",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ServiceName = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    HttpStatus = table.Column<int>(type: "integer", nullable: true),
                    ResponseTimeMs = table.Column<long>(type: "bigint", nullable: false),
                    ErrorCode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CheckedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_service_health_logs", x => x.Id);
                    table.CheckConstraint("ck_health_duration", "\"ResponseTimeMs\" >= 0");
                    table.CheckConstraint("ck_health_status", "\"Status\" IN ('Unknown','Healthy','Degraded','Unhealthy')");
                });

            migrationBuilder.CreateTable(
                name: "system_backup_logs",
                schema: "analytics",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ServiceName = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    StorageObjectName = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: true),
                    Sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    RequestedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    StartedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CompletedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    VerifiedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RestoreTestedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ErrorCode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_system_backup_logs", x => x.Id);
                    table.CheckConstraint("ck_backup_size", "\"SizeBytes\" IS NULL OR \"SizeBytes\" >= 0");
                    table.CheckConstraint("ck_backup_status", "\"Status\" IN ('Queued','Running','Succeeded','Failed','Verified')");
                });

            migrationBuilder.CreateIndex(
                name: "IX_service_health_logs_ServiceName_CheckedAtUtc",
                schema: "analytics",
                table: "service_health_logs",
                columns: new[] { "ServiceName", "CheckedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_service_health_logs_Status_CheckedAtUtc",
                schema: "analytics",
                table: "service_health_logs",
                columns: new[] { "Status", "CheckedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_system_backup_logs_ServiceName",
                schema: "analytics",
                table: "system_backup_logs",
                column: "ServiceName",
                unique: true,
                filter: "\"Status\" IN ('Queued','Running')");

            migrationBuilder.CreateIndex(
                name: "IX_system_backup_logs_ServiceName_CreatedAtUtc",
                schema: "analytics",
                table: "system_backup_logs",
                columns: new[] { "ServiceName", "CreatedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "service_health_logs",
                schema: "analytics");

            migrationBuilder.DropTable(
                name: "system_backup_logs",
                schema: "analytics");
        }
    }
}
