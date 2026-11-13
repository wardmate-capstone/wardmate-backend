using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using WardMate.Services.ApplicationWorkflow.Domain;

namespace WardMate.Services.ApplicationWorkflow.Infrastructure;

public sealed class WorkflowDbContext(DbContextOptions<WorkflowDbContext> options) : DbContext(options)
{
    public DbSet<CalendarClosure> CalendarClosures => Set<CalendarClosure>();
    public DbSet<WorkflowOutbox> Outbox => Set<WorkflowOutbox>();
    // Staffing tables (local feature)
    public DbSet<OfficerShift> Shifts => Set<OfficerShift>();
    public DbSet<OfficerLeave> Leaves => Set<OfficerLeave>();
    public DbSet<LeaveDraft> LeaveDrafts => Set<LeaveDraft>();
    public DbSet<LeaveDocumentRevision> LeaveRevisions => Set<LeaveDocumentRevision>();
    public DbSet<ScheduleRevision> ScheduleRevisions => Set<ScheduleRevision>();
    public DbSet<WeekPattern> WeekPatterns => Set<WeekPattern>();
    public DbSet<OfficerScheduleReceipt> ScheduleReceipts => Set<OfficerScheduleReceipt>();
    public DbSet<ShiftChangeRequest> ShiftChanges => Set<ShiftChangeRequest>();
    public DbSet<OfficerLeaveDecision> LeaveDecisions => Set<OfficerLeaveDecision>();
    public DbSet<ApplicationConsultation> Consultations => Set<ApplicationConsultation>();
    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        WorkflowOutboxCapture.Capture(this);
        return base.SaveChangesAsync(cancellationToken);
    }
    public DbSet<ApplicationRecord> Applications => Set<ApplicationRecord>();
    public DbSet<ApplicationAssignmentHistory> AssignmentHistory => Set<ApplicationAssignmentHistory>();
    public DbSet<ApplicationChecklist> Checklists => Set<ApplicationChecklist>();
    public DbSet<ApplicationStatusHistory> StatusHistory => Set<ApplicationStatusHistory>();
    public DbSet<ApplicationVersion> Versions => Set<ApplicationVersion>();
    public DbSet<ApplicationComment> Comments => Set<ApplicationComment>();

    /// <summary>Hàm PostgreSQL <c>unaccent</c>; chỉ dùng trong truy vấn LINQ, không gọi trực tiếp.</summary>
    public static string Unaccent(string value) => throw new NotSupportedException("Chỉ dùng trong truy vấn EF Core.");

    protected override void OnModelCreating(ModelBuilder b)
    {
        var draft = b.Entity<LeaveDraft>();
        var log=b.Entity<LeaveDraftAudit>();
        log.ToTable("officer_leave_draft_audit");
        log.HasIndex(x=>new {x.DraftId,x.Version}).IsUnique();
        log.HasOne<LeaveDraft>().WithMany().HasForeignKey(x=>x.DraftId).OnDelete(DeleteBehavior.Restrict);
        var schedule = b.Entity<ScheduleRevision>();
        schedule.ToTable("ward_schedule_revisions");
        schedule.HasKey(x=>x.Id);
        schedule.HasIndex(x=>new { x.WardCode,x.Month }).IsUnique().HasFilter("status = 'DRAFT'");
        schedule.Property(x=>x.SlotsJson).HasColumnType("jsonb");
        var week=b.Entity<WeekPattern>();
        week.ToTable("ward_week_patterns");
        week.HasKey(x=>x.WardCode);
        week.Property(x=>x.EntriesJson).HasColumnType("jsonb");
        var receipt=b.Entity<OfficerScheduleReceipt>();
        receipt.ToTable("officer_schedule_receipts");
        receipt.HasIndex(x=>new { x.WardCode,x.Month,x.OfficerId,x.Fingerprint }).IsUnique();
        var change=b.Entity<ShiftChangeRequest>();
        change.ToTable("officer_shift_change_requests");
        change.HasOne<OfficerShift>().WithMany().HasForeignKey(x=>x.ShiftId).OnDelete(DeleteBehavior.Restrict);
        change.HasIndex(x=>x.ShiftId).IsUnique().HasFilter("status = 'PENDING'");
        draft.ToTable("officer_leave_drafts");
        draft.HasKey(x => x.Id);
        draft.Property(x => x.Reason).HasMaxLength(2000);
        draft.Property(x => x.Version).IsConcurrencyToken();
        draft.HasIndex(x => new { x.OfficerId, x.CreationKey }).IsUnique();
        draft.HasIndex(x => new { x.OfficerId, x.SubmissionKey }).IsUnique();
        draft.HasIndex(x => new { x.WardCode, x.OfficerId, x.UpdatedAtUtc });
        draft.HasIndex(x => x.SubmittedLeaveId).IsUnique();
        draft.HasOne<OfficerLeave>().WithMany().HasForeignKey(x => x.SubmittedLeaveId).OnDelete(DeleteBehavior.Restrict);
        var rev = b.Entity<LeaveDocumentRevision>();
        rev.ToTable("officer_leave_document_revisions");
        rev.HasKey(x => x.Id);
        rev.HasAlternateKey(x => new { x.DraftId, x.Id });
        rev.HasOne<LeaveDraft>().WithMany().HasForeignKey(x => x.DraftId).OnDelete(DeleteBehavior.Restrict);
        draft.HasOne<LeaveDocumentRevision>().WithMany().HasForeignKey(x => new { x.Id, x.CurrentRevisionId })
            .HasPrincipalKey(x => new { x.DraftId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        var o = b.Entity<WorkflowOutbox>();
        o.ToTable("workflow_outbox"); o.HasKey(x => x.Id);
        o.Property(x => x.Payload).HasColumnType("jsonb"); o.Property(x => x.LastError).HasMaxLength(100);
        o.HasIndex(x => new { x.PublishedAt, x.NextAttemptAt });
        b.HasSequence<long>("application_code_sequence");
        var assignment = b.Entity<ApplicationAssignmentHistory>();
        assignment.ToTable("application_assignment_history");
        assignment.HasKey(x => x.Id);
        assignment.Property(x => x.Action).HasMaxLength(20);
        assignment.Property(x => x.Reason).HasMaxLength(500);
        assignment.Property(x => x.FromOfficerName).HasMaxLength(255);
        assignment.Property(x => x.ToOfficerName).HasMaxLength(255);
        assignment.Property(x => x.ActorName).HasMaxLength(255);
        assignment.HasIndex(x => new { x.ApplicationId, x.CreatedAt });
        assignment.HasIndex(x => new { x.ActorId, x.CreatedAt });
        // DISPATCH-SEARCH-01: tìm không dấu trong danh sách điều phối; index phục vụ lọc phường + sắp theo thời gian tạo.
        b.HasPostgresExtension("unaccent");
        b.HasDbFunction(typeof(WorkflowDbContext).GetMethod(nameof(Unaccent))!).HasName("unaccent");
        var a = b.Entity<ApplicationRecord>();
        a.HasIndex(x => new { x.WardCode, x.CreatedAt, x.Id }).HasDatabaseName("ix_applications_ward_code_created_at_id");
        a.Property(x => x.CategoryName).HasMaxLength(255);
        a.Property(x => x.AssignedOfficerName).HasMaxLength(255);
        a.Property(x => x.ApplicantName).HasMaxLength(255);
        a.Property(x => x.AssignmentVersion).HasDefaultValue(0L);
        a.HasIndex(x => new { x.WardCode, x.CategoryId, x.Status });
        a.HasMany(x => x.AssignmentHistory).WithOne().HasForeignKey(x => x.ApplicationId).OnDelete(DeleteBehavior.Cascade);
        a.ToTable("applications", t => t.HasCheckConstraint("ck_applications_status", "status IN ('DRAFT','SUBMITTED','UNDER_REVIEW','NEED_REVISION','DOCUMENTS_READY','OFFICIALLY_RECEIVED','APPROVED','CANCELLED','REJECTED')"));
        a.HasKey(x => x.Id);
        a.Property(x => x.DeadlineStatus).HasMaxLength(20).HasDefaultValue("UNKNOWN");
        a.Property(x => x.DeadlinePolicyCode).HasMaxLength(100);
        a.Property(x => x.DeadlinePolicyVersion).HasMaxLength(100);
        a.Property(x => x.DeadlineCalendarCode).HasMaxLength(100);
        a.Property(x => x.DeadlineCalendarVersion).HasMaxLength(100);
        a.Property(x => x.DeadlinePolicySnapshot).HasColumnType("jsonb");
        a.Property(x => x.DeadlineHistory).HasColumnType("jsonb").HasDefaultValue("[]");
        a.HasIndex(x => new { x.WardCode, x.DeadlineStatus, x.DueAtUtc });
        a.ToTable(t => t.HasCheckConstraint("ck_applications_deadline", "(deadline_status = 'KNOWN' AND due_at_utc IS NOT NULL AND first_submitted_at_utc IS NOT NULL AND deadline_policy_snapshot IS NOT NULL AND deadline_policy_code IS NOT NULL AND deadline_policy_version IS NOT NULL AND deadline_calendar_code IS NOT NULL AND deadline_calendar_version IS NOT NULL) OR (deadline_status IN ('UNKNOWN','NOT_APPLICABLE') AND due_at_utc IS NULL AND deadline_paused_at_utc IS NULL)"));
        a.Property(x => x.ReceiptNumber).HasMaxLength(64);
        a.HasIndex(x => x.ReceiptNumber).IsUnique();
        a.Property(x => x.WardCode).HasMaxLength(50);
        a.HasIndex(x => new { x.WardCode, x.Status, x.SubmittedAt });
        a.Property(x => x.ApplicationCode).HasMaxLength(50);
        a.HasIndex(x => x.ApplicationCode).IsUnique();
        a.HasIndex(x => new { x.UserId, x.CreatedAt });
        a.Property(x => x.ProcedureTitle).HasMaxLength(1000).IsRequired();
        a.Property(x => x.CaseCode).HasMaxLength(100);
        a.Property(x => x.Status).HasMaxLength(50).HasDefaultValue(ApplicationStates.Draft);
        a.Property(x => x.ResubmitCount).HasDefaultValue(0);
        a.Property(x => x.Notes).HasColumnType("text");
        a.HasIndex(x => new { x.Status, x.SubmittedAt, x.Id });
        a.HasIndex(x => x.AssignedOfficerId);
        a.Property(x => x.FormData).HasColumnType("jsonb").IsRequired();
        a.HasMany(x => x.Checklists).WithOne().HasForeignKey(x => x.ApplicationId).OnDelete(DeleteBehavior.Cascade);
        a.HasMany(x => x.History).WithOne().HasForeignKey(x => x.ApplicationId).OnDelete(DeleteBehavior.Cascade);
        a.HasMany(x => x.Versions).WithOne().HasForeignKey(x => x.ApplicationId).OnDelete(DeleteBehavior.Cascade);
        a.HasMany(x => x.Comments).WithOne().HasForeignKey(x => x.ApplicationId).OnDelete(DeleteBehavior.Cascade);

        var v = b.Entity<ApplicationVersion>();
        v.ToTable("application_versions", t => t.HasCheckConstraint("ck_application_versions_number", "version_number > 0"));
        v.HasKey(x => x.Id);
        v.HasAlternateKey(x => new { x.ApplicationId, x.Id });
        v.HasIndex(x => new { x.ApplicationId, x.VersionNumber }).IsUnique();
        v.Property(x => x.SnapshotData).HasColumnType("jsonb").IsRequired();

        var comment = b.Entity<ApplicationComment>();
        comment.ToTable("application_comments", t =>
        {
            t.HasCheckConstraint("ck_application_comments_status", "status IN ('OPEN','RESOLVED','WITHDRAWN')");
            t.HasCheckConstraint("ck_application_comments_withdrawal", "(status = 'WITHDRAWN' AND withdrawn_by IS NOT NULL AND withdrawn_at IS NOT NULL AND withdraw_reason IS NOT NULL) OR (status <> 'WITHDRAWN' AND withdrawn_by IS NULL AND withdrawn_at IS NULL AND withdraw_reason IS NULL)");
            t.HasCheckConstraint("ck_application_comments_target", "target_type IN ('FORM_FIELD','CHECKLIST_ITEM')");
        });
        comment.HasKey(x => x.Id);
        comment.Property(x => x.TargetType).HasMaxLength(30).IsRequired();
        comment.Property(x => x.TargetId).HasMaxLength(100).IsRequired();
        // BE-H: nhãn nhận xét giấy tờ thường là tên giấy tờ, có thể dài (Catalog cho tối đa 10.000 ký tự).
        comment.Property(x => x.FieldLabel).HasColumnType("text").IsRequired();
        comment.Property(x => x.CommentText).HasColumnType("text").IsRequired();
        comment.Property(x => x.Status).HasMaxLength(20).HasDefaultValue("OPEN");
        comment.Property(x => x.WithdrawReason).HasMaxLength(500);
        comment.HasIndex(x => new { x.ApplicationId, x.ApplicationVersionId, x.Status });
        comment.HasOne<ApplicationVersion>().WithMany().HasForeignKey(x => new { x.ApplicationId, x.ApplicationVersionId })
            .HasPrincipalKey(x => new { x.ApplicationId, x.Id }).OnDelete(DeleteBehavior.Restrict);

        var c = b.Entity<ApplicationChecklist>();
        c.ToTable("application_checklists", t => t.HasCheckConstraint("ck_checklists_status", "status IN ('PENDING','COMPLETED','REJECTED')"));
        c.HasKey(x => x.Id);
        c.HasIndex(x => new { x.ApplicationId, x.Code }).IsUnique();
        c.Property(x => x.Code).HasMaxLength(255).IsRequired();
        // BE-H (A.1): tên giấy tờ không còn giới hạn 255.
        c.Property(x => x.Title).HasColumnType("text").IsRequired();
        c.Property(x => x.Status).HasMaxLength(30).HasDefaultValue(ChecklistStates.Pending);
        c.Property(x => x.FileUrl).HasMaxLength(500);
        c.Property(x => x.Note).HasMaxLength(4000);
        c.Property(x => x.CaseCode).HasColumnType("text");
        c.Property(x => x.SubmissionType).HasMaxLength(30);
        c.Property(x => x.DocumentCopyType).HasMaxLength(30);
        c.Property(x => x.Quantity).HasDefaultValue(1);
        c.Property(x => x.ConditionNote).HasColumnType("text");
        c.Property(x => x.QuantityText).HasColumnType("text");
        c.Property(x => x.ItemNote).HasColumnType("text");
        c.Property(x => x.FormLinks).HasColumnType("jsonb");

        var h = b.Entity<ApplicationStatusHistory>();
        h.ToTable("application_status_history");
        h.HasKey(x => x.Id);
        h.HasIndex(x => new { x.ApplicationId, x.CreatedAt });
        h.HasIndex(x => new { x.ChangedBy, x.CreatedAt });
        h.Property(x => x.FromStatus).HasMaxLength(50);
        h.Property(x => x.ToStatus).HasMaxLength(50).IsRequired();
        h.Property(x => x.Reason).HasColumnType("text");
        h.Property(x => x.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");

        var closure = b.Entity<CalendarClosure>();
        closure.ToTable("calendar_closures", t => {
            t.HasCheckConstraint("ck_closure_cancellation", "(cancelled_by IS NULL AND cancelled_at_utc IS NULL AND cancellation_reason IS NULL) OR (cancelled_by IS NOT NULL AND cancelled_at_utc IS NOT NULL AND cancellation_reason IS NOT NULL)");
            t.HasCheckConstraint("ck_closure_reason", "length(btrim(reason)) BETWEEN 1 AND 500");
        });
        closure.HasKey(x=>x.Id);
        closure.Property(x=>x.WardCode).HasMaxLength(50);
        closure.Property(x=>x.Reason).HasMaxLength(500).IsRequired();
        closure.Property(x=>x.CancellationReason).HasMaxLength(500);
        closure.Property(x=>x.WardName).HasMaxLength(255);
        closure.Property(x=>x.CreatedByName).HasMaxLength(255);
        closure.Property(x=>x.CancelledByName).HasMaxLength(255);
        closure.HasIndex(x=>new { x.WardCode, x.ClosureDate }).IsUnique().HasFilter("cancelled_at_utc IS NULL AND ward_code IS NOT NULL");
        closure.HasIndex(x=>x.ClosureDate).HasDatabaseName("ux_calendar_closures_national").IsUnique().HasFilter("cancelled_at_utc IS NULL AND ward_code IS NULL");
        closure.HasIndex(x=>new { x.ClosureDate, x.Id });
        // --- Staffing tables (added by local staffing feature) ---
        var shift = b.Entity<OfficerShift>();
        shift.ToTable("officer_shifts", t => t.HasCheckConstraint("ck_shift_range", "ends_at_utc > starts_at_utc"));
        shift.HasKey(x => x.Id);
        shift.Property(x => x.WardCode).HasMaxLength(50);
        shift.Property(x => x.CancellationReason).HasMaxLength(2000);
        shift.HasIndex(x => new { x.WardCode, x.OfficerId, x.StartsAtUtc });

        var lv = b.Entity<OfficerLeave>();
        lv.ToTable("officer_leaves", t => { t.HasCheckConstraint("ck_leave_range", "ends_at_utc > starts_at_utc"); t.HasCheckConstraint("ck_leave_status", "status IN ('PENDING','APPROVED','REJECTED','CANCELLED')"); });
        lv.HasKey(x => x.Id);
        lv.Property(x => x.WardCode).HasMaxLength(50);
        lv.Property(x => x.Reason).HasMaxLength(2000);
        lv.Property(x => x.DecisionReason).HasMaxLength(2000);
        lv.Property(x => x.Status).HasMaxLength(20);
        lv.HasIndex(x => new { x.WardCode, x.OfficerId, x.StartsAtUtc });

        var decision = b.Entity<OfficerLeaveDecision>();
        decision.ToTable("officer_leave_decisions");
        decision.HasKey(x => x.Id);
        decision.HasIndex(x => new { x.LeaveId, x.Version }).IsUnique();
        decision.HasOne<OfficerLeave>().WithMany().HasForeignKey(x => x.LeaveId).OnDelete(DeleteBehavior.Restrict);
        decision.Property(x => x.FromStatus).HasMaxLength(20);
        decision.Property(x => x.ToStatus).HasMaxLength(20);
        decision.Property(x => x.Reason).HasMaxLength(2000);

        var consultation = b.Entity<ApplicationConsultation>();
        consultation.ToTable("application_consultations", t => t.HasCheckConstraint("ck_consultation_status", "status IN ('PENDING_DISPATCH','OPEN','ANSWERED','CLOSED')"));
        consultation.HasIndex(x => new { x.ApplicationId, x.AssignmentVersion }).IsUnique().HasFilter("status = 'PENDING_DISPATCH'");
        consultation.HasKey(x => x.Id);
        consultation.Property(x => x.Status).HasMaxLength(20);
        consultation.Property(x => x.Question).HasMaxLength(4000);
        consultation.Property(x => x.Answer).HasMaxLength(4000);
        consultation.Property(x => x.CloseReason).HasMaxLength(4000);
        consultation.Property(x => x.ApplicationCode).HasMaxLength(50);
        consultation.Property(x => x.SpecialistName).HasMaxLength(255);
        consultation.Property(x => x.RequestedByName).HasMaxLength(255);
        consultation.Property(x => x.ClosedByName).HasMaxLength(255);
        consultation.HasOne<ApplicationRecord>().WithMany().HasForeignKey(x => x.ApplicationId).OnDelete(DeleteBehavior.Restrict);
        consultation.HasIndex(x => new { x.ApplicationId, x.SpecialistId }).IsUnique().HasFilter("status <> 'CLOSED'");
    }
}
public sealed class WorkflowDbContextFactory : IDesignTimeDbContextFactory<WorkflowDbContext>
{
    public WorkflowDbContext CreateDbContext(string[] args) => new(new DbContextOptionsBuilder<WorkflowDbContext>()
        .UseNpgsql(WorkflowInfrastructureRegistration.NormalizeConnectionString(
            Environment.GetEnvironmentVariable("ConnectionStrings__WorkflowDatabase")
            ?? "Host=localhost;Port=5435;Database=wardmate_workflow_db;Username=wardmate_workflow"))
        .UseSnakeCaseNamingConvention().Options);
}

