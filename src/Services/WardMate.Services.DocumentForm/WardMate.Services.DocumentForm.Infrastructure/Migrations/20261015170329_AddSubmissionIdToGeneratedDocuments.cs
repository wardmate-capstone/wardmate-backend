using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WardMate.Services.DocumentForm.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSubmissionIdToGeneratedDocuments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "submission_id",
                schema: "document",
                table: "generated_documents",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_generated_documents_submission_id",
                schema: "document",
                table: "generated_documents",
                column: "submission_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_generated_documents_submission_id",
                schema: "document",
                table: "generated_documents");

            migrationBuilder.DropColumn(
                name: "submission_id",
                schema: "document",
                table: "generated_documents");
        }
    }
}
