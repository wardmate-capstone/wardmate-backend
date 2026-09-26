using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WardMate.Services.IAM.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RbacAdministration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Existing seeded IDs must not collide with newly created business roles.
            migrationBuilder.Sql("SELECT setval(pg_get_serial_sequence('roles', 'id'), GREATEST(COALESCE((SELECT MAX(id) FROM roles), 0) + 1, nextval(pg_get_serial_sequence('roles', 'id'))), false)");
            migrationBuilder.CreateTable(
                name: "rbac_audit_logs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    actor_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    action = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    target_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    role_id = table.Column<int>(type: "integer", nullable: true),
                    permission_id = table.Column<int>(type: "integer", nullable: true),
                    details = table.Column<string>(type: "jsonb", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_rbac_audit_logs", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_rbac_audit_logs_created_at_id",
                table: "rbac_audit_logs",
                columns: new[] { "created_at", "id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "rbac_audit_logs");
        }
    }
}
