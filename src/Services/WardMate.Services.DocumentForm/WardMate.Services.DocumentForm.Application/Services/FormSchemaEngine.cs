using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using WardMate.Services.DocumentForm.Application.Interfaces;
using WardMate.Services.DocumentForm.Application.Models;
using WardMate.Services.DocumentForm.Domain.Errors;
using WardMate.Services.DocumentForm.Domain.Models;
using WardMate.SharedKernel.Common;

namespace WardMate.Services.DocumentForm.Application.Services;

/// <summary>
/// Triển khai FormSchemaEngine: kiểm tra cấu trúc schema JSON và kiểm tra tính hợp lệ của dữ liệu nhập trên E-Forms.
/// </summary>
public sealed class FormSchemaEngine : IFormSchemaEngine
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        Converters =
        {
            new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower)
        }
    };

    private static readonly Regex FieldIdRegex = new(@"^[a-zA-Z0-9_]+$", RegexOptions.Compiled);
    private static readonly Regex EmailRegex = new(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.Compiled);
    private static readonly Regex PhoneRegex = new(@"^(0|\+84)[1-9][0-9]{8}$", RegexOptions.Compiled);
    private static readonly Regex NationalIdRegex = new(@"^(\d{9}|\d{12})$", RegexOptions.Compiled);

    public Result<FormSchemaDefinition> ParseAndValidateSchema(string schemaDefinitionJson)
    {
        if (string.IsNullOrWhiteSpace(schemaDefinitionJson))
        {
            return DocumentFormErrors.InvalidSchema("Schema definition JSON cannot be empty.");
        }

        FormSchemaDefinition? schema;
        try
        {
            schema = JsonSerializer.Deserialize<FormSchemaDefinition>(schemaDefinitionJson, JsonOptions);
        }
        catch (JsonException ex)
        {
            return DocumentFormErrors.InvalidSchema($"Invalid JSON format: {ex.Message}");
        }

        if (schema is null)
        {
            return DocumentFormErrors.InvalidSchema("Deserialized schema definition is null.");
        }

        if (string.IsNullOrWhiteSpace(schema.Title))
        {
            return DocumentFormErrors.InvalidSchema("Schema title is required.");
        }

        if (schema.Sections.Count == 0)
        {
            return DocumentFormErrors.InvalidSchema("Schema must contain at least one section.");
        }

        var sectionIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var fieldIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var section in schema.Sections)
        {
            if (string.IsNullOrWhiteSpace(section.SectionId))
            {
                return DocumentFormErrors.InvalidSchema("Section ID is required for all sections.");
            }

            if (!sectionIds.Add(section.SectionId))
            {
                return DocumentFormErrors.InvalidSchema($"Duplicate section ID '{section.SectionId}'.");
            }

            if (string.IsNullOrWhiteSpace(section.Title))
            {
                return DocumentFormErrors.InvalidSchema($"Section '{section.SectionId}' must have a title.");
            }

            foreach (var field in section.Fields)
            {
                if (string.IsNullOrWhiteSpace(field.FieldId))
                {
                    return DocumentFormErrors.InvalidSchema($"Field ID is required in section '{section.SectionId}'.");
                }

                if (!FieldIdRegex.IsMatch(field.FieldId))
                {
                    return DocumentFormErrors.InvalidSchema($"Field ID '{field.FieldId}' must contain only alphanumeric characters and underscores.");
                }

                if (!fieldIds.Add(field.FieldId))
                {
                    return DocumentFormErrors.InvalidSchema($"Duplicate field ID '{field.FieldId}' detected across form sections.");
                }

                if (string.IsNullOrWhiteSpace(field.Label))
                {
                    return DocumentFormErrors.InvalidSchema($"Field '{field.FieldId}' must have a label.");
                }

                if (field.Type is FormFieldType.Select or FormFieldType.Radio)
                {
                    if (field.Options.Count == 0)
                    {
                        return DocumentFormErrors.InvalidSchema($"Field '{field.FieldId}' of type '{field.Type}' must have at least one option.");
                    }

                    var optionValues = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    foreach (var option in field.Options)
                    {
                        if (string.IsNullOrWhiteSpace(option.Label) || string.IsNullOrWhiteSpace(option.Value))
                        {
                            return DocumentFormErrors.InvalidSchema($"Options for field '{field.FieldId}' must have non-empty label and value.");
                        }

                        if (!optionValues.Add(option.Value))
                        {
                            return DocumentFormErrors.InvalidSchema($"Duplicate option value '{option.Value}' in field '{field.FieldId}'.");
                        }
                    }
                }

                if (field.Validation?.RegexPattern is not null)
                {
                    try
                    {
                        _ = new Regex(field.Validation.RegexPattern);
                    }
                    catch (ArgumentException)
                    {
                        return DocumentFormErrors.InvalidSchema($"Invalid regular expression pattern in field '{field.FieldId}'.");
                    }
                }
            }
        }

        return schema;
    }

    public Result<IReadOnlyList<FormDataValidationError>> ValidateFormData(FormSchemaDefinition schema, string formDataJson)
    {
        ArgumentNullException.ThrowIfNull(schema);

        if (string.IsNullOrWhiteSpace(formDataJson))
        {
            return DocumentFormErrors.InvalidFormData("Form data payload cannot be empty.");
        }

        JsonDocument jsonDoc;
        try
        {
            jsonDoc = JsonDocument.Parse(formDataJson);
        }
        catch (JsonException ex)
        {
            return DocumentFormErrors.InvalidFormData($"Malformed form data JSON: {ex.Message}");
        }

        using (jsonDoc)
        {
            if (jsonDoc.RootElement.ValueKind != JsonValueKind.Object)
            {
                return DocumentFormErrors.InvalidFormData("Form data must be a JSON object.");
            }

            var root = jsonDoc.RootElement;
            var errors = new List<FormDataValidationError>();

            foreach (var field in schema.GetAllFields())
            {
                var hasProperty = root.TryGetProperty(field.FieldId, out var propElement);
                var isNullOrEmpty = !hasProperty ||
                                    propElement.ValueKind == JsonValueKind.Null ||
                                    propElement.ValueKind == JsonValueKind.Undefined ||
                                    (propElement.ValueKind == JsonValueKind.String && string.IsNullOrWhiteSpace(propElement.GetString()));

                // 1. Kiểm tra bắt buộc (Required)
                if (field.IsRequired && isNullOrEmpty)
                {
                    errors.Add(new FormDataValidationError
                    {
                        FieldId = field.FieldId,
                        Label = field.Label,
                        ErrorCode = "field.required",
                        ErrorMessage = field.Validation?.CustomErrorMessage ?? $"Trường '{field.Label}' là bắt buộc."
                    });
                    continue;
                }

                if (isNullOrEmpty)
                {
                    continue;
                }

                var stringVal = propElement.ValueKind == JsonValueKind.String
                    ? propElement.GetString()?.Trim() ?? string.Empty
                    : propElement.ToString();

                // 2. Kiểm tra độ dài chuỗi (MinLength / MaxLength)
                if (field.Validation?.MinLength is not null && stringVal.Length < field.Validation.MinLength.Value)
                {
                    errors.Add(new FormDataValidationError
                    {
                        FieldId = field.FieldId,
                        Label = field.Label,
                        ErrorCode = "field.min_length",
                        ErrorMessage = field.Validation.CustomErrorMessage ?? $"'{field.Label}' phải có tối thiểu {field.Validation.MinLength.Value} ký tự."
                    });
                }

                if (field.Validation?.MaxLength is not null && stringVal.Length > field.Validation.MaxLength.Value)
                {
                    errors.Add(new FormDataValidationError
                    {
                        FieldId = field.FieldId,
                        Label = field.Label,
                        ErrorCode = "field.max_length",
                        ErrorMessage = field.Validation.CustomErrorMessage ?? $"'{field.Label}' không được vượt quá {field.Validation.MaxLength.Value} ký tự."
                    });
                }

                // 3. Kiểm tra kiểu dữ liệu đặc thù (Field Types)
                switch (field.Type)
                {
                    case FormFieldType.Number or FormFieldType.Currency:
                        if (!decimal.TryParse(stringVal, out var numberVal))
                        {
                            errors.Add(new FormDataValidationError
                            {
                                FieldId = field.FieldId,
                                Label = field.Label,
                                ErrorCode = "field.invalid_number",
                                ErrorMessage = $"'{field.Label}' phải là một số hợp lệ."
                            });
                        }
                        else
                        {
                            if (field.Validation?.MinValue is not null && numberVal < field.Validation.MinValue.Value)
                            {
                                errors.Add(new FormDataValidationError
                                {
                                    FieldId = field.FieldId,
                                    Label = field.Label,
                                    ErrorCode = "field.min_value",
                                    ErrorMessage = $"'{field.Label}' phải lớn hơn hoặc bằng {field.Validation.MinValue.Value}."
                                });
                            }

                            if (field.Validation?.MaxValue is not null && numberVal > field.Validation.MaxValue.Value)
                            {
                                errors.Add(new FormDataValidationError
                                {
                                    FieldId = field.FieldId,
                                    Label = field.Label,
                                    ErrorCode = "field.max_value",
                                    ErrorMessage = $"'{field.Label}' không được vượt quá {field.Validation.MaxValue.Value}."
                                });
                            }
                        }
                        break;

                    case FormFieldType.Date:
                        if (!DateOnly.TryParse(stringVal, out _) && !DateTime.TryParse(stringVal, out _))
                        {
                            errors.Add(new FormDataValidationError
                            {
                                FieldId = field.FieldId,
                                Label = field.Label,
                                ErrorCode = "field.invalid_date",
                                ErrorMessage = $"'{field.Label}' phải là ngày hợp lệ (định dạng YYYY-MM-DD)."
                            });
                        }
                        break;

                    case FormFieldType.Email:
                        if (!EmailRegex.IsMatch(stringVal))
                        {
                            errors.Add(new FormDataValidationError
                            {
                                FieldId = field.FieldId,
                                Label = field.Label,
                                ErrorCode = "field.invalid_email",
                                ErrorMessage = $"'{field.Label}' phải là địa chỉ email hợp lệ."
                            });
                        }
                        break;

                    case FormFieldType.PhoneNumber:
                        if (!PhoneRegex.IsMatch(stringVal.Replace(" ", "").Replace("-", "")))
                        {
                            errors.Add(new FormDataValidationError
                            {
                                FieldId = field.FieldId,
                                Label = field.Label,
                                ErrorCode = "field.invalid_phone",
                                ErrorMessage = $"'{field.Label}' phải là số điện thoại Việt Nam hợp lệ (10 chữ số bắt đầu bằng 0 hoặc +84)."
                            });
                        }
                        break;

                    case FormFieldType.NationalId:
                        if (!NationalIdRegex.IsMatch(stringVal.Replace(" ", "")))
                        {
                            errors.Add(new FormDataValidationError
                            {
                                FieldId = field.FieldId,
                                Label = field.Label,
                                ErrorCode = "field.invalid_national_id",
                                ErrorMessage = $"'{field.Label}' phải là số CCCD hợp lệ (12 chữ số) hoặc CMND (9 chữ số)."
                            });
                        }
                        break;

                    case FormFieldType.Select or FormFieldType.Radio:
                        var validOptions = field.Options.Select(o => o.Value);
                        if (!validOptions.Contains(stringVal, StringComparer.OrdinalIgnoreCase))
                        {
                            errors.Add(new FormDataValidationError
                            {
                                FieldId = field.FieldId,
                                Label = field.Label,
                                ErrorCode = "field.invalid_option",
                                ErrorMessage = $"Giá trị chọn cho '{field.Label}' không nằm trong danh mục cho phép."
                            });
                        }
                        break;
                }

                // 4. Kiểm tra Regex tùy biến
                if (field.Validation?.RegexPattern is not null &&
                    !Regex.IsMatch(stringVal, field.Validation.RegexPattern))
                {
                    errors.Add(new FormDataValidationError
                    {
                        FieldId = field.FieldId,
                        Label = field.Label,
                        ErrorCode = "field.pattern_mismatch",
                        ErrorMessage = field.Validation.CustomErrorMessage ?? $"'{field.Label}' không đúng định dạng yêu cầu."
                    });
                }
            }

            return errors;
        }
    }
}
