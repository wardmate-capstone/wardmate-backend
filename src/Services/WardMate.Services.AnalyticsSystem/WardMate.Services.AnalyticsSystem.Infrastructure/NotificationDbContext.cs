using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using WardMate.Services.AnalyticsSystem.Domain;

namespace WardMate.Services.AnalyticsSystem.Infrastructure;

public sealed class NotificationDbContext(DbContextOptions<NotificationDbContext> options) : DbContext(options)
{
    public DbSet<Notification> Notifications => Set<Notification>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        var n = builder.Entity<Notification>();
        n.ToTable("notifications", table =>
        {
            table.HasCheckConstraint("ck_notifications_type", "type IN ('APPLICATION_STATUS_CHANGED','INLINE_COMMENT_ADDED','ASSIGNED_OFFICER','SYSTEM_ALERT')");
            table.HasCheckConstraint("ck_notifications_channel", "channel IN ('IN_APP','SMS','EMAIL')");
            table.HasCheckConstraint("ck_notifications_read", "(is_read AND read_at IS NOT NULL) OR (NOT is_read AND read_at IS NULL)");
        });
        n.HasKey(x => x.Id);
        n.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
        n.Property(x => x.RecipientUserId).HasColumnName("recipient_user_id").IsRequired();
        n.Property(x => x.ApplicationId).HasColumnName("application_id");
        n.Property(x => x.Title).HasColumnName("title").HasMaxLength(255).IsRequired();
        n.Property(x => x.Content).HasColumnName("content").HasColumnType("text").IsRequired();
        n.Property(x => x.Type).HasColumnName("type").HasMaxLength(50).IsRequired();
        n.Property(x => x.Channel).HasColumnName("channel").HasMaxLength(30).HasDefaultValue("IN_APP").IsRequired();
        n.Property(x => x.IsRead).HasColumnName("is_read").HasDefaultValue(false);
        n.Property(x => x.ReadAt).HasColumnName("read_at").HasColumnType("timestamp with time zone");
        n.Property(x => x.ActionUrl).HasColumnName("action_url").HasMaxLength(500);
        n.Property(x => x.CreatedAt).HasColumnName("created_at").HasColumnType("timestamp with time zone").HasDefaultValueSql("now()");
        n.HasIndex(x => new { x.RecipientUserId, x.IsRead });
        n.HasIndex(x => x.CreatedAt);
        n.HasIndex(x => new { x.RecipientUserId, x.CreatedAt, x.Id });
    }
}

public sealed class NotificationDbContextFactory : IDesignTimeDbContextFactory<NotificationDbContext>
{
    public NotificationDbContext CreateDbContext(string[] args) => new(new DbContextOptionsBuilder<NotificationDbContext>()
        .UseNpgsql(Environment.GetEnvironmentVariable("ConnectionStrings__NotificationsDatabase")
            ?? "Host=localhost;Port=5436;Database=wardmate_notifications_db;Username=wardmate_notifications")
        .Options);
}
