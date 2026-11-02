using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using WardMate.Services.AnalyticsSystem.Domain;

namespace WardMate.Services.AnalyticsSystem.Infrastructure;

public sealed class AnalyticsDbContext(DbContextOptions<AnalyticsDbContext> options) : DbContext(options)
{
    public DbSet<ProcedureMetric> Procedures => Set<ProcedureMetric>();
    public DbSet<OfficerMetric> Officers => Set<OfficerMetric>();
    public DbSet<MetricRefresh> Refreshes => Set<MetricRefresh>();
    public DbSet<CitizenFeedback> Feedback => Set<CitizenFeedback>();
    public DbSet<SystemSetting> SystemSettings => Set<SystemSetting>();
    public DbSet<SystemAuditLog> SystemAudits => Set<SystemAuditLog>();
    public DbSet<BusinessAuditEvent> BusinessAudits => Set<BusinessAuditEvent>();
    protected override void OnModelCreating(ModelBuilder b)
    {
        b.HasDefaultSchema("analytics");
        var events = b.Entity<BusinessAuditEvent>();
        events.ToTable("business_audit_events"); events.HasKey(x=>x.Id);
        events.Property(x=>x.Source).HasMaxLength(40);
        events.Property(x=>x.Entity).HasMaxLength(100);
        events.Property(x=>x.Action).HasMaxLength(10);
        events.Property(x=>x.TraceId).HasMaxLength(128);
        events.Property(x=>x.PayloadHash).HasMaxLength(64);
        events.Property(x=>x.Payload).HasColumnType("jsonb");
        events.HasIndex(x=>new { x.OccurredAtUtc,x.Id });
        events.HasIndex(x=>new { x.Source,x.Entity,x.OccurredAtUtc });
        events.HasIndex(x=>new { x.ActorId,x.OccurredAtUtc });
        events.HasIndex(x=>x.TraceId);
        var setting = b.Entity<SystemSetting>();
        setting.ToTable("system_settings", t => t.HasCheckConstraint("ck_system_setting_value", "\"Value\" BETWEEN 1 AND 366"));
        setting.HasKey(x => x.Key);
        setting.Property(x => x.Key).HasMaxLength(100);
        setting.Property(x => x.Version).IsConcurrencyToken();
        var audit = b.Entity<SystemAuditLog>();
        audit.ToTable("audit_logs");
        audit.HasKey(x => x.Id);
        audit.HasIndex(x => new { x.OccurredAtUtc, x.Id });
        audit.Property(x => x.Action).HasMaxLength(100);
        audit.Property(x => x.EntityKey).HasMaxLength(100);
        audit.Property(x => x.Reason).HasMaxLength(500);
        audit.Property(x => x.ClientIp).HasMaxLength(64);
        audit.Property(x => x.TraceId).HasMaxLength(128);
        var p = b.Entity<ProcedureMetric>();
        p.ToTable("application_procedure_metrics");
        p.HasKey(x => new { x.MetricDate, x.WardCode, x.ProcedureId });
        p.Property(x => x.WardCode).HasMaxLength(50);
        p.HasIndex(x => new { x.WardCode, x.MetricDate });
        var o = b.Entity<OfficerMetric>();
        o.ToTable("daily_performance_metrics");
        o.HasKey(x => new { x.MetricDate, x.WardCode, x.OfficerId, x.ProcedureId });
        o.Property(x => x.WardCode).HasMaxLength(50);
        o.HasIndex(x => new { x.WardCode, x.MetricDate });
        b.Entity<MetricRefresh>().ToTable("metric_refreshes");
        b.Entity<MetricRefresh>().HasKey(x => x.MetricDate);
        var f = b.Entity<CitizenFeedback>();
        f.ToTable("citizen_feedback", t => t.HasCheckConstraint("ck_feedback_rating", "\"Rating\" BETWEEN 1 AND 5"));
        f.HasKey(x => x.Id);
        f.HasIndex(x => x.ApplicationId).IsUnique();
        f.HasIndex(x => new { x.WardCode, x.CreatedAtUtc });
        f.HasIndex(x => new { x.CitizenId, x.ApplicationId });
        f.Property(x => x.WardCode).HasMaxLength(50);
        f.Property(x => x.Comment).HasMaxLength(2000);
    }
}
public sealed class AnalyticsDbContextFactory : IDesignTimeDbContextFactory<AnalyticsDbContext>
{
    public AnalyticsDbContext CreateDbContext(string[] args) => new(new DbContextOptionsBuilder<AnalyticsDbContext>()
        .UseNpgsql(Environment.GetEnvironmentVariable("ConnectionStrings__AnalyticsDatabase") ??
            "Host=localhost;Port=5436;Database=wardmate_notifications_db;Username=wardmate_notifications",
            x => x.MigrationsHistoryTable("__AnalyticsMigrationsHistory", "analytics")).Options);
}
