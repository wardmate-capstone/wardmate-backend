using WardMate.Services.DocumentForm.Application.Services;
using WardMate.Services.DocumentForm.Domain.Models;

namespace WardMate.Services.DocumentForm.Tests;

/// <summary>
/// Tests cho TASK-11: FormSchemaEngine – parse schema JSONB, validate cấu trúc, validate dữ liệu nhập từ E-Form.
/// </summary>
public sealed class FormSchemaEngineTests
{
    private readonly FormSchemaEngine _engine = new();

    // ─── Helper: tạo JSON schema hợp lệ tối thiểu ─────────────────────────────

    private static string MinimalSchema(
        string title = "Biểu mẫu thử nghiệm",
        string fieldId = "ho_ten",
        string fieldType = "text") =>
        $$"""
        {
          "title": "{{title}}",
          "version": 1,
          "sections": [
            {
              "section_id": "sec_1",
              "title": "Thông tin cơ bản",
              "order": 1,
              "fields": [
                {
                  "field_id": "{{fieldId}}",
                  "label": "Họ và tên",
                  "type": "{{fieldType}}",
                  "is_required": true,
                  "order": 1
                }
              ]
            }
          ]
        }
        """;

    // ══════════════════════════════════════════════════════════════
    // ParseAndValidateSchema – trường hợp thành công
    // ══════════════════════════════════════════════════════════════

    [Fact]
    public void ParseAndValidateSchema_ValidMinimalSchema_ReturnsSuccess()
    {
        var result = _engine.ParseAndValidateSchema(MinimalSchema());

        Assert.True(result.IsSuccess);
        Assert.Equal("Biểu mẫu thử nghiệm", result.Value!.Title);
        Assert.Single(result.Value.Sections);
        Assert.Single(result.Value.GetAllFields());
    }

    [Fact]
    public void ParseAndValidateSchema_MultipleFields_ParsesAll()
    {
        const string json = """
        {
          "title": "Mẫu phức tạp",
          "version": 2,
          "sections": [
            {
              "section_id": "sec_a",
              "title": "Phần A",
              "order": 1,
              "fields": [
                { "field_id": "ho_ten",  "label": "Họ tên",  "type": "text",   "is_required": true,  "order": 1 },
                { "field_id": "ngay_sinh", "label": "Ngày sinh", "type": "date", "is_required": false, "order": 2 },
                { "field_id": "email",   "label": "Email",   "type": "email",  "is_required": true,  "order": 3 }
              ]
            }
          ]
        }
        """;

        var result = _engine.ParseAndValidateSchema(json);

        Assert.True(result.IsSuccess);
        Assert.Equal(3, result.Value!.GetAllFields().Count());
        Assert.Equal(2, result.Value.Version);
    }

    [Fact]
    public void ParseAndValidateSchema_SelectFieldWithOptions_ParsesOptions()
    {
        const string json = """
        {
          "title": "Mẫu giới tính",
          "sections": [
            {
              "section_id": "sec_1",
              "title": "Thông tin",
              "fields": [
                {
                  "field_id": "gioi_tinh",
                  "label": "Giới tính",
                  "type": "select",
                  "is_required": true,
                  "order": 1,
                  "options": [
                    { "label": "Nam",  "value": "male"   },
                    { "label": "Nữ",   "value": "female" }
                  ]
                }
              ]
            }
          ]
        }
        """;

        var result = _engine.ParseAndValidateSchema(json);

        Assert.True(result.IsSuccess);
        var field = result.Value!.GetAllFields().First();
        Assert.Equal(FormFieldType.Select, field.Type);
        Assert.Equal(2, field.Options.Count);
    }

    // ══════════════════════════════════════════════════════════════
    // ParseAndValidateSchema – kiểm tra lỗi cấu trúc
    // ══════════════════════════════════════════════════════════════

