using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WardMate.Services.DocumentForm.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddApprovedQrTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "approved_qrs",
                schema: "document",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuid_generate_v4()"),
                    application_id = table.Column<Guid>(type: "uuid", nullable: false),
                    submission_id = table.Column<Guid>(type: "uuid", nullable: false),
                    qr_code_value = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    qr_image_png = table.Column<byte[]>(type: "bytea", nullable: false),
                    generated_at = table.Column<DateTime>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_approved_qrs", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_approved_qrs_application_id",
                schema: "document",
                table: "approved_qrs",
                column: "application_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_approved_qrs_submission_id",
                schema: "document",
                table: "approved_qrs",
                column: "submission_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "approved_qrs",
                schema: "document");
        }
    }
}
