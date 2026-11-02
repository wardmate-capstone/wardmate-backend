using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using WardMate.Auditing;
using WardMate.Services.DocumentForm.Infrastructure.Persistence;

namespace WardMate.Services.DocumentForm.Infrastructure.AuditMigrations;

[DbContext(typeof(DocumentDbContext))]
[Migration("20261102100003_TransactionalAuditSourceDocumentDbContext")]
public sealed class TransactionalAuditSourceDocumentDbContext : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => AuditSourceMigration.Install(migrationBuilder, "documentform",
            ("document","application_forms","id"),
            ("document","approved_qrs","id"),
            ("document","form_templates","id"),
            ("document","form_template_versions","id"),
            ("document","generated_documents","id"),
            ("document","supporting_documents","id"),
            ("document","user_submissions","id"));
    protected override void Down(MigrationBuilder migrationBuilder) => AuditSourceMigration.Remove(migrationBuilder,
            ("document","application_forms"),
            ("document","approved_qrs"),
            ("document","form_templates"),
            ("document","form_template_versions"),
            ("document","generated_documents"),
            ("document","supporting_documents"),
            ("document","user_submissions"));
}
