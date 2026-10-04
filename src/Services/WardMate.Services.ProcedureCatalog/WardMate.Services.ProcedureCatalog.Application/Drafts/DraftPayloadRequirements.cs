using System.Text.Json;

namespace WardMate.Services.ProcedureCatalog.Application.Drafts;

public static class DraftPayloadRequirements
{
    // Drafts preserve unknown values. Publication must not turn omitted fields into POCO defaults.
    public static Dictionary<string, string[]> Check(JsonElement payload)
    {
        var errors = new Dictionary<string, string[]>();
        void Require(JsonElement value, string prefix, params string[] fields)
        {
            foreach (var field in fields)
                if (value.ValueKind != JsonValueKind.Object || !value.TryGetProperty(field, out var property) || property.ValueKind == JsonValueKind.Null)
                    errors[prefix + field] = ["Cần đối soát và cung cấp giá trị; không tự áp dụng giá trị mặc định."];
        }
        Require(payload, "", "categoryId", "procedureCode", "title", "levelOfImplementation", "targetAudience", "feeSummary", "processingTimeSummary", "contentPayload");
        void Items(JsonElement parent, string name, string prefix, params string[] fields)
        {
            if (parent.ValueKind != JsonValueKind.Object || !parent.TryGetProperty(name, out var items) || items.ValueKind != JsonValueKind.Array) return;
            var i = 0;
            foreach (var item in items.EnumerateArray()) Require(item, $"{prefix}{name}[{i++}].", fields);
        }
        if (payload.ValueKind == JsonValueKind.Object && payload.TryGetProperty("contentPayload", out var content))
        {
            Require(content, "contentPayload.", "cases", "submissionMethods", "legalReferences", "results");
            Items(content, "submissionMethods", "contentPayload.", "feeAmount", "feeUnit", "estimatedDays");
        }
        Items(payload, "checklistSchema", "", "submissionType", "documentCopyType", "quantity", "isMandatory");
        Items(payload, "formDefinitions", "", "formType", "quantity", "isMandatory");
        return errors;
    }
}
