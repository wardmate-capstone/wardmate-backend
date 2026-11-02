using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using WardMate.Auditing;
using WardMate.Services.ApplicationWorkflow.Infrastructure;

namespace WardMate.Services.ApplicationWorkflow.Infrastructure.AuditMigrations;

[DbContext(typeof(WorkflowDbContext))]
[Migration("20261102100002_TransactionalAuditSourceWorkflowDbContext")]
public sealed class TransactionalAuditSourceWorkflowDbContext : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => AuditSourceMigration.Install(migrationBuilder, "workflow",
            ("public","applications","id"),
            ("public","application_checklists","id"),
            ("public","application_comments","id"),
            ("public","application_consultations","id"),
            ("public","application_assignment_history","id"),
            ("public","application_status_history","id"),
            ("public","application_versions","id"),
            ("public","officer_shifts","id"),
            ("public","officer_leaves","id"),
            ("public","officer_leave_decisions","id"),
            ("public","calendar_closures","id"));
    protected override void Down(MigrationBuilder migrationBuilder) => AuditSourceMigration.Remove(migrationBuilder,
            ("public","applications"),
            ("public","application_checklists"),
            ("public","application_comments"),
            ("public","application_consultations"),
            ("public","application_assignment_history"),
            ("public","application_status_history"),
            ("public","application_versions"),
            ("public","officer_shifts"),
            ("public","officer_leaves"),
            ("public","officer_leave_decisions"),
            ("public","calendar_closures"));
}
