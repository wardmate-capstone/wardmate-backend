using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using WardMate.Auditing;
using WardMate.Services.AIOCR.Infrastructure;

namespace WardMate.Services.AIOCR.Infrastructure.AuditMigrations;

[DbContext(typeof(ChatDbContext))]
[Migration("20261102100004_TransactionalAuditSourceChatDbContext")]
public sealed class TransactionalAuditSourceChatDbContext : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => AuditSourceMigration.Install(migrationBuilder, "aiocr",
            ("public","chat_sessions","id"),
            ("public","chat_messages","id"),
            ("public","knowledge_documents","id"),
            ("public","knowledge_chunks","id"));
    protected override void Down(MigrationBuilder migrationBuilder) => AuditSourceMigration.Remove(migrationBuilder,
            ("public","chat_sessions"),
            ("public","chat_messages"),
            ("public","knowledge_documents"),
            ("public","knowledge_chunks"));
}
