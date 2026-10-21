using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
namespace WardMate.Services.IAM.Infrastructure.Persistence.Migrations;

[DbContext(typeof(IamDbContext))]
[Migration("20261021090000_ManagerWorkflowReadScope")]
public sealed class ManagerWorkflowReadScope : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        INSERT INTO role_permissions(role_id, permission_id)
        SELECT r.id, p.id FROM roles r CROSS JOIN permissions p
        WHERE r.role_name = 'MANAGER' AND p.permission_code IN ('workflow.queue.read', 'workflow.read')
        ON CONFLICT (role_id, permission_id) DO NOTHING;
        DELETE FROM role_permissions rp USING roles r, permissions p
        WHERE rp.role_id = r.id AND rp.permission_id = p.id AND r.role_name = 'MANAGER'
        AND p.permission_code IN ('workflow.assign','workflow.comments.write','workflow.revision.request','workflow.readiness.confirm');
        """);
    protected override void Down(MigrationBuilder migrationBuilder) =>
        throw new NotSupportedException("Khôi phục grant cũ cần đối soát bản sao lưu; không tự cấp lại quyền thao tác cho Manager.");
}
