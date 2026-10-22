namespace WardMate.Services.AnalyticsSystem.Application;
public sealed record DeadlineFact(Guid Id, string Status, DateTime? FirstSubmittedAtUtc, DateTime? DueAtUtc,
    DateTime? DocumentsReadyAtUtc, string DeadlineStatus, DateTime? PausedAtUtc, Guid ProcedureId = default, Guid? AssignedOfficerId = null);
public sealed record DeadlineFacts(int ContractVersion, DateTime AsOfUtc, DeadlineFact[] Items);
public sealed record DeadlineStatistics(long CompletedOnTime, long CompletedLate, long CompletedMissingDeadline,
    long CompletedNotApplicable, long CompletedSampleCount, long OpenOverdue, long OpenMissingDeadline,
    long OpenNotApplicable, long OpenPaused, DateTime AsOfUtc)
{
    public double? OnTimeRate => CompletedSampleCount == 0 ? null : 100.0 * CompletedOnTime / CompletedSampleCount;
    public double? LateRate => CompletedSampleCount == 0 ? null : 100.0 * CompletedLate / CompletedSampleCount;
}
public static class DeadlineAggregation
{
    public static DeadlineStatistics Subset(DeadlineFacts source, IEnumerable<DeadlineFact> rows) =>
        Aggregate(source with { Items = rows.ToArray() });
    public static void ValidateDimensions(DeadlineFacts source)
    {
        _ = Aggregate(source);
        if (source.Items.Any(x => x.ProcedureId == Guid.Empty || x.AssignedOfficerId == Guid.Empty))
            throw new AnalyticsException(503, "analytics.deadline_dimensions_missing", "Cần cập nhật Workflow để cung cấp thủ tục/cán bộ cho deadline.");
    }

    public static DeadlineStatistics Aggregate(DeadlineFacts source)
    {
        if (source.ContractVersion != 1 || source.AsOfUtc.Kind != DateTimeKind.Utc || source.Items is null
            || source.Items.Length > 100000 || source.Items.Any(x => x is null) || source.Items.Select(x => x.Id).Distinct().Count() != source.Items.Length)
            throw new AnalyticsException(503, "analytics.deadline_invalid", "Nguồn deadline không hợp lệ.");
        foreach (var row in source.Items)
        {
            if (row.Id == Guid.Empty || row.DeadlineStatus is not ("KNOWN" or "UNKNOWN" or "NOT_APPLICABLE")
                || new[] { row.FirstSubmittedAtUtc, row.DueAtUtc, row.DocumentsReadyAtUtc, row.PausedAtUtc }.Any(x => x.HasValue && x.Value.Kind != DateTimeKind.Utc)
                || row.DeadlineStatus == "KNOWN" && (row.DueAtUtc is null || row.FirstSubmittedAtUtc is null)
                || row.DeadlineStatus != "KNOWN" && (row.DueAtUtc is not null || row.PausedAtUtc is not null)
                || row.DueAtUtc < row.FirstSubmittedAtUtc || row.DocumentsReadyAtUtc < row.FirstSubmittedAtUtc || row.PausedAtUtc > source.AsOfUtc
                || row.DocumentsReadyAtUtc is not null && row.PausedAtUtc is not null
                || row.DocumentsReadyAtUtc is null && row.Status is not ("SUBMITTED" or "UNDER_REVIEW" or "NEED_REVISION"))
                throw new AnalyticsException(503, "analytics.deadline_invalid", "Nguồn deadline không nhất quán.");
        }
        var completed = source.Items.Where(x => x.DocumentsReadyAtUtc is not null).ToArray();
        var open = source.Items.Where(x => x.DocumentsReadyAtUtc is null).ToArray();
        var onTime = completed.Count(x => x.DeadlineStatus == "KNOWN" && x.DocumentsReadyAtUtc <= x.DueAtUtc);
        var late = completed.Count(x => x.DeadlineStatus == "KNOWN" && x.DocumentsReadyAtUtc > x.DueAtUtc);
        return new(onTime, late, completed.Count(x => x.DeadlineStatus == "UNKNOWN"), completed.Count(x => x.DeadlineStatus == "NOT_APPLICABLE"),
            onTime + late, open.Count(x => x.DeadlineStatus == "KNOWN" && x.DueAtUtc < (x.PausedAtUtc ?? source.AsOfUtc)),
            open.Count(x => x.DeadlineStatus == "UNKNOWN"), open.Count(x => x.DeadlineStatus == "NOT_APPLICABLE"),
            open.Count(x => x.PausedAtUtc is not null), source.AsOfUtc);
    }
}
