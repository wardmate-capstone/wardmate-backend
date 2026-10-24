using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace WardMate.Services.IAM.Infrastructure.Persistence.Migrations;

[DbContext(typeof(IamDbContext))]
[Migration("20261024100000_KnowledgeManagePermission")]
public sealed class KnowledgeManagePermission : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        DO $knowledge$
        BEGIN
          PERFORM pg_advisory_xact_lock(1464680777);
          LOCK TABLE permissions IN SHARE ROW EXCLUSIVE MODE;
          PERFORM setval(pg_get_serial_sequence('permissions','id'), GREATEST(COALESCE((SELECT MAX(id) FROM permissions),1), (SELECT last_value FROM permissions_id_seq)), true);
          INSERT INTO permissions(permission_code,permission_name,module)
          VALUES ('knowledge.manage','Quản lý Knowledge Base cho trợ lý AI','AIOCR')
          ON CONFLICT(permission_code) DO NOTHING;
          INSERT INTO role_permissions(role_id,permission_id)
          SELECT r.id,p.id FROM roles r CROSS JOIN permissions p
          WHERE r.role_name IN ('PROCEDURE_MANAGER','IT_ADMIN') AND p.permission_code='knowledge.manage'
          ON CONFLICT(role_id,permission_id) DO NOTHING;
        END $knowledge$;
        """);
    protected override void Down(MigrationBuilder migrationBuilder) => throw new NotSupportedException("Đối soát quyền knowledge.manage trước khi rollback.");
}
