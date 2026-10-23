using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace WardMate.Services.IAM.Infrastructure.Persistence.Migrations;

[DbContext(typeof(IamDbContext))]
[Migration("20261024010000_ManagerAssignmentPermission")]
public sealed class ManagerAssignmentPermission : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        DO $assignment$
        BEGIN
          PERFORM pg_advisory_xact_lock(1464680777);
          LOCK TABLE permissions IN SHARE ROW EXCLUSIVE MODE;
          PERFORM setval(pg_get_serial_sequence('permissions','id'), GREATEST(COALESCE((SELECT MAX(id) FROM permissions),1), (SELECT last_value FROM permissions_id_seq)), true);
          INSERT INTO permissions(permission_code,permission_name,module)
          VALUES ('workflow.assign.manage','Giao/chuyển hồ sơ cho cán bộ trong phường','ApplicationWorkflow')
          ON CONFLICT(permission_code) DO NOTHING;
          INSERT INTO role_permissions(role_id,permission_id)
          SELECT r.id,p.id FROM roles r CROSS JOIN permissions p
          WHERE r.role_name='MANAGER' AND p.permission_code='workflow.assign.manage'
          ON CONFLICT(role_id,permission_id) DO NOTHING;
        END $assignment$;
        """);
    protected override void Down(MigrationBuilder migrationBuilder) => throw new NotSupportedException("Đối soát quyền trước khi rollback phân công.");
}
