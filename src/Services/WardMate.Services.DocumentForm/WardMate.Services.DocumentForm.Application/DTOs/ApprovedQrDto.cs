namespace WardMate.Services.DocumentForm.Application.DTOs;

public sealed record ApprovedQrDto
{
    public Guid Id { get; init; }
    public Guid ApplicationId { get; init; }
    public Guid SubmissionId { get; init; }
    public string QrCodeValue { get; init; } = string.Empty;
    public DateTime GeneratedAt { get; init; }
}
