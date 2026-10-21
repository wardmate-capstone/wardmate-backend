using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
namespace WardMate.Services.IAM.Infrastructure.Persistence.Migrations;
[DbContext(typeof(IamDbContext))]
[Migration("20261021100000_CounterReceiptPermissions")]
public sealed class CounterReceiptPermissions : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        DO $p1$ BEGIN
        PERFORM pg_advisory_xact_lock(1464680777);
        LOCK TABLE permissions IN SHARE ROW EXCLUSIVE MODE;
        PERFORM setval(pg_get_serial_sequence('permissions','id'), GREATEST(COALESCE((SELECT MAX(id) FROM permissions),1), (SELECT last_value FROM permissions_id_seq)), true);
        INSERT INTO permissions(permission_code,permission_name,module) VALUES
          ('workflow.receipt.confirm','Tiếp nhận hồ sơ giấy tại quầy','ApplicationWorkflow'),
          ('workflow.cancel','Hủy hồ sơ tiền kiểm của chính mình','ApplicationWorkflow')
        ON CONFLICT (permission_code) DO NOTHING;
        INSERT INTO role_permissions(role_id,permission_id)
        SELECT r.id,p.id FROM roles r CROSS JOIN permissions p
        WHERE (r.role_name='FRONT_DESK_OFFICER' AND p.permission_code='workflow.receipt.confirm')
           OR (p.permission_code='workflow.cancel' AND EXISTS
               (SELECT 1 FROM role_permissions rp JOIN permissions old ON old.id=rp.permission_id WHERE rp.role_id=r.id AND old.permission_code='workflow.submit'))
        ON CONFLICT (role_id,permission_id) DO NOTHING;
        END $p1$;
        """);
    protected override void Down(MigrationBuilder migrationBuilder) =>
        throw new NotSupportedException("Đối soát quyền trước khi rollback P1.");
}
