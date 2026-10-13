using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WardMate.Services.IAM.Infrastructure.Persistence.Migrations;

public partial class DeployedFeaturePermissionCatalog : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            SELECT pg_advisory_xact_lock(1464680777);
            LOCK TABLE permissions IN SHARE ROW EXCLUSIVE MODE;
            SELECT setval(pg_get_serial_sequence('permissions', 'id'),
                GREATEST(COALESCE((SELECT MAX(id) FROM permissions), 1),
                    COALESCE(pg_sequence_last_value(pg_get_serial_sequence('permissions', 'id')::regclass), 1)), true);

            CREATE TEMP TABLE wardmate_feature_permissions (
                code text, name text, module text, role_names text[]
            ) ON COMMIT DROP;
            INSERT INTO wardmate_feature_permissions VALUES
            ('iam.accounts.read', 'Xem tài khoản và hồ sơ trong phạm vi quản lý', 'IAM', ARRAY['IT_ADMIN','MANAGER']),
            ('iam.accounts.manage', 'Quản lý tài khoản và hồ sơ trong phạm vi được phép', 'IAM', ARRAY['IT_ADMIN','MANAGER']),
            ('iam.wards.read', 'Xem phường trong phạm vi quản lý', 'IAM', ARRAY['IT_ADMIN','MANAGER']),
            ('iam.wards.manage', 'Tạo phường và phân công phường công tác', 'IAM', ARRAY['IT_ADMIN']),
            ('iam.rbac.manage', 'Quản trị vai trò và quyền toàn hệ thống', 'IAM', ARRAY['IT_ADMIN']),
            ('iam.audit.read', 'Xem nhật ký phân quyền', 'IAM', ARRAY['IT_ADMIN']),
            ('procedure.read', 'Xem toàn bộ thủ tục, kể cả đã ngừng công khai', 'ProcedureCatalog', ARRAY['IT_ADMIN','PROCEDURE_MANAGER']),
            ('procedure.create', 'Tạo thủ tục', 'ProcedureCatalog', ARRAY['IT_ADMIN','PROCEDURE_MANAGER']),
            ('procedure.update', 'Cập nhật nội dung thủ tục', 'ProcedureCatalog', ARRAY['IT_ADMIN','PROCEDURE_MANAGER']),
            ('procedure.publish', 'Xuất bản dữ liệu thủ tục đã đối soát', 'ProcedureCatalog', ARRAY['IT_ADMIN','PROCEDURE_MANAGER']),
            ('procedure.status', 'Bật hoặc tắt thủ tục', 'ProcedureCatalog', ARRAY['IT_ADMIN','PROCEDURE_MANAGER']),
            ('procedure.versions.read', 'Xem lịch sử phiên bản thủ tục', 'ProcedureCatalog', ARRAY['IT_ADMIN','PROCEDURE_MANAGER']),
            ('procedure.rollback', 'Khôi phục phiên bản thủ tục', 'ProcedureCatalog', ARRAY['IT_ADMIN','PROCEDURE_MANAGER']),
            ('procedure.source.read', 'Đọc PDF gốc của phiên bản lịch sử', 'ProcedureCatalog', ARRAY['IT_ADMIN','PROCEDURE_MANAGER']),
            ('procedure.categories.manage', 'Thêm sửa xóa danh mục thủ tục', 'ProcedureCatalog', ARRAY['IT_ADMIN','PROCEDURE_MANAGER']),
            ('procedure.drafts.read', 'Xem bản nháp và PDF đối soát', 'ProcedureCatalog', ARRAY['IT_ADMIN','PROCEDURE_MANAGER']),
            ('procedure.drafts.upload', 'Tải PDF tạo bản nháp', 'ProcedureCatalog', ARRAY['IT_ADMIN','PROCEDURE_MANAGER']),
            ('procedure.drafts.update', 'Sửa dữ liệu bản nháp', 'ProcedureCatalog', ARRAY['IT_ADMIN','PROCEDURE_MANAGER']),
            ('procedure.drafts.extract', 'Đọc thử PDF và thử trích xuất lại', 'ProcedureCatalog', ARRAY['IT_ADMIN','PROCEDURE_MANAGER']),
            ('procedure.drafts.publish', 'Xác nhận và xuất bản bản nháp', 'ProcedureCatalog', ARRAY['IT_ADMIN','PROCEDURE_MANAGER']),
            ('procedure.drafts.delete', 'Xóa bản nháp chưa xuất bản', 'ProcedureCatalog', ARRAY['IT_ADMIN','PROCEDURE_MANAGER']),
            ('document.templates.read', 'Tra cứu và tải biểu mẫu gốc', 'DocumentForm', ARRAY['IT_ADMIN','PROCEDURE_MANAGER','MANAGER','FRONT_DESK_OFFICER','REGISTERED_CITIZEN']),
            ('document.templates.manage', 'Tạo biểu mẫu và tải DOCX gốc', 'DocumentForm', ARRAY['IT_ADMIN','PROCEDURE_MANAGER']),
            ('document.submissions.read', 'Xem đơn điện tử của chính mình', 'DocumentForm', ARRAY['IT_ADMIN','PROCEDURE_MANAGER','MANAGER','FRONT_DESK_OFFICER','REGISTERED_CITIZEN']),
            ('document.submissions.write', 'Tạo và sửa nháp đơn điện tử của chính mình', 'DocumentForm', ARRAY['IT_ADMIN','PROCEDURE_MANAGER','MANAGER','FRONT_DESK_OFFICER','REGISTERED_CITIZEN']),
            ('document.submissions.submit', 'Chốt đơn điện tử của chính mình', 'DocumentForm', ARRAY['IT_ADMIN','PROCEDURE_MANAGER','MANAGER','FRONT_DESK_OFFICER','REGISTERED_CITIZEN']),
            ('document.submissions.download', 'Tải DOCX đã điền của chính mình', 'DocumentForm', ARRAY['IT_ADMIN','PROCEDURE_MANAGER','MANAGER','FRONT_DESK_OFFICER','REGISTERED_CITIZEN']);

            INSERT INTO permissions (permission_code, permission_name, module)
            SELECT code, name, module FROM wardmate_feature_permissions
            ON CONFLICT (permission_code) DO NOTHING;

            INSERT INTO role_permissions (role_id, permission_id)
            SELECT r.id, p.id FROM wardmate_feature_permissions f
            JOIN permissions p ON p.permission_code = f.code
            JOIN roles r ON r.role_name = ANY(f.role_names)
            ON CONFLICT (role_id, permission_id) DO NOTHING;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Grants may have been customized after deployment. A downgrade must not silently remove them.
        throw new NotSupportedException("Không tự động thu hồi quyền nghiệp vụ. Hãy tạo migration điều chỉnh quyền có đối soát.");
    }
}
