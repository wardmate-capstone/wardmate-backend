using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace WardMate.Services.AIOCR.Infrastructure.Extraction;

// One contract drives both provider schema and tolerant draft normalization.
internal static class ProcedureAiContract
{
    private static readonly JsonNode Template = JsonNode.Parse("""
    {"procedureCode":"string","title":"string","categoryId":"integer","issuingAuthority":"string","executingAgency":"string",
    "levelOfImplementation":"string","targetAudience":"string","feeSummary":"string","processingTimeSummary":"string",
    "contentPayload":{"decisionNumber":"string","receivingAddress":"string",
    "submissionMethods":[{"methodName":"string","feeAmount":"number","feeUnit":"string","estimatedDays":"number","note":"string",
    "caseCode":"string","feeText":"string","processingTimeText":"string"}],
    "legalReferences":[{"documentNumber":"string","documentName":"string","issueDate":"string","authority":"string"}],
    "results":["string"],"cases":[{"caseCode":"string","caseName":"string","steps":[{"stepOrder":"integer","stepName":"string","executor":"string","actionDetails":"string"}]}],
    "procedureType":"string","levels":["string"],"competentAuthorities":["string"],"executingAgencies":["string"],
    "authorizedAgencies":["string"],"coordinatingAgencies":["string"],"requirements":"string","receivingAddresses":["string"],
    "keywords":["string"],"description":"string","stepsText":"string"},
    "checklistSchema":[{"checklistId":"string","caseCode":"string","submissionType":"string","itemName":"string","documentCopyType":"string","quantity":"integer","conditionNote":"string","isMandatory":"boolean",
    "quantityText":"string","note":"string","formCodes":["string"]}],
    "formDefinitions":[{"caseCode":"string","formCode":"string","formName":"string","formType":"string","quantity":"integer","isMandatory":"boolean"}]}
    """)!;

    // BE-H: trường bổ sung theo khung Cổng DVC — thường để trống nên không cảnh báo khi null/rỗng.
    // Chỉ thêm trường phẳng (độ sâu như trường cũ) để schema structured output không vượt giới hạn lồng nhau.
    private static readonly HashSet<string> OptionalKeys = new(StringComparer.Ordinal)
    {
        "procedureType", "levels", "competentAuthorities", "executingAgencies", "authorizedAgencies", "coordinatingAgencies",
        "requirements", "receivingAddresses", "keywords", "description", "stepsText", "feeText", "processingTimeText",
        "quantityText", "note", "formCodes"
    };

    public static object ResponseFormat => new
    {
        type = "json_schema",
        json_schema = new { name = "procedure_extraction", strict = true, schema = new JsonObject
        {
            ["type"] = "object", ["additionalProperties"] = false,
            ["required"] = new JsonArray("payload", "warnings"),
            ["properties"] = new JsonObject { ["payload"] = Schema(Template),
                ["warnings"] = new JsonObject { ["type"] = "array", ["items"] = new JsonObject { ["type"] = "string" } } }
        } }
    };

    private static JsonNode Schema(JsonNode template)
    {
        if (template is JsonObject obj)
        {
            var props = new JsonObject(); var required = new JsonArray();
            foreach (var pair in obj) { props[pair.Key] = Schema(pair.Value!); required.Add(pair.Key); }
            return new JsonObject { ["type"] = "object", ["additionalProperties"] = false, ["properties"] = props, ["required"] = required };
        }
        if (template is JsonArray array)
            return new JsonObject { ["type"] = "array", ["items"] = Schema(array[0]!) };
        return new JsonObject { ["type"] = new JsonArray(template.GetValue<string>(), "null") };
    }

