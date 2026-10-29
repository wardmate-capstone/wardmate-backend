using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace WardMate.Services.IAM.Infrastructure.Persistence.Migrations;

[DbContext(typeof(IamDbContext))]
[Migration("20261029120000_ManagerStaffingPermissions")]
public sealed class ManagerStaffingPermissions : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        SELECT pg_advisory_xact_lock(1464680777);
        LOCK TABLE permissions IN SHARE ROW EXCLUSIVE MODE;
        SELECT setval(pg_get_serial_sequence('permissions', 'id'), GREATEST(COALESCE((SELECT MAX(id) FROM permissions), 1), (SELECT last_value FROM permissions_id_seq)), true);
        INSERT INTO permissions(permission_code, permission_name, module) VALUES
          ('workflow.dispatch.read','Xem đề xuất và lịch sử phân công','WORKFLOW'),
          ('workflow.dispatch.write','Phân công và chuyển cán bộ xử lý','WORKFLOW'),
          ('workflow.staffing.read','Xem lịch làm và yêu cầu nghỉ','WORKFLOW'),
          ('workflow.staffing.manage','Quản lý lịch làm và duyệt nghỉ','WORKFLOW'),
          ('workflow.leave.request','Gửi và hủy yêu cầu nghỉ của mình','WORKFLOW'),
          ('workflow.consultation.read','Xem tham vấn được phân quyền','WORKFLOW'),
          ('workflow.consultation.respond','Trả lời tham vấn được giao','WORKFLOW')
        ON CONFLICT (permission_code) DO NOTHING;
        INSERT INTO role_permissions(role_id, permission_id)
        SELECT r.id,p.id FROM roles r CROSS JOIN permissions p
        WHERE (r.role_name = 'MANAGER' AND p.permission_code IN
          ('workflow.dispatch.read','workflow.dispatch.write','workflow.staffing.read','workflow.staffing.manage','workflow.consultation.read'))
          OR (r.role_name = 'FRONT_DESK_OFFICER' AND p.permission_code IN ('workflow.staffing.read','workflow.leave.request','workflow.consultation.read','workflow.consultation.respond'))
        ON CONFLICT (role_id,permission_id) DO NOTHING;
        """);
    protected override void Down(MigrationBuilder migrationBuilder) =>
        throw new NotSupportedException("Cần đối soát quyền trước khi rollback; không tự xóa quyền đã được quản trị viên chỉnh sửa.");
}
