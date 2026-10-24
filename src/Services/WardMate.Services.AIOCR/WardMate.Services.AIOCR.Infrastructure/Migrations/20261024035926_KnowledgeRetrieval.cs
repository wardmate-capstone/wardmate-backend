using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WardMate.Services.AIOCR.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class KnowledgeRetrieval : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_knowledge_documents_status",
                table: "knowledge_documents");

            migrationBuilder.AddColumn<DateTime>(
                name: "embedded_at",
                table: "knowledge_documents",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "embedding_attempts",
                table: "knowledge_documents",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "embedding_error",
                table: "knowledge_documents",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "embedding_model",
                table: "knowledge_documents",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "next_embedding_at",
                table: "knowledge_documents",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "embedded_at",
                table: "knowledge_chunks",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<float[]>(
                name: "embedding",
                table: "knowledge_chunks",
                type: "real[]",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "embedding_model",
                table: "knowledge_chunks",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "citations",
                table: "chat_messages",
                type: "jsonb",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_knowledge_documents_status_is_active",
                table: "knowledge_documents",
                columns: new[] { "status", "is_active" });

            migrationBuilder.AddCheckConstraint(
                name: "ck_knowledge_documents_status",
                table: "knowledge_documents",
                sql: "status IN ('CHUNKED','EMBEDDED','EMBEDDING_FAILED')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_knowledge_documents_status_is_active",
                table: "knowledge_documents");

            migrationBuilder.DropCheckConstraint(
                name: "ck_knowledge_documents_status",
                table: "knowledge_documents");

            migrationBuilder.DropColumn(
                name: "embedded_at",
                table: "knowledge_documents");

            migrationBuilder.DropColumn(
                name: "embedding_attempts",
                table: "knowledge_documents");

            migrationBuilder.DropColumn(
                name: "embedding_error",
                table: "knowledge_documents");

            migrationBuilder.DropColumn(
                name: "embedding_model",
                table: "knowledge_documents");

            migrationBuilder.DropColumn(
                name: "next_embedding_at",
                table: "knowledge_documents");

            migrationBuilder.DropColumn(
                name: "embedded_at",
                table: "knowledge_chunks");

            migrationBuilder.DropColumn(
                name: "embedding",
                table: "knowledge_chunks");

            migrationBuilder.DropColumn(
                name: "embedding_model",
                table: "knowledge_chunks");

            migrationBuilder.DropColumn(
                name: "citations",
                table: "chat_messages");

            // Embeddings are dropped above, so every document is back to "chunked, not embedded".
            migrationBuilder.Sql("UPDATE knowledge_documents SET status = 'CHUNKED' WHERE status <> 'CHUNKED';");
            migrationBuilder.AddCheckConstraint(
                name: "ck_knowledge_documents_status",
                table: "knowledge_documents",
                sql: "status IN ('CHUNKED')");
        }
    }
}
