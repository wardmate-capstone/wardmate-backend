using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WardMate.Services.IAM.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddUserOtpsTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "iam");

            migrationBuilder.CreateTable(
                name: "user_otps",
                schema: "iam",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    otp_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    channel = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    purpose = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    expires_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    attempts = table.Column<int>(type: "integer", nullable: false),
                    is_used = table.Column<bool>(type: "boolean", nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_user_otps", x => x.id);
                    table.CheckConstraint("ck_user_otps_attempts", "attempts BETWEEN 0 AND 5");
                    table.CheckConstraint("ck_user_otps_channel", "channel IN ('EMAIL','SMS')");
                    table.CheckConstraint("ck_user_otps_purpose", "purpose IN ('CHANGE_PASSWORD','FORGOT_PASSWORD')");
                    table.ForeignKey(
                        name: "fk_user_otps_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "welcome_emails",
                schema: "iam",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    attempts = table.Column<int>(type: "integer", nullable: false),
                    next_attempt_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    sent_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_welcome_emails", x => x.id);
                    table.ForeignKey(
                        name: "fk_welcome_emails_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_user_otps_user_id_created_at_utc",
                schema: "iam",
                table: "user_otps",
                columns: new[] { "user_id", "created_at_utc" });

            migrationBuilder.CreateIndex(
                name: "ix_welcome_emails_sent_at_utc_next_attempt_at_utc",
                schema: "iam",
                table: "welcome_emails",
                columns: new[] { "sent_at_utc", "next_attempt_at_utc" });

            migrationBuilder.CreateIndex(
                name: "ix_welcome_emails_user_id",
                schema: "iam",
                table: "welcome_emails",
                column: "user_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "user_otps",
                schema: "iam");

            migrationBuilder.DropTable(
                name: "welcome_emails",
                schema: "iam");
        }
    }
}
