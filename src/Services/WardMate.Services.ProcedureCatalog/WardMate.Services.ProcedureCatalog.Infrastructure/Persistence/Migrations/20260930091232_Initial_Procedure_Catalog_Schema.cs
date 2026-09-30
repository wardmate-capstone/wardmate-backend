using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace WardMate.Services.ProcedureCatalog.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Initial_Procedure_Catalog_Schema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "procedure_categories",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    category_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    description = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_procedure_categories", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "procedures",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    category_id = table.Column<int>(type: "integer", nullable: false),
                    procedure_code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    title = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    issuing_authority = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    executing_agency = table.Column<string>(type: "text", nullable: true),
                    level_of_implementation = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, defaultValue: "Cấp Xã"),
                    target_audience = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false, defaultValue: "Công dân Việt Nam"),
                    fee_summary = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false, defaultValue: "Miễn phí"),
                    processing_time_summary = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false, defaultValue: "1 ngày"),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    content_payload = table.Column<string>(type: "jsonb", nullable: false),
                    checklist_schema = table.Column<string>(type: "jsonb", nullable: true),
                    form_definitions = table.Column<string>(type: "jsonb", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_procedures", x => x.id);
                    table.ForeignKey(
                        name: "fk_procedures_procedure_categories_category_id",
                        column: x => x.category_id,
                        principalTable: "procedure_categories",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "procedure_versions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    procedure_id = table.Column<Guid>(type: "uuid", nullable: false),
                    version_number = table.Column<int>(type: "integer", nullable: false),
                    decision_number = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    effective_date = table.Column<DateOnly>(type: "date", nullable: false),
                    snapshot_data = table.Column<string>(type: "jsonb", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_procedure_versions", x => x.id);
                    table.ForeignKey(
                        name: "fk_procedure_versions_procedures_procedure_id",
                        column: x => x.procedure_id,
                        principalTable: "procedures",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "procedure_categories",
                columns: new[] { "id", "category_name", "description" },
                values: new object[,]
                {
                    { 1, "Hộ tịch", "Các thủ tục hộ tịch" },
                    { 2, "Đất đai", "Các thủ tục đất đai" },
                    { 3, "Quản lý công sản", "Các thủ tục quản lý công sản" }
                });

            migrationBuilder.InsertData(
                table: "procedures",
                columns: new[] { "id", "category_id", "checklist_schema", "content_payload", "created_at", "executing_agency", "fee_summary", "form_definitions", "is_active", "issuing_authority", "level_of_implementation", "procedure_code", "processing_time_summary", "target_audience", "title", "updated_at" },
                values: new object[] { new Guid("07000000-0000-0000-0000-000000000001"), 1, "[{\"checklistId\":\"TO_KHAI\",\"caseCode\":\"THONG_THUONG\",\"submissionType\":\"NOP\",\"itemName\":\"T\\u1EDD khai \\u0111\\u0103ng k\\u00FD k\\u1EBFt h\\u00F4n (m\\u1EABu)\",\"documentCopyType\":\"ORIGINAL\",\"quantity\":1,\"conditionNote\":null,\"isMandatory\":true},{\"checklistId\":\"DINH_DANH\",\"caseCode\":null,\"submissionType\":\"XUAT_TRINH\",\"itemName\":\"Gi\\u1EA5y t\\u1EDD \\u0111\\u1ECBnh danh (m\\u1EABu)\",\"documentCopyType\":\"ORIGINAL\",\"quantity\":1,\"conditionNote\":\"\\u0110\\u1ED1i chi\\u1EBFu th\\u00F4ng tin minh h\\u1ECDa\",\"isMandatory\":true}]", "{\"decisionNumber\":\"DEMO-QD-01\",\"receivingAddress\":\"B\\u1ED9 ph\\u1EADn ti\\u1EBFp nh\\u1EADn v\\u00E0 tr\\u1EA3 k\\u1EBFt qu\\u1EA3 (minh h\\u1ECDa)\",\"submissionMethods\":[{\"methodName\":\"Tr\\u1EF1c ti\\u1EBFp\",\"feeAmount\":0,\"feeUnit\":\"VND\",\"estimatedDays\":1,\"note\":\"D\\u1EEF li\\u1EC7u th\\u1EED nghi\\u1EC7m \\u00E1nh x\\u1EA1 JSONB\"}],\"legalReferences\":[{\"documentNumber\":\"DEMO-VB-01\",\"documentName\":\"V\\u0103n b\\u1EA3n minh h\\u1ECDa, kh\\u00F4ng d\\u00F9ng l\\u00E0m c\\u0103n c\\u1EE9 ph\\u00E1p l\\u00FD\",\"issueDate\":\"2026-09-01\",\"authority\":\"C\\u01A1 quan minh h\\u1ECDa\"}],\"results\":[\"Gi\\u1EA5y ch\\u1EE9ng nh\\u1EADn k\\u1EBFt h\\u00F4n (minh h\\u1ECDa)\"],\"cases\":[{\"caseCode\":\"THONG_THUONG\",\"caseName\":\"Tr\\u01B0\\u1EDDng h\\u1EE3p th\\u00F4ng th\\u01B0\\u1EDDng (minh h\\u1ECDa)\",\"steps\":[{\"stepOrder\":1,\"stepName\":\"Ti\\u1EBFp nh\\u1EADn\",\"executor\":\"C\\u00E1n b\\u1ED9 ti\\u1EBFp nh\\u1EADn\",\"actionDetails\":\"Ki\\u1EC3m tra th\\u00E0nh ph\\u1EA7n h\\u1ED3 s\\u01A1 m\\u1EABu\"},{\"stepOrder\":2,\"stepName\":\"Tr\\u1EA3 k\\u1EBFt qu\\u1EA3\",\"executor\":\"C\\u00E1n b\\u1ED9 h\\u1ED9 t\\u1ECBch\",\"actionDetails\":\"Tr\\u1EA3 k\\u1EBFt qu\\u1EA3 theo quy tr\\u00ECnh minh h\\u1ECDa\"}]}]}", new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc), "Ủy ban nhân dân cấp xã", "Miễn phí", "[{\"formTemplateId\":null,\"caseCode\":\"THONG_THUONG\",\"formCode\":\"DEMO-TK-01\",\"formName\":\"T\\u1EDD khai \\u0111i\\u1EC7n t\\u1EED minh h\\u1ECDa\",\"formType\":\"ONLINE_INTERACTIVE\",\"quantity\":1,\"isMandatory\":true},{\"formTemplateId\":null,\"caseCode\":null,\"formCode\":\"DEMO-DOCX-01\",\"formName\":\"M\\u1EABu DOCX minh h\\u1ECDa\",\"formType\":\"DOCX_TEMPLATE\",\"quantity\":1,\"isMandatory\":false}]", true, "Cơ quan ban hành minh họa", "Cấp Xã", "DEMO-KET-HON", "1 ngày", "Công dân Việt Nam", "Đăng ký kết hôn (dữ liệu minh họa)", new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc) });

            migrationBuilder.CreateIndex(
                name: "ix_procedure_versions_procedure_id_version_number",
                table: "procedure_versions",
                columns: new[] { "procedure_id", "version_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_procedures_category_id",
                table: "procedures",
                column: "category_id");

            migrationBuilder.CreateIndex(
                name: "ix_procedures_checklist_schema",
                table: "procedures",
                column: "checklist_schema")
                .Annotation("Npgsql:IndexMethod", "gin");

            migrationBuilder.CreateIndex(
                name: "ix_procedures_content_payload",
                table: "procedures",
                column: "content_payload")
                .Annotation("Npgsql:IndexMethod", "gin");

            migrationBuilder.CreateIndex(
                name: "ix_procedures_procedure_code",
                table: "procedures",
                column: "procedure_code",
                unique: true)
                .Annotation("Npgsql:IndexMethod", "btree");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "procedure_versions");

            migrationBuilder.DropTable(
                name: "procedures");

            migrationBuilder.DropTable(
                name: "procedure_categories");
        }
    }
}
