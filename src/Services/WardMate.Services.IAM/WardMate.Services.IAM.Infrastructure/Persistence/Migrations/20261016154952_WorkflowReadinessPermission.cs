using Microsoft.EntityFrameworkCore.Migrations;
#nullable disable
namespace WardMate.Services.IAM.Infrastructure.Persistence.Migrations;
public partial class WorkflowReadinessPermission : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DO $readiness$ BEGIN
            PERFORM pg_advisory_xact_lock(1464680777);
            LOCK TABLE permissions IN SHARE ROW EXCLUSIVE MODE;
            PERFORM setval(pg_get_serial_sequence('permissions', 'id'),
                GREATEST(COALESCE((SELECT MAX(id) FROM permissions), 1), (SELECT last_value FROM permissions_id_seq)), true);
            INSERT INTO permissions(permission_code, permission_name, module)
            VALUES ('workflow.readiness.confirm', 'Xác nhận giấy tờ sẵn sàng để mang đến phường', 'ApplicationWorkflow')
            ON CONFLICT (permission_code) DO UPDATE SET permission_name = EXCLUDED.permission_name;
            INSERT INTO role_permissions(role_id, permission_id)
            SELECT rp.role_id, target.id
            FROM role_permissions rp
            JOIN permissions old ON old.id = rp.permission_id AND old.permission_code = 'workflow.approve'
            CROSS JOIN permissions target
            WHERE target.permission_code = 'workflow.readiness.confirm'
            ON CONFLICT (role_id, permission_id) DO NOTHING;
            DELETE FROM role_permissions WHERE permission_id IN
                (SELECT id FROM permissions WHERE permission_code IN ('workflow.approve', 'workflow.reject'));
            DELETE FROM permissions WHERE permission_code IN ('workflow.approve', 'workflow.reject');
            END $readiness$;
            """);
    }
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        throw new NotSupportedException("Khôi phục quyền cũ cần đối soát grant và bản sao lưu; không tự cấp lại quyền phê duyệt/từ chối.");
    }
}
