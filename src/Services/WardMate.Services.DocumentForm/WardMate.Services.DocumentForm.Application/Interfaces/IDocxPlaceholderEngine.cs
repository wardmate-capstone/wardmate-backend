using WardMate.Services.DocumentForm.Application.Models;
using WardMate.Services.DocumentForm.Domain.Models;
using WardMate.SharedKernel.Common;

namespace WardMate.Services.DocumentForm.Application.Interfaces;

/// <summary>
/// Engine phân tích và bóc tách các biến giữ chỗ (placeholders ví dụ {{ho_va_ten}}) từ file Word (.docx),
/// và tự động đối chiếu / ánh xạ với Schema của biểu mẫu điện tử.
/// </summary>
public interface IDocxPlaceholderEngine
{
    /// <summary>
    /// Bóc tách tất cả placeholders từ luồng dữ liệu file Word DOCX.
    /// </summary>
    Result<IReadOnlyList<DocxPlaceholder>> ExtractPlaceholders(Stream docxStream);

    /// <summary>
    /// Bóc tách tất cả placeholders từ mảng byte file Word DOCX.
    /// </summary>
    Result<IReadOnlyList<DocxPlaceholder>> ExtractPlaceholders(byte[] docxBytes);

    /// <summary>
    /// Đối chiếu danh sách placeholder tìm thấy trong Word với cấu hình schema để phát hiện trường thiếu/thừa.
    /// </summary>
    PlaceholderValidationResult MatchPlaceholdersWithSchema(IReadOnlyList<DocxPlaceholder> placeholders, FormSchemaDefinition schema);

    /// <summary>
    /// Tự động sinh cấu trúc Schema dự thảo từ danh sách placeholders được bóc tách từ file Word.
    /// </summary>
    FormSchemaDefinition GenerateDraftSchema(IReadOnlyList<DocxPlaceholder> placeholders, string templateTitle);
}
