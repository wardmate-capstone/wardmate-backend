using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WardMate.Services.IAM.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class WardScopedAccountManagement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ward_id",
                table: "users",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "wards",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_wards", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_users_ward_id",
                table: "users",
                column: "ward_id");

            migrationBuilder.CreateIndex(
                name: "ix_wards_code",
                table: "wards",
                column: "code",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "fk_users_wards_ward_id",
                table: "users",
                column: "ward_id",
                principalTable: "wards",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_users_wards_ward_id",
                table: "users");

            migrationBuilder.DropTable(
                name: "wards");

            migrationBuilder.DropIndex(
                name: "ix_users_ward_id",
                table: "users");

            migrationBuilder.DropColumn(
                name: "ward_id",
                table: "users");
        }
    }
}
