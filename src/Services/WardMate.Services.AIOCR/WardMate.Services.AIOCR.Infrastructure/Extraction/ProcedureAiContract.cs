using System.Text.Json;
using System.Text.Json.Nodes;

namespace WardMate.Services.AIOCR.Infrastructure.Extraction;

// One contract drives both provider schema and tolerant draft normalization.
internal static class ProcedureAiContract
{
    private static readonly JsonNode Template = JsonNode.Parse("""
    {"procedureCode":"string","title":"string","categoryId":"integer","issuingAuthority":"string","executingAgency":"string",
    "levelOfImplementation":"string","targetAudience":"string","feeSummary":"string","processingTimeSummary":"string",
    "contentPayload":{"decisionNumber":"string","receivingAddress":"string",
    "submissionMethods":[{"methodName":"string","feeAmount":"number","feeUnit":"string","estimatedDays":"number","note":"string"}],
    "legalReferences":[{"documentNumber":"string","documentName":"string","issueDate":"string","authority":"string"}],
    "results":["string"],"cases":[{"caseCode":"string","caseName":"string","steps":[{"stepOrder":"integer","stepName":"string","executor":"string","actionDetails":"string"}]}]},
    "checklistSchema":[{"checklistId":"string","caseCode":"string","submissionType":"string","itemName":"string","documentCopyType":"string","quantity":"integer","conditionNote":"string","isMandatory":"boolean"}],
    "formDefinitions":[{"caseCode":"string","formCode":"string","formName":"string","formType":"string","quantity":"integer","isMandatory":"boolean"}]}
    """)!;

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

    private static JsonNode? Read(JsonNode template, JsonNode? value, string path, List<string> warnings)
    {
        if (template is JsonObject obj)
        {
            var result = new JsonObject();
            if (value is not JsonObject) warnings.Add($"{path}: Thiếu hoặc sai kiểu object; cần bổ sung.");
            foreach (var pair in obj) result[pair.Key] = Read(pair.Value!, (value as JsonObject)?[pair.Key], path + "." + pair.Key, warnings);
            return result;
        }
        if (template is JsonArray array)
        {
            var result = new JsonArray();
            if (value is not JsonArray input) { warnings.Add($"{path}: Chưa có danh sách hợp lệ; cần đối soát."); return result; }
            if (input.Count == 0) warnings.Add($"{path}: Danh sách trống; kiểm tra PDF để xác nhận không bỏ sót.");
            for (var i = 0; i < input.Count; i++) result.Add(Read(array[0]!, input[i], $"{path}[{i}]", warnings));
            return result;
        }
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
