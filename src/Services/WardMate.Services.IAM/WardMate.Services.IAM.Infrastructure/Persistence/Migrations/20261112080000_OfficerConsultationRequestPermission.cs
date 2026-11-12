using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace WardMate.Services.IAM.Infrastructure.Persistence.Migrations;

[DbContext(typeof(IamDbContext))]
[Migration("20261112080000_OfficerConsultationRequestPermission")]
public sealed class OfficerConsultationRequestPermission : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        SELECT pg_advisory_xact_lock(1464680777);
        LOCK TABLE permissions IN SHARE ROW EXCLUSIVE MODE;
        SELECT setval(pg_get_serial_sequence('permissions', 'id'), GREATEST(COALESCE((SELECT MAX(id) FROM permissions), 1), (SELECT last_value FROM permissions_id_seq)), true);
        INSERT INTO permissions(permission_code, permission_name, module) VALUES
          ('workflow.consultation.request','Yêu cầu tham vấn cho hồ sơ đang phụ trách','WORKFLOW')
        ON CONFLICT (permission_code) DO NOTHING;
        INSERT INTO role_permissions(role_id, permission_id)
        SELECT r.id,p.id FROM roles r CROSS JOIN permissions p
        WHERE r.role_name = 'FRONT_DESK_OFFICER' AND p.permission_code = 'workflow.consultation.request'
        ON CONFLICT (role_id,permission_id) DO NOTHING;
        """);
    protected override void Down(MigrationBuilder migrationBuilder) =>
        throw new NotSupportedException("Cần đối soát quyền trước khi rollback; không tự xóa quyền đã được quản trị viên chỉnh sửa.");
}
