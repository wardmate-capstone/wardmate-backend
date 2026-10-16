using Microsoft.EntityFrameworkCore.Migrations;
#nullable disable
namespace WardMate.Services.ApplicationWorkflow.Infrastructure.Migrations;
public partial class DocumentReadiness : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<DateTime>(name: "documents_ready_at", table: "applications", type: "timestamp with time zone", nullable: true);
        migrationBuilder.DropCheckConstraint(name: "ck_applications_status", table: "applications");
        // Keep historical decisions and timestamps; never manufacture readiness from old decisions.
        migrationBuilder.AddCheckConstraint(name: "ck_applications_status", table: "applications",
            sql: "status IN ('DRAFT','SUBMITTED','UNDER_REVIEW','NEED_REVISION','DOCUMENTS_READY','APPROVED','CANCELLED','REJECTED')");
    }
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DO $$ BEGIN
                IF EXISTS (SELECT 1 FROM applications WHERE status = 'DOCUMENTS_READY') THEN
                    RAISE EXCEPTION 'Cannot roll back while DOCUMENTS_READY applications exist.';
                END IF;
            END $$;
            """);
        migrationBuilder.DropCheckConstraint(name: "ck_applications_status", table: "applications");
        migrationBuilder.AddCheckConstraint(name: "ck_applications_status", table: "applications",
            sql: "status IN ('DRAFT','SUBMITTED','UNDER_REVIEW','NEED_REVISION','APPROVED','CANCELLED','REJECTED')");
        migrationBuilder.DropColumn(name: "documents_ready_at", table: "applications");
    }
}
