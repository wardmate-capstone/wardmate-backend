using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WardMate.Services.IAM.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// IAM-ROLE-DESC-01 (L-30): Việt hóa mô tả 5 vai trò hệ thống. Chỉ ghi khi mô tả còn trống hoặc vẫn là câu tiếng Anh
    /// của seed cũ, để không ghi đè mô tả Quản trị hệ thống đã sửa trên môi trường đang chạy.
    /// </summary>
    public partial class RoleDescriptionsVi : Migration
    {
        private static readonly (int Id, string Old, string New)[] Rows =
        [
            (1, "Registered citizen", "Công dân đã đăng ký tài khoản, tự tạo và theo dõi hồ sơ của mình"),
            (2, "Front desk officer", "Cán bộ một cửa: kiểm tra giấy tờ và tiếp nhận hồ sơ tại quầy"),
            (3, "Manager", "Quản lý phường: giám sát hồ sơ, phân công và xếp ca cán bộ"),
            (4, "Procedure catalog manager", "Quản lý thủ tục: soạn, xuất bản thủ tục và biểu mẫu"),
            (5, "IT administrator", "Quản trị hệ thống: tài khoản, vai trò, phường và cấu hình"),
        ];

        private static string Quote(string value) => "'" + value.Replace("'", "''") + "'";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            foreach (var (id, old, @new) in Rows)
                migrationBuilder.Sql($"UPDATE roles SET description = {Quote(@new)} WHERE id = {id} AND (description IS NULL OR btrim(description) = '' OR description = {Quote(old)});");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            foreach (var (id, old, @new) in Rows)
                migrationBuilder.Sql($"UPDATE roles SET description = {Quote(old)} WHERE id = {id} AND description = {Quote(@new)};");
        }
    }
}
