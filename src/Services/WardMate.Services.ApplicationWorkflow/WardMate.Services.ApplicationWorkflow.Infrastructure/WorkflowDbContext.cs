using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using WardMate.Services.ApplicationWorkflow.Domain;

namespace WardMate.Services.ApplicationWorkflow.Infrastructure;

public sealed class WorkflowDbContext(DbContextOptions<WorkflowDbContext> options) : DbContext(options)
{
    public DbSet<ApplicationRecord> Applications => Set<ApplicationRecord>();
    public DbSet<ApplicationChecklist> Checklists => Set<ApplicationChecklist>();
    public DbSet<ApplicationStatusHistory> StatusHistory => Set<ApplicationStatusHistory>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.HasSequence<long>("application_code_sequence");
        var a = b.Entity<ApplicationRecord>();
        a.ToTable("applications", t => t.HasCheckConstraint("ck_applications_status", "status IN ('DRAFT','SUBMITTED')"));
        a.HasKey(x => x.Id);
        a.Property(x => x.ApplicationCode).HasMaxLength(40);
        a.HasIndex(x => x.ApplicationCode).IsUnique();
        a.HasIndex(x => new { x.UserId, x.CreatedAt });
        a.Property(x => x.ProcedureTitle).HasMaxLength(500).IsRequired();
        a.Property(x => x.CaseCode).HasMaxLength(100);
        a.Property(x => x.Status).HasMaxLength(30).HasDefaultValue(ApplicationStates.Draft);
        a.Property(x => x.FormData).HasColumnType("jsonb").IsRequired();
        a.HasMany(x => x.Checklists).WithOne().HasForeignKey(x => x.ApplicationId).OnDelete(DeleteBehavior.Cascade);
        a.HasMany(x => x.History).WithOne().HasForeignKey(x => x.ApplicationId).OnDelete(DeleteBehavior.Cascade);

        var c = b.Entity<ApplicationChecklist>();
        c.ToTable("application_checklists", t => t.HasCheckConstraint("ck_checklists_status", "status IN ('PENDING','COMPLETED','REJECTED')"));
        c.HasKey(x => x.Id);
        c.HasIndex(x => new { x.ApplicationId, x.Code }).IsUnique();
        c.Property(x => x.Code).HasMaxLength(255).IsRequired();
        c.Property(x => x.Title).HasMaxLength(255).IsRequired();
        c.Property(x => x.Status).HasMaxLength(30).HasDefaultValue(ChecklistStates.Pending);
        c.Property(x => x.FileUrl).HasMaxLength(500);
        c.Property(x => x.Note).HasMaxLength(4000);

        var h = b.Entity<ApplicationStatusHistory>();
        h.ToTable("application_status_history");
        h.HasKey(x => x.Id);
        h.HasIndex(x => new { x.ApplicationId, x.CreatedAt });
        h.Property(x => x.FromStatus).HasMaxLength(30);
        h.Property(x => x.ToStatus).HasMaxLength(30).IsRequired();
        h.Property(x => x.Reason).HasColumnType("text");
        h.Property(x => x.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
    }
}
public sealed class WorkflowDbContextFactory : IDesignTimeDbContextFactory<WorkflowDbContext>
{
    public WorkflowDbContext CreateDbContext(string[] args) => new(new DbContextOptionsBuilder<WorkflowDbContext>()
        .UseNpgsql(Environment.GetEnvironmentVariable("ConnectionStrings__WorkflowDatabase")
            ?? "Host=localhost;Port=5435;Database=wardmate_workflow_db;Username=wardmate_workflow")
        .UseSnakeCaseNamingConvention().Options);
}
