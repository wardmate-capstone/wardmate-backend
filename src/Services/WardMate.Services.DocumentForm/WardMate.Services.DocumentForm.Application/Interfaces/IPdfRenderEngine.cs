namespace WardMate.Services.DocumentForm.Application.Interfaces;

/// <summary>
/// Abstraction cho engine chuyển đổi văn bản DOCX sang PDF giấy tờ chuẩn bị.
/// </summary>
public interface IPdfRenderEngine
{
    Task<Stream> RenderDocxToPdfAsync(
        Stream docxStream,
        string fileName = "document.docx",
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gộp nhiều file PDF thành một file PDF duy nhất theo thứ tự danh sách truyền vào.
    /// </summary>
    Task<Stream> MergePdfsAsync(
        IReadOnlyList<(Stream Stream, string FileName)> pdfStreams,
        CancellationToken cancellationToken = default);
}