    [Fact]
    public void ParseAndValidateSchema_EmptyJson_ReturnsFail()
    {
        var result = _engine.ParseAndValidateSchema("   ");
        Assert.False(result.IsSuccess);
        Assert.Contains("empty", result.Error!.Description, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ParseAndValidateSchema_MalformedJson_ReturnsFail()
    {
        var result = _engine.ParseAndValidateSchema("{ not valid json }");
        Assert.False(result.IsSuccess);
    }

    [Fact]
    public void ParseAndValidateSchema_MissingTitle_ReturnsFail()
    {
        const string json = """
        {
          "sections": [
            { "section_id": "sec_1", "title": "A", "fields": [] }
          ]
        }
        """;

        var result = _engine.ParseAndValidateSchema(json);
        Assert.False(result.IsSuccess);
        Assert.Contains("title", result.Error!.Description, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ParseAndValidateSchema_NoSections_ReturnsFail()
    {
        const string json = """{ "title": "Mẫu A", "sections": [] }""";
        var result = _engine.ParseAndValidateSchema(json);
        Assert.False(result.IsSuccess);
        Assert.Contains("section", result.Error!.Description, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ParseAndValidateSchema_DuplicateSectionId_ReturnsFail()
    {
        const string json = """
        {
          "title": "Mẫu trùng section",
          "sections": [
            { "section_id": "sec_1", "title": "Phần 1", "fields": [] },
            { "section_id": "sec_1", "title": "Phần 1 trùng", "fields": [] }
          ]
        }
        """;

        var result = _engine.ParseAndValidateSchema(json);
        Assert.False(result.IsSuccess);
        Assert.Contains("Duplicate section", result.Error!.Description, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ParseAndValidateSchema_DuplicateFieldId_ReturnsFail()
    {
        const string json = """
        {
          "title": "Mẫu trùng field",
          "sections": [
            {
              "section_id": "sec_1",
              "title": "Phần 1",
              "fields": [
                { "field_id": "ho_ten", "label": "Họ tên", "type": "text", "order": 1 },
                { "field_id": "ho_ten", "label": "Họ tên (dup)", "type": "text", "order": 2 }
              ]
            }
          ]
        }
        """;

        var result = _engine.ParseAndValidateSchema(json);
        Assert.False(result.IsSuccess);
        Assert.Contains("Duplicate field", result.Error!.Description, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ParseAndValidateSchema_InvalidFieldIdChars_ReturnsFail()
    {
        const string json = """
        {
          "title": "Mẫu ID sai",
          "sections": [
            {
              "section_id": "sec_1",
              "title": "Phần 1",
              "fields": [
                { "field_id": "ho-ten", "label": "Họ tên", "type": "text", "order": 1 }
              ]
            }
          ]
        }
        """;
        // Ký tự '-' không hợp lệ — chỉ cho phép [a-zA-Z0-9_]
        var result = _engine.ParseAndValidateSchema(json);
        Assert.False(result.IsSuccess);
        Assert.Contains("alphanumeric", result.Error!.Description, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ParseAndValidateSchema_SelectWithNoOptions_ReturnsFail()
    {
        const string json = """
        {
          "title": "Mẫu select rỗng",
          "sections": [
            {
              "section_id": "sec_1",
              "title": "Phần 1",
              "fields": [
                { "field_id": "tinh_trang", "label": "Tình trạng", "type": "select", "order": 1, "options": [] }
              ]
            }
          ]
        }
        """;

        var result = _engine.ParseAndValidateSchema(json);
        Assert.False(result.IsSuccess);
        Assert.Contains("option", result.Error!.Description, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ParseAndValidateSchema_InvalidRegexPattern_ReturnsFail()
    {
        const string json = """
        {
          "title": "Mẫu regex sai",
          "sections": [
            {
              "section_id": "sec_1",
              "title": "Phần 1",
              "fields": [
                {
                  "field_id": "ma_so",
                  "label": "Mã số",
                  "type": "text",
                  "order": 1,
                  "validation": { "regex_pattern": "[invalid((" }
                }
              ]
            }
          ]
        }
        """;

        var result = _engine.ParseAndValidateSchema(json);
        Assert.False(result.IsSuccess);
        Assert.Contains("expression", result.Error!.Description, StringComparison.OrdinalIgnoreCase);
    }

    // ══════════════════════════════════════════════════════════════
    // ValidateFormData – trường hợp thành công (không có lỗi)
    // ══════════════════════════════════════════════════════════════

    [Fact]
    public void ValidateFormData_ValidPayload_ReturnsEmptyErrorList()
    {
        var schemaResult = _engine.ParseAndValidateSchema(MinimalSchema());
        Assert.True(schemaResult.IsSuccess);

        const string formData = """{ "ho_ten": "Nguyễn Văn A" }""";
        var result = _engine.ValidateFormData(schemaResult.Value!, formData);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value!);
    }

    // ══════════════════════════════════════════════════════════════
    // ValidateFormData – lỗi trường bắt buộc
    // ══════════════════════════════════════════════════════════════

    [Fact]
    public void ValidateFormData_MissingRequiredField_ReturnsRequiredError()
    {
        var schemaResult = _engine.ParseAndValidateSchema(MinimalSchema());
        var result = _engine.ValidateFormData(schemaResult.Value!, "{}");

        Assert.True(result.IsSuccess);
        Assert.Single(result.Value!);
        Assert.Equal("field.required", result.Value![0].ErrorCode);
        Assert.Equal("ho_ten", result.Value![0].FieldId);
    }

    [Fact]
    public void ValidateFormData_WhitespaceRequiredField_ReturnsRequiredError()
    {
        var schemaResult = _engine.ParseAndValidateSchema(MinimalSchema());
        var result = _engine.ValidateFormData(schemaResult.Value!, """{ "ho_ten": "   " }""");

        Assert.True(result.IsSuccess);
        Assert.Single(result.Value!);
        Assert.Equal("field.required", result.Value![0].ErrorCode);
    }

    // ══════════════════════════════════════════════════════════════
    // ValidateFormData – kiểu Email
    // ══════════════════════════════════════════════════════════════

    [Fact]
    public void ValidateFormData_InvalidEmail_ReturnsEmailError()
    {
        var schemaResult = _engine.ParseAndValidateSchema(MinimalSchema(fieldId: "email", fieldType: "email"));
        var result = _engine.ValidateFormData(schemaResult.Value!, """{ "email": "not-an-email" }""");

        Assert.True(result.IsSuccess);
        Assert.Single(result.Value!);
        Assert.Equal("field.invalid_email", result.Value![0].ErrorCode);
    }

    [Fact]
    public void ValidateFormData_ValidEmail_ReturnsNoError()
    {
        var schemaResult = _engine.ParseAndValidateSchema(MinimalSchema(fieldId: "email", fieldType: "email"));
        var result = _engine.ValidateFormData(schemaResult.Value!, """{ "email": "user@hospital.vn" }""");

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value!);
    }

    // ══════════════════════════════════════════════════════════════
    // ValidateFormData – kiểu số điện thoại
    // ══════════════════════════════════════════════════════════════

    [Fact]
    public void ValidateFormData_InvalidPhone_ReturnsPhoneError()
    {
        var schemaResult = _engine.ParseAndValidateSchema(MinimalSchema(fieldId: "dien_thoai", fieldType: "phone_number"));
        var result = _engine.ValidateFormData(schemaResult.Value!, """{ "dien_thoai": "12345" }""");

        Assert.True(result.IsSuccess);
        Assert.Single(result.Value!);
        Assert.Equal("field.invalid_phone", result.Value![0].ErrorCode);
    }

    [Fact]
    public void ValidateFormData_ValidVietnamPhone_ReturnsNoError()
    {
        var schemaResult = _engine.ParseAndValidateSchema(MinimalSchema(fieldId: "dien_thoai", fieldType: "phone_number"));
        var result = _engine.ValidateFormData(schemaResult.Value!, """{ "dien_thoai": "0912345678" }""");

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value!);
    }

    // ══════════════════════════════════════════════════════════════
    // ValidateFormData – kiểu CCCD / CMND
    // ══════════════════════════════════════════════════════════════

    [Fact]
    public void ValidateFormData_InvalidNationalId_ReturnsNationalIdError()
    {
        var schemaResult = _engine.ParseAndValidateSchema(MinimalSchema(fieldId: "cccd", fieldType: "national_id"));
        var result = _engine.ValidateFormData(schemaResult.Value!, """{ "cccd": "1234" }""");

        Assert.True(result.IsSuccess);
        Assert.Equal("field.invalid_national_id", result.Value![0].ErrorCode);
    }

    [Fact]
    public void ValidateFormData_Valid12DigitNationalId_ReturnsNoError()
    {
        var schemaResult = _engine.ParseAndValidateSchema(MinimalSchema(fieldId: "cccd", fieldType: "national_id"));
        var result = _engine.ValidateFormData(schemaResult.Value!, """{ "cccd": "001234567890" }""");

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value!);
    }

    // ══════════════════════════════════════════════════════════════
    // ValidateFormData – kiểu Select
    // ══════════════════════════════════════════════════════════════

    [Fact]
    public void ValidateFormData_InvalidSelectOption_ReturnsInvalidOptionError()
    {
        const string schema = """
        {
          "title": "Mẫu giới tính",
          "sections": [
            {
              "section_id": "sec_1",
              "title": "Thông tin",
              "fields": [
                {
                  "field_id": "gioi_tinh",
                  "label": "Giới tính",
                  "type": "select",
                  "is_required": true,
                  "order": 1,
                  "options": [
                    { "label": "Nam",  "value": "male"   },
                    { "label": "Nữ",   "value": "female" }
                  ]
                }
              ]
            }
          ]
        }
        """;

        var schemaResult = _engine.ParseAndValidateSchema(schema);
        var result = _engine.ValidateFormData(schemaResult.Value!, """{ "gioi_tinh": "other" }""");

        Assert.True(result.IsSuccess);
        Assert.Single(result.Value!);
        Assert.Equal("field.invalid_option", result.Value![0].ErrorCode);
    }

    // ══════════════════════════════════════════════════════════════
    // ValidateFormData – MinLength / MaxLength
    // ══════════════════════════════════════════════════════════════

    [Fact]
    public void ValidateFormData_ValueTooShort_ReturnsMinLengthError()
    {
        const string schema = """
        {
          "title": "Mẫu độ dài",
          "sections": [
            {
              "section_id": "sec_1",
              "title": "Thông tin",
              "fields": [
                {
                  "field_id": "ghi_chu",
                  "label": "Ghi chú",
                  "type": "text",
                  "order": 1,
                  "validation": { "min_length": 10 }
                }
              ]
            }
          ]
        }
        """;

        var schemaResult = _engine.ParseAndValidateSchema(schema);
        var result = _engine.ValidateFormData(schemaResult.Value!, """{ "ghi_chu": "ngắn" }""");

        Assert.True(result.IsSuccess);
        Assert.Single(result.Value!);
        Assert.Equal("field.min_length", result.Value![0].ErrorCode);
    }

    // ══════════════════════════════════════════════════════════════
    // ValidateFormData – số và tiền tệ
    // ══════════════════════════════════════════════════════════════

    [Fact]
    public void ValidateFormData_NumberBelowMin_ReturnsMinValueError()
    {
        const string schema = """
        {
          "title": "Mẫu số tuổi",
          "sections": [
            {
              "section_id": "sec_1",
              "title": "Thông tin",
              "fields": [
                {
                  "field_id": "tuoi",
                  "label": "Tuổi",
                  "type": "number",
                  "is_required": true,
                  "order": 1,
                  "validation": { "min_value": 18, "max_value": 100 }
                }
              ]
            }
          ]
        }
        """;

        var schemaResult = _engine.ParseAndValidateSchema(schema);

        // Tuổi = 15 → nhỏ hơn min 18
        var result = _engine.ValidateFormData(schemaResult.Value!, """{ "tuoi": "15" }""");
        Assert.True(result.IsSuccess);
        Assert.Single(result.Value!);
        Assert.Equal("field.min_value", result.Value![0].ErrorCode);
    }

    [Fact]
    public void ValidateFormData_NotANumber_ReturnsInvalidNumberError()
    {
        const string schema = """
        {
          "title": "Mẫu số",
          "sections": [
            {
              "section_id": "sec_1",
              "title": "Thông tin",
              "fields": [
                { "field_id": "so_luong", "label": "Số lượng", "type": "number", "is_required": true, "order": 1 }
              ]
            }
          ]
        }
        """;

        var schemaResult = _engine.ParseAndValidateSchema(schema);
        var result = _engine.ValidateFormData(schemaResult.Value!, """{ "so_luong": "abc" }""");

        Assert.True(result.IsSuccess);
        Assert.Single(result.Value!);
        Assert.Equal("field.invalid_number", result.Value![0].ErrorCode);
    }

    // ══════════════════════════════════════════════════════════════
    // ValidateFormData – Date
    // ══════════════════════════════════════════════════════════════

    [Fact]
    public void ValidateFormData_InvalidDate_ReturnsDateError()
    {
        var schemaResult = _engine.ParseAndValidateSchema(MinimalSchema(fieldId: "ngay_sinh", fieldType: "date"));
        var result = _engine.ValidateFormData(schemaResult.Value!, """{ "ngay_sinh": "32-13-2000" }""");

        Assert.True(result.IsSuccess);
        Assert.Single(result.Value!);
        Assert.Equal("field.invalid_date", result.Value![0].ErrorCode);
    }

    [Fact]
    public void ValidateFormData_ValidDate_ReturnsNoError()
    {
        var schemaResult = _engine.ParseAndValidateSchema(MinimalSchema(fieldId: "ngay_sinh", fieldType: "date"));
        var result = _engine.ValidateFormData(schemaResult.Value!, """{ "ngay_sinh": "2000-01-15" }""");

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value!);
    }

    // ══════════════════════════════════════════════════════════════
    // ValidateFormData – Custom Regex
    // ══════════════════════════════════════════════════════════════

    [Fact]
    public void ValidateFormData_FailsCustomRegex_ReturnsPatternMismatchError()
    {
        const string schema = """
        {
          "title": "Mẫu mã bưu chính",
          "sections": [
            {
              "section_id": "sec_1",
              "title": "Địa chỉ",
              "fields": [
                {
                  "field_id": "ma_buu_chinh",
                  "label": "Mã bưu chính",
                  "type": "text",
                  "is_required": true,
                  "order": 1,
                  "validation": { "regex_pattern": "^\\d{5,6}$" }
                }
              ]
            }
          ]
        }
        """;

        var schemaResult = _engine.ParseAndValidateSchema(schema);
        var result = _engine.ValidateFormData(schemaResult.Value!, """{ "ma_buu_chinh": "ABCDE" }""");

        Assert.True(result.IsSuccess);
        Assert.Single(result.Value!);
        Assert.Equal("field.pattern_mismatch", result.Value![0].ErrorCode);
    }

    // ══════════════════════════════════════════════════════════════
    // ValidateFormData – lỗi payload
    // ══════════════════════════════════════════════════════════════

    [Fact]
    public void ValidateFormData_EmptyPayload_ReturnsFail()
    {
        var schemaResult = _engine.ParseAndValidateSchema(MinimalSchema());
        var result = _engine.ValidateFormData(schemaResult.Value!, "   ");
        Assert.False(result.IsSuccess);
    }

    [Fact]
    public void ValidateFormData_MalformedPayload_ReturnsFail()
    {
        var schemaResult = _engine.ParseAndValidateSchema(MinimalSchema());
        var result = _engine.ValidateFormData(schemaResult.Value!, "not json");
        Assert.False(result.IsSuccess);
    }
}
