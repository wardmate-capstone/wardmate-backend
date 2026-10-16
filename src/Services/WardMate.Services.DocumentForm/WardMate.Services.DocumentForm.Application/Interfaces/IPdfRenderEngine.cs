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
}
