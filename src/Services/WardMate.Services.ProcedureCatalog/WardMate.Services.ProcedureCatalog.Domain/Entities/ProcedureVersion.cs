using System.Text.Json;

namespace WardMate.Services.ProcedureCatalog.Domain.Entities;

public sealed class ProcedureVersion
{
    public Guid Id { get; set; }
    public Guid ProcedureId { get; set; }
    public int VersionNumber { get; set; }
    public string? DecisionNumber { get; set; }
    public DateOnly EffectiveDate { get; set; }
    public string SnapshotData { get; set; } = "{}";
    public DateTime CreatedAt { get; set; }
    public Procedure Procedure { get; set; } = null!;

    // Freeze values immediately; exclude navigations to avoid cycles/version history recursion.
    public static ProcedureVersion Capture(Procedure procedure, int versionNumber, DateOnly effectiveDate,
        string? decisionNumber, DateTime createdAt)
    {
        ArgumentNullException.ThrowIfNull(procedure);
        if (procedure.Id == Guid.Empty) throw new ArgumentException("Thủ tục phải có mã định danh trước khi tạo phiên bản.", nameof(procedure));
        if (versionNumber < 1) throw new ArgumentOutOfRangeException(nameof(versionNumber), "Số phiên bản phải lớn hơn 0.");
        return new()
        {
            ProcedureId = procedure.Id, VersionNumber = versionNumber, EffectiveDate = effectiveDate,
            DecisionNumber = decisionNumber, CreatedAt = createdAt,
            SnapshotData = JsonSerializer.Serialize(new
            {
                procedure.Id, procedure.CategoryId, procedure.ProcedureCode, procedure.Title,
                procedure.IssuingAuthority, procedure.ExecutingAgency, procedure.LevelOfImplementation,
                procedure.TargetAudience, procedure.FeeSummary, procedure.ProcessingTimeSummary,
                procedure.IsActive, procedure.ContentPayload, procedure.ChecklistSchema, procedure.FormDefinitions,
                procedure.CreatedAt, procedure.UpdatedAt
            }, new JsonSerializerOptions(JsonSerializerDefaults.Web))
        };
    }
}