    public static JsonElement Normalize(JsonElement payload, List<string> warnings)
    {
        var normalized = (JsonObject)Read(Template, JsonNode.Parse(payload.GetRawText()), "payload", warnings)!;
        normalized["categoryId"] = 0;
        // BE-H: processingTimeText (phẳng cho AI) → processingTime.text của Catalog.
        foreach (var method in normalized["contentPayload"]!["submissionMethods"]!.AsArray().OfType<JsonObject>())
        {
            var text = method["processingTimeText"]?.DeepClone();
            method.Remove("processingTimeText");
            if (text is not null) method["processingTime"] = new JsonObject { ["value"] = null, ["unit"] = null, ["text"] = text };
        }
        warnings.Add("categoryId: Chọn danh mục trong hệ thống trước khi xuất bản.");
        var cases = normalized["contentPayload"]!["cases"]!.AsArray();
        var codes = cases.Select(x => x?["caseCode"]?.GetValue<string>()).Where(x => !string.IsNullOrWhiteSpace(x)).ToHashSet();
        foreach (var list in new[] { "checklistSchema", "formDefinitions" })
            for (var i = 0; i < normalized[list]!.AsArray().Count; i++)
            {
                var code = normalized[list]![i]?["caseCode"]?.GetValue<string>();
                if (code != null && !codes.Contains(code))
                    warnings.Add($"{list}[{i}].caseCode: Không khớp trường hợp nào; cần sửa liên kết trước khi xuất bản.");
            }
        return JsonSerializer.SerializeToElement(normalized);
    }

    // Deterministic guard: template file names (.doc/.docx) in the source text are counted, never trusted to the AI alone.
    private static readonly Regex TemplateFile = new(@"\.docx?\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));

    public static void CheckFormCoverage(string source, JsonElement payload, List<string> warnings)
    {
        var files = TemplateFile.Matches(source).Count;
        var forms = payload.TryGetProperty("formDefinitions", out var list) && list.ValueKind == JsonValueKind.Array ? list.GetArrayLength() : 0;
        if (files > forms)
            warnings.Add($"formDefinitions: Văn bản nguồn có {files} tệp mẫu (.doc/.docx) nhưng chỉ bóc tách được {forms} biểu mẫu; kiểm tra cột \"Mẫu đơn, tờ khai\" và bổ sung mẫu còn thiếu.");
    }

    private static JsonNode? Read(JsonNode template, JsonNode? value, string path, List<string> warnings, bool optional = false)
    {
        if (template is JsonObject obj)
        {
            var result = new JsonObject();
            if (value is not JsonObject) warnings.Add($"{path}: Thiếu hoặc sai kiểu object; cần bổ sung.");
            foreach (var pair in obj) result[pair.Key] = Read(pair.Value!, (value as JsonObject)?[pair.Key], path + "." + pair.Key, warnings,
                optional || OptionalKeys.Contains(pair.Key));
            return result;
        }
        if (template is JsonArray array)
        {
            var result = new JsonArray();
            if (value is not JsonArray input)
            {
                if (!optional || value is not null) warnings.Add($"{path}: Chưa có danh sách hợp lệ; cần đối soát.");
                return result;
            }
            if (input.Count == 0 && !optional) warnings.Add($"{path}: Danh sách trống; kiểm tra PDF để xác nhận không bỏ sót.");
            for (var i = 0; i < input.Count; i++) result.Add(Read(array[0]!, input[i], $"{path}[{i}]", warnings, optional));
            return result;
        }
        if (optional && (value is null || (value is JsonValue blank && blank.TryGetValue<string>(out var s) && string.IsNullOrWhiteSpace(s))))
            return null;
        var type = template.GetValue<string>();
        var valid = value is JsonValue scalar && type switch
        {
            "string" => scalar.TryGetValue<string>(out var text) && !string.IsNullOrWhiteSpace(text),
            "integer" => scalar.TryGetValue<int>(out _),
            "number" => scalar.TryGetValue<decimal>(out _),
            "boolean" => scalar.TryGetValue<bool>(out _),
            _ => false
        };
        if (valid) return value!.DeepClone();
        warnings.Add($"{path}: Thiếu hoặc sai kiểu dữ liệu; cần đối soát và bổ sung.");
        return null;
    }
}
