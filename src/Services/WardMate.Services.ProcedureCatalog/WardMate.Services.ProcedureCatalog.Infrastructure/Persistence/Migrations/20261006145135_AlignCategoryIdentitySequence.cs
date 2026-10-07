using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WardMate.Services.ProcedureCatalog.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AlignCategoryIdentitySequence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Seed rows use explicit positive IDs. Never rewind an existing sequence on an upgraded database.
            migrationBuilder.Sql("""
                LOCK TABLE procedure_categories IN EXCLUSIVE MODE;
                SELECT setval(pg_get_serial_sequence('procedure_categories', 'id'),
                    GREATEST(COALESCE((SELECT MAX(id) FROM procedure_categories), 1),
                        COALESCE(pg_sequence_last_value(pg_get_serial_sequence('procedure_categories', 'id')::regclass), 1)), true);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Rewinding would risk collisions with categories already created by the API.
        }
    }
}
