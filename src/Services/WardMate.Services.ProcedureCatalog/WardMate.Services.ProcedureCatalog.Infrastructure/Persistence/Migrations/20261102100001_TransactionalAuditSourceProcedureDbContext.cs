using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using WardMate.Auditing;
using WardMate.Services.ProcedureCatalog.Infrastructure.Persistence;

namespace WardMate.Services.ProcedureCatalog.Infrastructure.AuditMigrations;

[DbContext(typeof(ProcedureDbContext))]
[Migration("20261102100001_TransactionalAuditSourceProcedureDbContext")]
public sealed class TransactionalAuditSourceProcedureDbContext : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => AuditSourceMigration.Install(migrationBuilder, "catalog",
            ("public","procedures","id"),
            ("public","procedure_categories","id"),
            ("public","procedure_drafts","id"),
            ("public","procedure_versions","id"));
    protected override void Down(MigrationBuilder migrationBuilder) => AuditSourceMigration.Remove(migrationBuilder,
            ("public","procedures"),
            ("public","procedure_categories"),
            ("public","procedure_drafts"),
            ("public","procedure_versions"));
}
