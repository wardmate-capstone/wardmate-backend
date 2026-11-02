using Microsoft.EntityFrameworkCore.Migrations;

namespace WardMate.Auditing;

// Migration inputs are literal reviewed identifiers, never HTTP input.
public static class AuditSourceMigration
{
    public static void Install(MigrationBuilder migration, string source, params (string Schema, string Table, string Keys)[] tables)
    {
        using var stream = typeof(AuditSourceMigration).Assembly.GetManifestResourceStream("WardMate.Auditing.SourceAudit.sql")!;
        using var reader = new StreamReader(stream);
        migration.Sql(reader.ReadToEnd());
        foreach (var (schema, table, keys) in tables)
        {
            Validate(schema); Validate(table); Validate(source);
            foreach (var key in keys.Split(',')) Validate(key);
            migration.Sql($"CREATE TRIGGER wardmate_audit_capture AFTER INSERT OR UPDATE OR DELETE ON \"{schema}\".\"{table}\" FOR EACH ROW EXECUTE FUNCTION audit_transport.capture('{source}', '{keys}');");
            migration.Sql($"CREATE TRIGGER wardmate_audit_no_truncate BEFORE TRUNCATE ON \"{schema}\".\"{table}\" FOR EACH STATEMENT EXECUTE FUNCTION audit_transport.reject_truncate();");
        }
    }
    public static void Remove(MigrationBuilder migration, params (string Schema, string Table)[] tables)
    {
        foreach (var (schema, table) in tables)
        {
            Validate(schema); Validate(table);
            migration.Sql($"DROP TRIGGER IF EXISTS wardmate_audit_capture ON \"{schema}\".\"{table}\";");
            migration.Sql($"DROP TRIGGER IF EXISTS wardmate_audit_no_truncate ON \"{schema}\".\"{table}\";");
        }
        // Keep committed evidence and shared function/outbox: another DbContext may own tables in this DB.
    }
    private static void Validate(string value)
    {
        if (value.Length == 0 || value.Any(c => !char.IsAsciiLetterOrDigit(c) && c != '_'))
            throw new ArgumentException("Invalid static audit identifier.");
    }
}
