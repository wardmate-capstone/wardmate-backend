using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WardMate.Services.IAM.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class OfficerDirectoryCategories : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int[]>(
                name: "assigned_categories",
                table: "users",
                type: "integer[]",
                nullable: false,
                defaultValueSql: "ARRAY[]::integer[]");
            migrationBuilder.Sql("""
                SELECT pg_advisory_xact_lock(1464680777);
                LOCK TABLE permissions IN SHARE ROW EXCLUSIVE MODE;
                SELECT setval(pg_get_serial_sequence('permissions', 'id'),
                    GREATEST(COALESCE((SELECT MAX(id) FROM permissions), 1),
                    (SELECT last_value FROM permissions_id_seq)), true);
                CREATE TEMP TABLE workflow_permission_seed(code text, label text, officer_only boolean) ON COMMIT DROP;
                INSERT INTO workflow_permission_seed VALUES
                ('workflow.create','Tạo hồ sơ cá nhân',false),
                ('workflow.read','Xem hồ sơ cá nhân',false),
                ('workflow.checklist.write','Cập nhật checklist hồ sơ cá nhân',false),
                ('workflow.submit','Nộp hồ sơ cá nhân',false),
                ('workflow.resubmit','Nộp lại hồ sơ cá nhân',false),
                ('workflow.comments.read','Đọc nhận xét hồ sơ trong phạm vi',false),
                ('workflow.versions.read','Đọc phiên bản hồ sơ trong phạm vi',false),
                ('workflow.diff.read','So sánh phiên bản hồ sơ trong phạm vi',false),
                ('workflow.queue.read','Xem hàng đợi đúng phường',true),
                ('workflow.assign','Tiếp nhận hồ sơ đúng phường',true),
                ('workflow.comments.write','Bắt lỗi hồ sơ đang thụ lý',true),
                ('workflow.revision.request','Yêu cầu bổ sung hồ sơ',true),
                ('workflow.approve','Phê duyệt hồ sơ đang thụ lý',true),
                ('workflow.reject','Từ chối hồ sơ đang thụ lý',true);
                INSERT INTO permissions(permission_code, permission_name, module)
                    SELECT code, label, 'ApplicationWorkflow' FROM workflow_permission_seed
                    ON CONFLICT(permission_code) DO NOTHING;
                INSERT INTO role_permissions(role_id, permission_id)
                    SELECT r.id, p.id FROM roles r JOIN workflow_permission_seed s
                      ON (NOT s.officer_only AND r.role_name IN ('REGISTERED_CITIZEN','FRONT_DESK_OFFICER','MANAGER','PROCEDURE_MANAGER','IT_ADMIN'))
                        OR (s.officer_only AND r.role_name = 'FRONT_DESK_OFFICER')
                    JOIN permissions p ON p.permission_code = s.code
                    ON CONFLICT(role_id,permission_id) DO NOTHING;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "assigned_categories",
                table: "users");
        }
    }
}

