using WardMate.Services.DocumentForm.Application.Models;
using WardMate.Services.DocumentForm.Domain.Models;
using WardMate.SharedKernel.Common;

namespace WardMate.Services.DocumentForm.Application.Interfaces;

/// <summary>
/// Engine quản lý Schema định nghĩa biểu mẫu động (E-Forms) và xác thực dữ liệu nhập liệu.
/// </summary>
public interface IFormSchemaEngine
{
    /// <summary>
    /// Kiểm tra tính hợp lệ về cấu trúc của chuỗi JSON schema_definition và parse thành FormSchemaDefinition object.
    /// </summary>
    Result<FormSchemaDefinition> ParseAndValidateSchema(string schemaDefinitionJson);

    /// <summary>
    /// Kiểm tra tính hợp lệ của dữ liệu người dùng gửi lên so với Schema biểu mẫu đã định nghĩa.
    /// </summary>
    Result<IReadOnlyList<FormDataValidationError>> ValidateFormData(FormSchemaDefinition schema, string formDataJson);
}
