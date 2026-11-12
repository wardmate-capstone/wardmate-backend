using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WardMate.Services.ProcedureCatalog.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ProcedureUnpublish : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "first_published_at",
                table: "procedures",
                type: "timestamp with time zone",
                nullable: true);

            // PROC-UNPUBLISH-01: thủ tục có sẵn coi như đã từng công khai (an toàn: không bị hủy xuất bản nhầm).
            migrationBuilder.Sql("UPDATE procedures SET first_published_at = COALESCE(updated_at, created_at, now()) WHERE first_published_at IS NULL;");

            migrationBuilder.UpdateData(
                table: "procedures",
                keyColumn: "id",
                keyValue: new Guid("07000000-0000-0000-0000-000000000001"),
                column: "first_published_at",
                value: new DateTime(2026, 9, 30, 0, 0, 0, 0, DateTimeKind.Utc));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "first_published_at",
                table: "procedures");
        }
    }
}
