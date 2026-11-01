using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using WardMate.Services.AIOCR.Domain;

namespace WardMate.Services.AIOCR.Infrastructure;

public sealed class ChatDbContext(DbContextOptions<ChatDbContext> options) : DbContext(options)
{
    public DbSet<ChatSession> Sessions => Set<ChatSession>();
    public DbSet<ChatMessage> Messages => Set<ChatMessage>();
    public DbSet<KnowledgeDocument> KnowledgeDocuments => Set<KnowledgeDocument>();
    public DbSet<KnowledgeChunk> KnowledgeChunks => Set<KnowledgeChunk>();
    public DbSet<AiRequestLog> AiRequestLogs => Set<AiRequestLog>();
    protected override void OnModelCreating(ModelBuilder b)
    {
        var l = b.Entity<AiRequestLog>();
        l.ToTable("ai_request_logs", t =>
        {
            t.HasCheckConstraint("ck_ai_request_logs_outcome", "outcome IN ('SUCCESS','FAILED','REJECTED')");
            t.HasCheckConstraint("ck_ai_request_logs_counts", "duration_ms >= 0 AND item_count >= 1");
        });
        l.HasKey(x => x.Id);
        l.Property(x => x.Operation).HasMaxLength(40).IsRequired();
        l.Property(x => x.Model).HasMaxLength(120).IsRequired();
        l.Property(x => x.Outcome).HasMaxLength(20).IsRequired();
        l.Property(x => x.ErrorCode).HasMaxLength(100);
        l.HasIndex(x => x.CreatedAt);
        l.HasIndex(x => new { x.UserId, x.CreatedAt });

        var d = b.Entity<KnowledgeDocument>();
        d.ToTable("knowledge_documents", t =>
        {
            t.HasCheckConstraint("ck_knowledge_documents_source_type", "source_type IN ('PROCEDURE','MANUAL_TEXT','FAQ','PDF_FILE')");
            t.HasCheckConstraint("ck_knowledge_documents_status", "status IN ('CHUNKED','EMBEDDED','EMBEDDING_FAILED')");
            t.HasCheckConstraint("ck_knowledge_documents_procedure", "source_type <> 'PROCEDURE' OR procedure_id IS NOT NULL");
        });
        d.HasKey(x => x.Id);
        d.Property(x => x.Title).HasMaxLength(500).IsRequired();
        d.Property(x => x.SourceType).HasMaxLength(20).IsRequired();
        d.Property(x => x.SourceReference).HasMaxLength(500);
        d.Property(x => x.SourceVersion).HasMaxLength(100);
        d.Property(x => x.Content).HasColumnType("text").IsRequired();
        d.Property(x => x.ContentSha256).HasMaxLength(64).IsRequired();
        d.Property(x => x.Status).HasMaxLength(20).IsRequired();
        d.Property(x => x.ChunkingVersion).HasMaxLength(50).IsRequired();
        d.Property(x => x.EmbeddingModel).HasMaxLength(120);
        d.Property(x => x.EmbeddingError).HasMaxLength(100);
        d.HasIndex(x => new { x.Status, x.IsActive });
        // One knowledge document per catalog procedure; manual/FAQ/PDF documents may reference a procedure freely.
        d.HasIndex(x => x.ProcedureId).IsUnique().HasFilter("source_type = 'PROCEDURE'");
        d.HasIndex(x => new { x.SourceType, x.IsActive });
        d.HasIndex(x => x.UpdatedAt);
        d.HasMany(x => x.Chunks).WithOne().HasForeignKey(x => x.DocumentId).OnDelete(DeleteBehavior.Cascade);
        var k = b.Entity<KnowledgeChunk>();
        k.ToTable("knowledge_chunks"); k.HasKey(x => x.Id);
        k.Property(x => x.Heading).HasMaxLength(500);
        k.Property(x => x.Content).HasColumnType("text").IsRequired();
        k.Property(x => x.ContentSha256).HasMaxLength(64).IsRequired();
        // Plain real[] instead of pgvector: similarity is computed in memory (KnowledgeRetriever).
        k.Property(x => x.Embedding).HasColumnType("real[]");
        k.Property(x => x.EmbeddingModel).HasMaxLength(120);
        k.HasIndex(x => new { x.DocumentId, x.ChunkIndex }).IsUnique();

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
        m.Property(x => x.Citations).HasColumnType("jsonb");
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
