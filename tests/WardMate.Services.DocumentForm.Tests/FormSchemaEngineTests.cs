using WardMate.Services.DocumentForm.Application.Services;
using WardMate.Services.DocumentForm.Domain.Models;
using Xunit;

namespace WardMate.Services.DocumentForm.Tests;

public sealed class FormSchemaEngineTests
{
    private readonly FormSchemaEngine _engine = new();

    private const string ValidSchemaJson = """
    {
      "schema_version": "1.0",
      "title": "Tờ khai đăng ký khai sinh",
      "description": "Biểu mẫu điện tử thực hiện thủ tục đăng ký khai sinh",
      "sections": [
        {
          "section_id": "thong_tin_nguoi_yeu_cau",
          "title": "1. Thông tin người yêu cầu",
          "fields": [
            {
              "field_id": "ho_ten_nguoi_yeu_cau",
              "label": "Họ chữ đệm và tên",
              "type": "text",
              "is_required": true
            },
            {
              "field_id": "cccd_nguoi_yeu_cau",
              "label": "Số định danh / CCCD",
              "type": "national_id",
              "is_required": true,
              "validation": {
                "regex_pattern": "^(\\d{9}|\\d{12})$",
                "custom_error_message": "Số CCCD phải gồm 9 hoặc 12 chữ số."
              }
            },
            {
              "field_id": "email_lien_he",
              "label": "Email liên hệ",
              "type": "email",
              "is_required": false
            }
          ]
        },
        {
          "section_id": "thong_tin_tre",
          "title": "2. Thông tin trẻ em được khai sinh",
          "fields": [
            {
              "field_id": "ho_ten_tre",
              "label": "Họ và tên trẻ",
              "type": "text",
              "is_required": true
            },
            {
              "field_id": "gioi_tinh",
              "label": "Giới tính",
              "type": "radio",
              "is_required": true,
              "options": [
                { "label": "Nam", "value": "NAM" },
                { "label": "Nữ", "value": "NU" }
              ]
            },
            {
              "field_id": "ngay_sinh",
              "label": "Ngày sinh",
              "type": "date",
              "is_required": true
            }
          ]
        }
      ]
    }
    """;

    [Fact]
    public void ParseAndValidateSchema_ValidJson_ReturnsSuccessWithPopulatedModel()
    {
        var result = _engine.ParseAndValidateSchema(ValidSchemaJson);

        Assert.True(result.IsSuccess);
        var schema = result.Value;
        Assert.Equal("1.0", schema.SchemaVersion);
        Assert.Equal("Tờ khai đăng ký khai sinh", schema.Title);
        Assert.Equal(2, schema.Sections.Count);

        var allFields = schema.GetAllFields().ToList();
        Assert.Equal(5, allFields.Count);
        Assert.Contains(allFields, f => f.FieldId == "ho_ten_nguoi_yeu_cau");
        Assert.Contains(allFields, f => f.FieldId == "gioi_tinh" && f.Options.Count == 2);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void ParseAndValidateSchema_EmptyOrWhitespace_Fails(string invalidJson)
    {
        var result = _engine.ParseAndValidateSchema(invalidJson);
        Assert.True(result.IsFailure);
        Assert.Equal("schema.invalid_format", result.Error.Code);
    }

    [Fact]
    public void ParseAndValidateSchema_MalformedJson_Fails()
    {
        var result = _engine.ParseAndValidateSchema("{ not valid json: 123 }");
        Assert.True(result.IsFailure);
        Assert.Equal("schema.invalid_format", result.Error.Code);
    }

    [Fact]
    public void ParseAndValidateSchema_DuplicateFieldIds_Fails()
    {
        var jsonWithDuplicates = """
        {
          "schema_version": "1.0",
          "title": "Biểu mẫu lỗi",
          "sections": [
            {
              "section_id": "sec1",
              "title": "Mục 1",
              "fields": [
                { "field_id": "trung_lap", "label": "Trường 1", "type": "text" }
              ]
            },
            {
              "section_id": "sec2",
              "title": "Mục 2",
              "fields": [
                { "field_id": "trung_lap", "label": "Trường 2", "type": "text" }
              ]
            }
          ]
        }
        """;

        var result = _engine.ParseAndValidateSchema(jsonWithDuplicates);
        Assert.True(result.IsFailure);
        Assert.Contains("Duplicate field ID 'trung_lap'", result.Error.Description);
    }

    [Fact]
    public void ParseAndValidateSchema_SelectWithoutOptions_Fails()
    {
        var jsonWithoutOptions = """
        {
          "schema_version": "1.0",
          "title": "Biểu mẫu lỗi",
          "sections": [
            {
              "section_id": "sec1",
              "title": "Mục 1",
              "fields": [
                { "field_id": "dan_toc", "label": "Dân tộc", "type": "select", "options": [] }
              ]
            }
          ]
        }
        """;

        var result = _engine.ParseAndValidateSchema(jsonWithoutOptions);
        Assert.True(result.IsFailure);
        Assert.Contains("must have at least one option", result.Error.Description);
    }

    [Fact]
    public void ValidateFormData_ValidData_ReturnsNoErrors()
    {
        var schema = _engine.ParseAndValidateSchema(ValidSchemaJson).Value;

        var validFormData = """
        {
          "ho_ten_nguoi_yeu_cau": "Nguyễn Văn A",
          "cccd_nguoi_yeu_cau": "012345678901",
          "email_lien_he": "vana@example.com",
          "ho_ten_tre": "Nguyễn Văn B",
          "gioi_tinh": "NAM",
          "ngay_sinh": "2026-01-15"
        }
        """;

        var result = _engine.ValidateFormData(schema, validFormData);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value);
    }

    [Fact]
    public void ValidateFormData_MissingRequiredField_ReportsErrors()
    {
        var schema = _engine.ParseAndValidateSchema(ValidSchemaJson).Value;

        var missingData = """
        {
          "ho_ten_nguoi_yeu_cau": "Nguyễn Văn A"
        }
        """;

        var result = _engine.ValidateFormData(schema, missingData);

        Assert.True(result.IsSuccess);
        var errors = result.Value;
        Assert.NotEmpty(errors);
        Assert.Contains(errors, e => e.FieldId == "cccd_nguoi_yeu_cau" && e.ErrorCode == "field.required");
        Assert.Contains(errors, e => e.FieldId == "ho_ten_tre" && e.ErrorCode == "field.required");
        Assert.Contains(errors, e => e.FieldId == "gioi_tinh" && e.ErrorCode == "field.required");
    }

    [Fact]
    public void ValidateFormData_InvalidNationalId_ReportsCustomErrorMessage()
    {
        var schema = _engine.ParseAndValidateSchema(ValidSchemaJson).Value;

        var invalidData = """
        {
          "ho_ten_nguoi_yeu_cau": "Nguyễn Văn A",
          "cccd_nguoi_yeu_cau": "123",
          "ho_ten_tre": "Nguyễn Văn B",
          "gioi_tinh": "NAM",
          "ngay_sinh": "2026-01-15"
        }
        """;

        var result = _engine.ValidateFormData(schema, invalidData);

        Assert.True(result.IsSuccess);
        var errors = result.Value;
        Assert.Contains(errors, e => e.FieldId == "cccd_nguoi_yeu_cau" && e.ErrorMessage.Contains("9 hoặc 12 chữ số"));
    }
}
