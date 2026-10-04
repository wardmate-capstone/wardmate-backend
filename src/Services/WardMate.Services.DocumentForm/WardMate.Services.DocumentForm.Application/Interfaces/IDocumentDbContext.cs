using Microsoft.EntityFrameworkCore;
using WardMate.Services.DocumentForm.Domain.Entities;

namespace WardMate.Services.DocumentForm.Application.Interfaces;

/// <summary>
/// Abstraction interface cho Document & Form DbContext.
/// </summary>
public interface IDocumentDbContext
{
    DbSet<FormTemplate> FormTemplates { get; }
    DbSet<ApplicationForm> ApplicationForms { get; }
    DbSet<SupportingDocument> SupportingDocuments { get; }
    DbSet<GeneratedDocument> GeneratedDocuments { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
