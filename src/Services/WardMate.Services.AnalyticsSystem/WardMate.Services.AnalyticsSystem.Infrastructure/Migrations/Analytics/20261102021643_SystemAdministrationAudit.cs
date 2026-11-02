using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WardMate.Services.AnalyticsSystem.Infrastructure.Migrations.Analytics
{
    /// <inheritdoc />
    public partial class SystemAdministrationAudit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "audit_logs",
                schema: "analytics",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: false),
                    Action = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    EntityKey = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    PreviousValue = table.Column<int>(type: "integer", nullable: true),
                    NewValue = table.Column<int>(type: "integer", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    ClientIp = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    TraceId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    OccurredAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_audit_logs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "system_settings",
                schema: "analytics",
                columns: table => new
                {
                    Key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Value = table.Column<int>(type: "integer", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_system_settings", x => x.Key);
                    table.CheckConstraint("ck_system_setting_value", "\"Value\" BETWEEN 1 AND 366");
                });

            migrationBuilder.CreateIndex(
                name: "IX_audit_logs_OccurredAtUtc_Id",
                schema: "analytics",
                table: "audit_logs",
                columns: new[] { "OccurredAtUtc", "Id" });
            migrationBuilder.Sql("""
                CREATE FUNCTION analytics.reject_audit_mutation() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                    RAISE EXCEPTION 'Audit logs are append-only';
                END;
                $$;
                CREATE TRIGGER audit_logs_append_only BEFORE UPDATE OR DELETE ON analytics.audit_logs
                FOR EACH ROW EXECUTE FUNCTION analytics.reject_audit_mutation();
                CREATE TRIGGER audit_logs_no_truncate BEFORE TRUNCATE ON analytics.audit_logs
                FOR EACH STATEMENT EXECUTE FUNCTION analytics.reject_audit_mutation();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "audit_logs",
                schema: "analytics");

            migrationBuilder.Sql("DROP FUNCTION analytics.reject_audit_mutation();");

            migrationBuilder.DropTable(
                name: "system_settings",
                schema: "analytics");
        }
    }
}
