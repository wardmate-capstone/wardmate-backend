using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using WardMate.Services.AnalyticsSystem.Domain;

namespace WardMate.Services.AnalyticsSystem.Infrastructure;

public sealed class NotificationDbContext(DbContextOptions<NotificationDbContext> options) : DbContext(options)
{
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<NotificationInbox> Inbox => Set<NotificationInbox>();
    public DbSet<NotificationDelivery> Deliveries => Set<NotificationDelivery>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        var inbox = builder.Entity<NotificationInbox>();
        inbox.ToTable("notification_inbox"); inbox.HasKey(x => x.Id);
        inbox.Property(x => x.Payload).HasColumnType("jsonb"); inbox.Property(x => x.State).HasMaxLength(30);
        inbox.Property(x => x.ErrorCode).HasMaxLength(100); inbox.HasIndex(x => new { x.State, x.NextAttemptAt });
        var delivery = builder.Entity<NotificationDelivery>();
        delivery.ToTable("notification_deliveries"); delivery.HasKey(x => x.Id);
        delivery.Property(x => x.Channel).HasMaxLength(30); delivery.Property(x => x.State).HasMaxLength(30);
        delivery.Property(x => x.ErrorCode).HasMaxLength(100);
        delivery.HasIndex(x => new { x.NotificationId, x.Channel }).IsUnique();
        delivery.HasIndex(x => new { x.State, x.NextAttemptAt });
        delivery.HasOne<Notification>().WithMany().HasForeignKey(x => x.NotificationId).OnDelete(DeleteBehavior.Cascade);
        var n = builder.Entity<Notification>();
        n.Property(x => x.SourceEventId).HasColumnName("source_event_id");
        n.Property(x => x.RecipientWardCode).HasColumnName("recipient_ward_code").HasMaxLength(50);
        n.HasIndex(x => new { x.SourceEventId, x.RecipientUserId, x.Channel }).IsUnique();
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
