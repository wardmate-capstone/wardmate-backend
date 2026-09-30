using Microsoft.EntityFrameworkCore;
using WardMate.Services.DocumentForm.Application.Interfaces;
using WardMate.Services.DocumentForm.Domain.Entities;

namespace WardMate.Services.DocumentForm.Infrastructure.Persistence;

public sealed class DocumentDbContext : DbContext, IDocumentDbContext
{
    public DocumentDbContext(DbContextOptions<DocumentDbContext> options)
        : base(options)
    {
    }

    public DbSet<FormTemplate> FormTemplates => Set<FormTemplate>();
    public DbSet<FormTemplateVersion> FormTemplateVersions => Set<FormTemplateVersion>();
    public DbSet<ApplicationForm> ApplicationForms => Set<ApplicationForm>();
    public DbSet<SupportingDocument> SupportingDocuments => Set<SupportingDocument>();
    public DbSet<GeneratedDocument> GeneratedDocuments => Set<GeneratedDocument>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("document");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(DocumentDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
