using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WardMate.Services.AIOCR.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ChatQualityFeedback : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "feedback_at",
                table: "chat_messages",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "feedback_comment",
                table: "chat_messages",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "feedback_helpful",
                table: "chat_messages",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "feedback_reason",
                table: "chat_messages",
                type: "character varying(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "needs_clarification",
                table: "chat_messages",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "suggested_questions",
                table: "chat_messages",
                type: "jsonb",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_chat_messages_feedback_helpful_feedback_at",
                table: "chat_messages",
                columns: new[] { "feedback_helpful", "feedback_at" },
                filter: "feedback_helpful IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_chat_messages_feedback_helpful_feedback_at",
                table: "chat_messages");

            migrationBuilder.DropColumn(
                name: "feedback_at",
                table: "chat_messages");

            migrationBuilder.DropColumn(
                name: "feedback_comment",
                table: "chat_messages");

            migrationBuilder.DropColumn(
                name: "feedback_helpful",
                table: "chat_messages");

            migrationBuilder.DropColumn(
                name: "feedback_reason",
                table: "chat_messages");

            migrationBuilder.DropColumn(
                name: "needs_clarification",
                table: "chat_messages");

            migrationBuilder.DropColumn(
                name: "suggested_questions",
                table: "chat_messages");
        }
    }
}
