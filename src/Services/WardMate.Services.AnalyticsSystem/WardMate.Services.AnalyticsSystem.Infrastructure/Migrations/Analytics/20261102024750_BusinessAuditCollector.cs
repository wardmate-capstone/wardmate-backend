using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WardMate.Services.AnalyticsSystem.Infrastructure.Migrations.Analytics
{
    /// <inheritdoc />
    public partial class BusinessAuditCollector : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "business_audit_events",
                schema: "analytics",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Source = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Entity = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Action = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: true),
                    TraceId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    OccurredAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ReceivedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Payload = table.Column<string>(type: "jsonb", nullable: false),
                    PayloadHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_business_audit_events", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_business_audit_events_ActorId_OccurredAtUtc",
                schema: "analytics",
                table: "business_audit_events",
                columns: new[] { "ActorId", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_business_audit_events_OccurredAtUtc_Id",
                schema: "analytics",
                table: "business_audit_events",
                columns: new[] { "OccurredAtUtc", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_business_audit_events_Source_Entity_OccurredAtUtc",
                schema: "analytics",
                table: "business_audit_events",
                columns: new[] { "Source", "Entity", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_business_audit_events_TraceId",
                schema: "analytics",
                table: "business_audit_events",
                column: "TraceId");
            migrationBuilder.Sql("""
                CREATE TRIGGER business_audit_append_only BEFORE UPDATE OR DELETE ON analytics.business_audit_events
                FOR EACH ROW EXECUTE FUNCTION analytics.reject_audit_mutation();
                CREATE TRIGGER business_audit_no_truncate BEFORE TRUNCATE ON analytics.business_audit_events
                FOR EACH STATEMENT EXECUTE FUNCTION analytics.reject_audit_mutation();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "business_audit_events",
                schema: "analytics");
        }
    }
}
