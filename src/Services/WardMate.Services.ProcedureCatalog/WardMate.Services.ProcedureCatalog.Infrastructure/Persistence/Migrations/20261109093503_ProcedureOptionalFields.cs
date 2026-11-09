using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WardMate.Services.ProcedureCatalog.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// PROC-01b: cấp thực hiện, đối tượng, tóm tắt lệ phí/thời hạn được null; bỏ giá trị mặc định cố định
    /// (nếu giữ default, EF bỏ qua null và DB tự điền "Miễn phí"/"1 ngày"...). Không đổi dữ liệu đang có.
    /// </summary>
    public partial class ProcedureOptionalFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "target_audience",
                table: "procedures",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(255)",
                oldMaxLength: 255,
                oldDefaultValue: "Công dân Việt Nam");

            migrationBuilder.AlterColumn<string>(
                name: "processing_time_summary",
                table: "procedures",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(255)",
                oldMaxLength: 255,
                oldDefaultValue: "1 ngày");

            migrationBuilder.AlterColumn<string>(
                name: "level_of_implementation",
                table: "procedures",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50,
                oldDefaultValue: "Cấp Xã");

            migrationBuilder.AlterColumn<string>(
                name: "fee_summary",
                table: "procedures",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(255)",
                oldMaxLength: 255,
                oldDefaultValue: "Miễn phí");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "target_audience",
                table: "procedures",
                type: "character varying(255)",
                maxLength: 255,
                nullable: false,
                defaultValue: "Công dân Việt Nam",
                oldClrType: typeof(string),
                oldType: "character varying(255)",
                oldMaxLength: 255,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "processing_time_summary",
                table: "procedures",
                type: "character varying(255)",
                maxLength: 255,
                nullable: false,
                defaultValue: "1 ngày",
                oldClrType: typeof(string),
                oldType: "character varying(255)",
                oldMaxLength: 255,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "level_of_implementation",
                table: "procedures",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "Cấp Xã",
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "fee_summary",
                table: "procedures",
                type: "character varying(255)",
                maxLength: 255,
                nullable: false,
                defaultValue: "Miễn phí",
                oldClrType: typeof(string),
                oldType: "character varying(255)",
                oldMaxLength: 255,
                oldNullable: true);
        }
    }
}
