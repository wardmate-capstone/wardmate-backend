using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WardMate.Services.AIOCR.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class KnowledgeBase : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "knowledge_documents",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    title = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    source_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    procedure_id = table.Column<Guid>(type: "uuid", nullable: true),
                    source_reference = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    source_version = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    content = table.Column<string>(type: "text", nullable: false),
                    content_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    chunk_count = table.Column<int>(type: "integer", nullable: false),
                    chunking_version = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    chunked_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    revision = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_knowledge_documents", x => x.id);
                    table.CheckConstraint("ck_knowledge_documents_procedure", "source_type <> 'PROCEDURE' OR procedure_id IS NOT NULL");
                    table.CheckConstraint("ck_knowledge_documents_source_type", "source_type IN ('PROCEDURE','MANUAL_TEXT','FAQ','PDF_FILE')");
                    table.CheckConstraint("ck_knowledge_documents_status", "status IN ('CHUNKED')");
                });

            migrationBuilder.CreateTable(
                name: "knowledge_chunks",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    document_id = table.Column<Guid>(type: "uuid", nullable: false),
                    chunk_index = table.Column<int>(type: "integer", nullable: false),
                    heading = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    content = table.Column<string>(type: "text", nullable: false),
                    char_count = table.Column<int>(type: "integer", nullable: false),
                    content_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_knowledge_chunks", x => x.id);
                    table.ForeignKey(
                        name: "fk_knowledge_chunks_knowledge_documents_document_id",
                        column: x => x.document_id,
                        principalTable: "knowledge_documents",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_knowledge_chunks_document_id_chunk_index",
                table: "knowledge_chunks",
                columns: new[] { "document_id", "chunk_index" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_knowledge_documents_procedure_id",
                table: "knowledge_documents",
                column: "procedure_id",
                unique: true,
                filter: "source_type = 'PROCEDURE'");

            migrationBuilder.CreateIndex(
                name: "ix_knowledge_documents_source_type_is_active",
                table: "knowledge_documents",
                columns: new[] { "source_type", "is_active" });

            migrationBuilder.CreateIndex(
                name: "ix_knowledge_documents_updated_at",
                table: "knowledge_documents",
                column: "updated_at");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Curated knowledge cannot be rebuilt from the database alone; refuse to drop it silently.
            migrationBuilder.Sql("""
                DO $rollback$
                BEGIN
                  IF EXISTS (SELECT 1 FROM knowledge_documents) THEN
                    RAISE EXCEPTION 'knowledge_documents has rows; export them before rolling back KnowledgeBase.';
                  END IF;
                END $rollback$;
                """);
            migrationBuilder.DropTable(
                name: "knowledge_chunks");

            migrationBuilder.DropTable(
                name: "knowledge_documents");
        }
    }
}
