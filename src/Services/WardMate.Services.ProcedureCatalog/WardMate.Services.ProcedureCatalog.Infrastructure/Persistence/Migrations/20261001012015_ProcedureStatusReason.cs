using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WardMate.Services.ProcedureCatalog.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ProcedureStatusReason : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "status_change_reason",
                table: "procedures",
                type: "text",
                nullable: true);

            migrationBuilder.UpdateData(
                table: "procedures",
                keyColumn: "id",
                keyValue: new Guid("07000000-0000-0000-0000-000000000001"),
                column: "status_change_reason",
                value: null);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "status_change_reason",
                table: "procedures");
        }
    }
}
