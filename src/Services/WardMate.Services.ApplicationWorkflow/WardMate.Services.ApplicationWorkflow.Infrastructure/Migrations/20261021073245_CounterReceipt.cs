using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WardMate.Services.ApplicationWorkflow.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CounterReceipt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_applications_status",
                table: "applications");

            migrationBuilder.AddColumn<DateTime>(
                name: "appointment_date",
                table: "applications",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "receipt_number",
                table: "applications",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "received_at",
                table: "applications",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_applications_receipt_number",
                table: "applications",
                column: "receipt_number",
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_applications_status",
                table: "applications",
                sql: "status IN ('DRAFT','SUBMITTED','UNDER_REVIEW','NEED_REVISION','DOCUMENTS_READY','OFFICIALLY_RECEIVED','APPROVED','CANCELLED','REJECTED')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DO $$ BEGIN IF EXISTS (SELECT 1 FROM applications WHERE status = 'OFFICIALLY_RECEIVED' OR receipt_number IS NOT NULL OR received_at IS NOT NULL OR appointment_date IS NOT NULL) THEN RAISE EXCEPTION 'CounterReceipt rollback requires preserving receipt data first.'; END IF; END $$;");
            migrationBuilder.DropIndex(
                name: "ix_applications_receipt_number",
                table: "applications");

            migrationBuilder.DropCheckConstraint(
                name: "ck_applications_status",
                table: "applications");

            migrationBuilder.DropColumn(
                name: "appointment_date",
                table: "applications");

            migrationBuilder.DropColumn(
                name: "receipt_number",
                table: "applications");

            migrationBuilder.DropColumn(
                name: "received_at",
                table: "applications");

            migrationBuilder.AddCheckConstraint(
                name: "ck_applications_status",
                table: "applications",
                sql: "status IN ('DRAFT','SUBMITTED','UNDER_REVIEW','NEED_REVISION','DOCUMENTS_READY','APPROVED','CANCELLED','REJECTED')");
        }
    }
}

