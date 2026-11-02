using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using WardMate.Auditing;
using WardMate.Services.AnalyticsSystem.Infrastructure;

namespace WardMate.Services.AnalyticsSystem.Infrastructure.AuditMigrations;

[DbContext(typeof(NotificationDbContext))]
[Migration("20261102100006_TransactionalAuditSourceNotificationDbContext")]
public sealed class TransactionalAuditSourceNotificationDbContext : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => AuditSourceMigration.Install(migrationBuilder, "notifications",
            ("public","notifications","id"),
            ("public","notification_deliveries","Id"));
    protected override void Down(MigrationBuilder migrationBuilder) => AuditSourceMigration.Remove(migrationBuilder,
            ("public","notifications"),
            ("public","notification_deliveries"));
}
