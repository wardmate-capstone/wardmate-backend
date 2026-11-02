using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using WardMate.Auditing;
using WardMate.Services.IAM.Infrastructure.Persistence;

namespace WardMate.Services.IAM.Infrastructure.AuditMigrations;

[DbContext(typeof(IamDbContext))]
[Migration("20261102100000_TransactionalAuditSourceIamDbContext")]
public sealed class TransactionalAuditSourceIamDbContext : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => AuditSourceMigration.Install(migrationBuilder, "iam",
            ("public","users","id"),
            ("public","user_profiles","user_id"),
            ("public","roles","id"),
            ("public","permissions","id"),
            ("public","user_roles","user_id,role_id"),
            ("public","role_permissions","role_id,permission_id"),
            ("public","wards","id"));
    protected override void Down(MigrationBuilder migrationBuilder) => AuditSourceMigration.Remove(migrationBuilder,
            ("public","users"),
            ("public","user_profiles"),
            ("public","roles"),
            ("public","permissions"),
            ("public","user_roles"),
            ("public","role_permissions"),
            ("public","wards"));
}
