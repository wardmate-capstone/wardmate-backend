using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WardMate.Services.AnalyticsSystem.Infrastructure.Migrations.Analytics
{
    /// <inheritdoc />
    public partial class AnalyticsMetricsAndCitizenFeedback : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "analytics");

            migrationBuilder.CreateTable(
                name: "application_procedure_metrics",
                schema: "analytics",
                columns: table => new
                {
                    MetricDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ProcedureId = table.Column<Guid>(type: "uuid", nullable: false),
                    WardCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ReceivedCount = table.Column<int>(type: "integer", nullable: false),
                    ReadyCount = table.Column<int>(type: "integer", nullable: false),
                    RevisionRequestedCount = table.Column<int>(type: "integer", nullable: false),
                    ResubmittedCount = table.Column<int>(type: "integer", nullable: false),
                    CancelledCount = table.Column<int>(type: "integer", nullable: false),
                    LegacyApprovedCount = table.Column<int>(type: "integer", nullable: false),
                    LegacyRejectedCount = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_application_procedure_metrics", x => new { x.MetricDate, x.WardCode, x.ProcedureId });
                });

            migrationBuilder.CreateTable(
                name: "citizen_feedback",
                schema: "analytics",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ApplicationId = table.Column<Guid>(type: "uuid", nullable: false),
                    CitizenId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProcedureId = table.Column<Guid>(type: "uuid", nullable: false),
                    WardCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Rating = table.Column<int>(type: "integer", nullable: false),
                    Comment = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_citizen_feedback", x => x.Id);
                    table.CheckConstraint("ck_feedback_rating", "\"Rating\" BETWEEN 1 AND 5");
                });

            migrationBuilder.CreateTable(
                name: "daily_performance_metrics",
                schema: "analytics",
                columns: table => new
                {
                    MetricDate = table.Column<DateOnly>(type: "date", nullable: false),
                    OfficerId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProcedureId = table.Column<Guid>(type: "uuid", nullable: false),
                    WardCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ReadyCount = table.Column<int>(type: "integer", nullable: false),
                    RevisionRequestedCount = table.Column<int>(type: "integer", nullable: false),
                    DurationSampleCount = table.Column<int>(type: "integer", nullable: false),
                    TotalProcessingMinutes = table.Column<double>(type: "double precision", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_daily_performance_metrics", x => new { x.MetricDate, x.WardCode, x.OfficerId, x.ProcedureId });
                });

            migrationBuilder.CreateTable(
                name: "metric_refreshes",
                schema: "analytics",
                columns: table => new
                {
                    MetricDate = table.Column<DateOnly>(type: "date", nullable: false),
                    SourceAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RefreshedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_metric_refreshes", x => x.MetricDate);
                });

            migrationBuilder.CreateIndex(
                name: "IX_application_procedure_metrics_WardCode_MetricDate",
                schema: "analytics",
                table: "application_procedure_metrics",
                columns: new[] { "WardCode", "MetricDate" });

            migrationBuilder.CreateIndex(
                name: "IX_citizen_feedback_ApplicationId",
                schema: "analytics",
                table: "citizen_feedback",
                column: "ApplicationId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_citizen_feedback_CitizenId_ApplicationId",
                schema: "analytics",
                table: "citizen_feedback",
                columns: new[] { "CitizenId", "ApplicationId" });

            migrationBuilder.CreateIndex(
                name: "IX_citizen_feedback_WardCode_CreatedAtUtc",
                schema: "analytics",
                table: "citizen_feedback",
                columns: new[] { "WardCode", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_daily_performance_metrics_WardCode_MetricDate",
                schema: "analytics",
                table: "daily_performance_metrics",
                columns: new[] { "WardCode", "MetricDate" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "application_procedure_metrics",
                schema: "analytics");

            migrationBuilder.DropTable(
                name: "citizen_feedback",
                schema: "analytics");

            migrationBuilder.DropTable(
                name: "daily_performance_metrics",
                schema: "analytics");

            migrationBuilder.DropTable(
                name: "metric_refreshes",
                schema: "analytics");
        }
    }
}
