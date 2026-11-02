using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using WardMate.Auditing;
using WardMate.Services.AnalyticsSystem.Infrastructure;

namespace WardMate.Services.AnalyticsSystem.Infrastructure.AuditMigrations;

[DbContext(typeof(AnalyticsDbContext))]
[Migration("20261102100005_TransactionalAuditSourceAnalyticsDbContext")]
public sealed class TransactionalAuditSourceAnalyticsDbContext : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => AuditSourceMigration.Install(migrationBuilder, "analytics",
            ("analytics","citizen_feedback","Id"),
            ("analytics","system_settings","Key"));
    protected override void Down(MigrationBuilder migrationBuilder) => AuditSourceMigration.Remove(migrationBuilder,
            ("analytics","citizen_feedback"),
            ("analytics","system_settings"));
}
