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
    protected override void OnModelCreating(ModelBuilder b)
    {
        b.HasDefaultSchema("analytics");
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
