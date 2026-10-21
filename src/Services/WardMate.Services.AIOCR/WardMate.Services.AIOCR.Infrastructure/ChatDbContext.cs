using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using WardMate.Services.AIOCR.Domain;

namespace WardMate.Services.AIOCR.Infrastructure;

public sealed class ChatDbContext(DbContextOptions<ChatDbContext> options) : DbContext(options)
{
    public DbSet<ChatSession> Sessions => Set<ChatSession>();
    public DbSet<ChatMessage> Messages => Set<ChatMessage>();
    protected override void OnModelCreating(ModelBuilder b)
    {
        var s = b.Entity<ChatSession>();
        s.ToTable("chat_sessions"); s.HasKey(x => x.Id);
        s.Property(x => x.Id).HasDefaultValueSql("gen_random_uuid()");
        s.Property(x => x.WardCode).HasMaxLength(50).IsRequired();
        s.Property(x => x.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
        s.HasIndex(x => new { x.UserId, x.CreatedAt });
        s.HasMany(x => x.Messages).WithOne().HasForeignKey(x => x.SessionId).OnDelete(DeleteBehavior.Cascade);
        var m = b.Entity<ChatMessage>();
        m.ToTable("chat_messages", t => t.HasCheckConstraint("ck_chat_message_role", "role IN ('User','Assistant')"));
        m.HasKey(x => x.Id); m.Property(x => x.Id).HasDefaultValueSql("gen_random_uuid()");
        m.Property(x => x.Role).HasMaxLength(20).IsRequired();
        m.Property(x => x.Content).HasMaxLength(20000).IsRequired();
        m.Property(x => x.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
        m.HasIndex(x => new { x.SessionId, x.CreatedAt });
    }
}
public sealed class ChatDbContextFactory : IDesignTimeDbContextFactory<ChatDbContext>
{
    public ChatDbContext CreateDbContext(string[] args) => new(new DbContextOptionsBuilder<ChatDbContext>()
        .UseNpgsql("Host=localhost;Database=wardmate_aiocr_db;Username=wardmate_aiocr")
        .UseSnakeCaseNamingConvention().Options);
}
