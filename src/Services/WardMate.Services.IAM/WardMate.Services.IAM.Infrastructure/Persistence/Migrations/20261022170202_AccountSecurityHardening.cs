using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WardMate.Services.IAM.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AccountSecurityHardening : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_user_otps_purpose",
                schema: "iam",
                table: "user_otps");

            migrationBuilder.AddColumn<Guid>(
                name: "security_stamp",
                table: "users",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<string>(
                name: "verified_phone_number",
                table: "users",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "target_phone_number",
                schema: "iam",
                table: "user_otps",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_user_otps_purpose",
                schema: "iam",
                table: "user_otps",
                sql: "purpose IN ('CHANGE_PASSWORD','FORGOT_PASSWORD','VERIFY_PHONE')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_user_otps_purpose",
                schema: "iam",
                table: "user_otps");

            migrationBuilder.DropColumn(
                name: "security_stamp",
                table: "users");

            migrationBuilder.DropColumn(
                name: "verified_phone_number",
                table: "users");

            migrationBuilder.DropColumn(
                name: "target_phone_number",
                schema: "iam",
                table: "user_otps");

            migrationBuilder.AddCheckConstraint(
                name: "ck_user_otps_purpose",
                schema: "iam",
                table: "user_otps",
                sql: "purpose IN ('CHANGE_PASSWORD','FORGOT_PASSWORD')");
        }
    }
}
