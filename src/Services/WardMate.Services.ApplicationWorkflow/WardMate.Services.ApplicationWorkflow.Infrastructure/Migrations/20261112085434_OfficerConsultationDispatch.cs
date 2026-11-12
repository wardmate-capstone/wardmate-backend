using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WardMate.Services.ApplicationWorkflow.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class OfficerConsultationDispatch : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_consultation_status",
                table: "application_consultations");

            migrationBuilder.AlterColumn<Guid>(
                name: "specialist_id",
                table: "application_consultations",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<string>(
                name: "decision",
                table: "application_consultations",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "dispatched_at_utc",
                table: "application_consultations",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "dispatched_by",
                table: "application_consultations",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "willing_to_take_over",
                table: "application_consultations",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "ix_application_consultations_application_id_assignment_version",
                table: "application_consultations",
                columns: new[] { "application_id", "assignment_version" },
                unique: true,
                filter: "status = 'PENDING_DISPATCH'");

            migrationBuilder.AddCheckConstraint(
                name: "ck_consultation_status",
                table: "application_consultations",
                sql: "status IN ('PENDING_DISPATCH','OPEN','ANSWERED','CLOSED')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_application_consultations_application_id_assignment_version",
                table: "application_consultations");

            migrationBuilder.DropCheckConstraint(
                name: "ck_consultation_status",
                table: "application_consultations");

            migrationBuilder.DropColumn(
                name: "decision",
                table: "application_consultations");

            migrationBuilder.DropColumn(
                name: "dispatched_at_utc",
                table: "application_consultations");

            migrationBuilder.DropColumn(
                name: "dispatched_by",
                table: "application_consultations");

            migrationBuilder.DropColumn(
                name: "willing_to_take_over",
                table: "application_consultations");

            migrationBuilder.AlterColumn<Guid>(
                name: "specialist_id",
                table: "application_consultations",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_consultation_status",
                table: "application_consultations",
                sql: "status IN ('OPEN','ANSWERED','CLOSED')");
        }
    }
}
