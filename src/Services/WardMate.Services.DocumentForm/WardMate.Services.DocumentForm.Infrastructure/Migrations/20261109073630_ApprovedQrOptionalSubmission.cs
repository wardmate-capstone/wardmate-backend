using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WardMate.Services.DocumentForm.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ApprovedQrOptionalSubmission : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Guid>(
                name: "submission_id",
                schema: "document",
                table: "approved_qrs",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Guid>(
                name: "submission_id",
                schema: "document",
                table: "approved_qrs",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);
        }
    }
}
