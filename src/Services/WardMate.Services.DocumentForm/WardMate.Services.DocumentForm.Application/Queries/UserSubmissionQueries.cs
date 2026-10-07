using Microsoft.EntityFrameworkCore;
using WardMate.Services.DocumentForm.Application.DTOs;
using WardMate.Services.DocumentForm.Application.Interfaces;
using WardMate.Services.DocumentForm.Domain.Errors;
using WardMate.Services.DocumentForm.Domain.Models;
using WardMate.SharedKernel.Blob;
using WardMate.SharedKernel.Common;
using WardMate.SharedKernel.CQRS;

namespace WardMate.Services.DocumentForm.Application.Queries;

// ─────────────────────────────────────────────────────────────────────────────
// GetMySubmissions — Lấy danh sách hồ sơ của người dùng
// GET /user-submissions?applicantId={id}&status={status}
// ─────────────────────────────────────────────────────────────────────────────

public sealed record GetMySubmissionsQuery(
    Guid ApplicantId,
    string? Status = null,
    int Page = 1,
    int PageSize = 20) : IQuery<PagedResult<UserSubmissionSummaryDto>>;

public sealed class GetMySubmissionsQueryHandler
    : IQueryHandler<GetMySubmissionsQuery, PagedResult<UserSubmissionSummaryDto>>
{
    private readonly IDocumentDbContext _dbContext;

    public GetMySubmissionsQueryHandler(IDocumentDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Result<PagedResult<UserSubmissionSummaryDto>>> Handle(
        GetMySubmissionsQuery request, CancellationToken cancellationToken)
    {
        if (request.ApplicantId == Guid.Empty || request.Page < 1 || request.PageSize < 1 || request.PageSize > 100)
            return Error.Validation("document.invalid_query", "applicantId is required; page >= 1 and pageSize between 1 and 100.");
        var query = _dbContext.UserSubmissions
            .AsNoTracking()
            .Where(s => s.ApplicantId == request.ApplicantId
                        && !s.IsDeleted);

        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            if (!Enum.TryParse<SubmissionStatus>(request.Status, ignoreCase: true, out var status)
                || !Enum.IsDefined(status))
                return Error.Validation("document.invalid_submission_status", "Unknown submission status.");

            query = query.Where(s => s.Status == status);
        }

        var total = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(s => s.UpdatedAtUtc ?? s.CreatedAtUtc)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(s => new UserSubmissionSummaryDto
            {
                Id = s.Id,
                TemplateId = s.TemplateId,
                ApplicantId = s.ApplicantId,
                FileName = s.FileName,
                FileSizeBytes = s.FileSizeBytes,
                Status = s.Status.ToString(),
                OfficerComment = s.OfficerComment,
                SubmittedAt = s.SubmittedAt,
                ReviewedAt = s.ReviewedAt,
                CreatedAt = s.CreatedAtUtc,
                UpdatedAt = s.UpdatedAtUtc
            })
            .ToListAsync(cancellationToken);

        return PagedResult<UserSubmissionSummaryDto>.Create(items, total, request.Page, request.PageSize);
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// GetSubmissionById — Lấy chi tiết một hồ sơ
// GET /user-submissions/{id}
// ─────────────────────────────────────────────────────────────────────────────

public sealed record GetSubmissionByIdQuery(Guid SubmissionId, Guid ApplicantId) : IQuery<UserSubmissionSummaryDto>;

public sealed class GetSubmissionByIdQueryHandler
    : IQueryHandler<GetSubmissionByIdQuery, UserSubmissionSummaryDto>
{
    private readonly IDocumentDbContext _dbContext;

    public GetSubmissionByIdQueryHandler(IDocumentDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Result<UserSubmissionSummaryDto>> Handle(
        GetSubmissionByIdQuery request, CancellationToken cancellationToken)
    {
        var submission = await _dbContext.UserSubmissions
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == request.SubmissionId && !s.IsDeleted, cancellationToken);

        if (submission is null)
            return DocumentFormErrors.SubmissionNotFound(request.SubmissionId);

        if (request.ApplicantId == Guid.Empty || submission.ApplicantId != request.ApplicantId)
            return DocumentFormErrors.SubmissionNotOwnedByApplicant(submission.Id, request.ApplicantId);
        var version = await _dbContext.FormTemplateVersions.AsNoTracking()
            .FirstOrDefaultAsync(v => v.Id == submission.TemplateVersionId, cancellationToken);
        return new UserSubmissionSummaryDto
        {
            Id = submission.Id,
            TemplateVersionId = submission.TemplateVersionId,
            FormData = submission.FormData is null ? null : System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(submission.FormData),
            SchemaDefinition = version is null ? null : System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(version.SchemaDefinition),
            TemplateId = submission.TemplateId,
            ApplicantId = submission.ApplicantId,
            FileName = submission.FileName,
            FileSizeBytes = submission.FileSizeBytes,
            Status = submission.Status.ToString(),
            OfficerComment = submission.OfficerComment,
            SubmittedAt = submission.SubmittedAt,
            ReviewedAt = submission.ReviewedAt,
            CreatedAt = submission.CreatedAtUtc,
            UpdatedAt = submission.UpdatedAtUtc
        };
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// DownloadSubmissionDocx — Tải file DOCX của hồ sơ (người dùng điền tiếp / cán bộ xem)
// GET /user-submissions/{id}/download-docx
// ─────────────────────────────────────────────────────────────────────────────

public sealed record DownloadSubmissionDocxQuery(Guid SubmissionId, Guid ApplicantId) : IQuery<DownloadDocxResult>;

public sealed class DownloadSubmissionDocxQueryHandler
    : IQueryHandler<DownloadSubmissionDocxQuery, DownloadDocxResult>
{
    private readonly IDocumentDbContext _dbContext;
    private readonly IBlobStorageClient _blobClient;

    public DownloadSubmissionDocxQueryHandler(IDocumentDbContext dbContext, IBlobStorageClient blobClient)
    {
        _dbContext = dbContext;
        _blobClient = blobClient;
    }

    public async Task<Result<DownloadDocxResult>> Handle(
        DownloadSubmissionDocxQuery request, CancellationToken cancellationToken)
    {
        var submission = await _dbContext.UserSubmissions
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == request.SubmissionId && !s.IsDeleted, cancellationToken);

        if (submission is null)
            return DocumentFormErrors.SubmissionNotFound(request.SubmissionId);

        if (request.ApplicantId == Guid.Empty || submission.ApplicantId != request.ApplicantId)
            return DocumentFormErrors.SubmissionNotOwnedByApplicant(submission.Id, request.ApplicantId);
        if (string.IsNullOrWhiteSpace(submission.BlobUrl))
            return DocumentFormErrors.SubmissionFileNotFound(request.SubmissionId);

        var blobName = ExtractBlobName(submission.BlobUrl);
        var stream = await _blobClient.DownloadAsync(blobName, ct: cancellationToken);

        if (stream is null)
            return DocumentFormErrors.SubmissionFileNotFound(request.SubmissionId);

        return new DownloadDocxResult
        {
            FileStream = stream,
            FileName = submission.FileName,
            ContentType = "application/vnd.openxmlformats-officedocument.wordprocessingml.document"
        };
    }

    private static string ExtractBlobName(string blobUrl)
    {
        var uri = new Uri(blobUrl);
        var path = uri.AbsolutePath.TrimStart('/');
        if (path.StartsWith("devstoreaccount1/", StringComparison.OrdinalIgnoreCase))
        {
            path = path["devstoreaccount1/".Length..];
        }
        var firstSlashIndex = path.IndexOf('/');
        return firstSlashIndex >= 0 ? path[(firstSlashIndex + 1)..] : path;
    }
}

