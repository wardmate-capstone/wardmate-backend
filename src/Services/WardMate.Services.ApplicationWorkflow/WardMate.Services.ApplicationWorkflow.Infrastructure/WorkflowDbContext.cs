using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using WardMate.Services.ApplicationWorkflow.Domain;

namespace WardMate.Services.ApplicationWorkflow.Infrastructure;

public sealed class WorkflowDbContext(DbContextOptions<WorkflowDbContext> options) : DbContext(options)
{
    public DbSet<WorkflowOutbox> Outbox => Set<WorkflowOutbox>();
    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        WorkflowOutboxCapture.Capture(this);
        return base.SaveChangesAsync(cancellationToken);
    }
    public DbSet<ApplicationRecord> Applications => Set<ApplicationRecord>();
    public DbSet<ApplicationChecklist> Checklists => Set<ApplicationChecklist>();
    public DbSet<ApplicationStatusHistory> StatusHistory => Set<ApplicationStatusHistory>();
    public DbSet<ApplicationVersion> Versions => Set<ApplicationVersion>();
    public DbSet<ApplicationComment> Comments => Set<ApplicationComment>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        var o = b.Entity<WorkflowOutbox>();
        o.ToTable("workflow_outbox"); o.HasKey(x => x.Id);
        o.Property(x => x.Payload).HasColumnType("jsonb"); o.Property(x => x.LastError).HasMaxLength(100);
        o.HasIndex(x => new { x.PublishedAt, x.NextAttemptAt });
        b.HasSequence<long>("application_code_sequence");
        var a = b.Entity<ApplicationRecord>();
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
        a.Property(x => x.ProcedureTitle).HasMaxLength(500).IsRequired();
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
            t.HasCheckConstraint("ck_application_comments_status", "status IN ('OPEN','RESOLVED')");
            t.HasCheckConstraint("ck_application_comments_target", "target_type IN ('FORM_FIELD','CHECKLIST_ITEM')");
        });
        comment.HasKey(x => x.Id);
        comment.Property(x => x.TargetType).HasMaxLength(30).IsRequired();
        comment.Property(x => x.TargetId).HasMaxLength(100).IsRequired();
        comment.Property(x => x.FieldLabel).HasMaxLength(255).IsRequired();
        comment.Property(x => x.CommentText).HasColumnType("text").IsRequired();
        comment.Property(x => x.Status).HasMaxLength(20).HasDefaultValue("OPEN");
        comment.HasIndex(x => new { x.ApplicationId, x.ApplicationVersionId, x.Status });
        comment.HasOne<ApplicationVersion>().WithMany().HasForeignKey(x => new { x.ApplicationId, x.ApplicationVersionId })
            .HasPrincipalKey(x => new { x.ApplicationId, x.Id }).OnDelete(DeleteBehavior.Restrict);

        var c = b.Entity<ApplicationChecklist>();
        c.ToTable("application_checklists", t => t.HasCheckConstraint("ck_checklists_status", "status IN ('PENDING','COMPLETED','REJECTED')"));
        c.HasKey(x => x.Id);
        c.HasIndex(x => new { x.ApplicationId, x.Code }).IsUnique();
        c.Property(x => x.Code).HasMaxLength(255).IsRequired();
        c.Property(x => x.Title).HasMaxLength(255).IsRequired();
        c.Property(x => x.Status).HasMaxLength(30).HasDefaultValue(ChecklistStates.Pending);
        c.Property(x => x.FileUrl).HasMaxLength(500);
        c.Property(x => x.Note).HasMaxLength(4000);
        c.Property(x => x.CaseCode).HasColumnType("text");
        c.Property(x => x.SubmissionType).HasMaxLength(30);
        c.Property(x => x.DocumentCopyType).HasMaxLength(30);
        c.Property(x => x.Quantity).HasDefaultValue(1);
        c.Property(x => x.ConditionNote).HasColumnType("text");

        var h = b.Entity<ApplicationStatusHistory>();
        h.ToTable("application_status_history");
        h.HasKey(x => x.Id);
        h.HasIndex(x => new { x.ApplicationId, x.CreatedAt });
        h.Property(x => x.FromStatus).HasMaxLength(50);
        h.Property(x => x.ToStatus).HasMaxLength(50).IsRequired();
        h.Property(x => x.Reason).HasColumnType("text");
        h.Property(x => x.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
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

