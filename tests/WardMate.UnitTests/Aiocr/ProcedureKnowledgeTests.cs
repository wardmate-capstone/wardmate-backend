using System.Text.Json;
using WardMate.Services.AIOCR.Application;
using WardMate.Services.AIOCR.Infrastructure.Extraction;

namespace WardMate.UnitTests.Aiocr;

// KB-01a, AI-02a (BE-H): AIOCR đọc thủ tục linh hoạt và schema bóc tách mới.
public sealed class ProcedureKnowledgeTests
{
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    private const string FlexibleProcedure = """
    {
      "id": "07000000-0000-0000-0000-000000000009", "procedureCode": "3.000781", "title": "Đăng ký đất đai",
      "categoryName": "Đất đai", "isActive": true, "updatedAt": "2026-11-10T00:00:00Z",
      "contentPayload": {
        "decisionNumber": "135/QĐ", "receivingAddress": "", "results": [], "legalReferences": [],
        "cases": [],
        "submissionMethods": [
          { "methodName": "Trực tiếp", "feeAmount": null, "feeUnit": "VND", "estimatedDays": null,
            "feeText": "Theo quy định của Bộ Tài chính", "processingTime": { "value": null, "unit": null, "text": "Không quá 15 phút" } },
          { "methodName": "Trực tuyến", "feeAmount": 0, "feeUnit": "VND", "estimatedDays": null,
            "fees": [ { "kind": "LE_PHI", "amount": 0 }, { "kind": "PHI", "percent": 0.5, "description": "Tổ chức" } ] }
        ],
        "requirements": "Đất không có tranh chấp.",
        "stepsText": "Bước 1: Nộp hồ sơ.\nBước 2: Nhận kết quả.",
        "coordinatingAgencies": ["Cơ quan thuế"],
        "resultDetails": [ { "name": "Giấy chứng nhận", "code": "KQ.H17.000312" } ],
        "additionalSections": [ { "title": "Lưu ý", "content": "Mang bản gốc." } ]
      },
      "checklistSchema": [
        { "itemName": "Đơn đăng ký", "documentCopyType": "OTHER", "quantity": 1, "isMandatory": true,
          "quantityText": "01 bản chính và 01 bản sao", "formCodes": ["M25"] }
      ],
      "formDefinitions": [ { "formCode": "M25", "formName": "Mẫu số 25", "quantity": 1, "isMandatory": true } ]
    }
    """;

    [Fact]
    public void Nullable_fee_and_new_fields_are_read_and_rendered()
    {
        var procedure = JsonSerializer.Deserialize<CatalogProcedure>(FlexibleProcedure, Web)!;
        var text = ProcedureKnowledgeText.Build(procedure);
        Assert.Contains("phí, lệ phí: Theo quy định của Bộ Tài chính", text);
        Assert.Contains("thời hạn Không quá 15 phút", text);
        Assert.Contains("lệ phí miễn phí", text);
        Assert.Contains("0,5%", text);
        Assert.Contains("## Yêu cầu, điều kiện thực hiện", text);
        Assert.Contains("Bước 2: Nhận kết quả.", text);
        Assert.Contains("Cơ quan phối hợp: Cơ quan thuế", text);
        Assert.Contains("01 bản chính và 01 bản sao", text);
        Assert.Contains("mẫu: Mẫu số 25", text);
        Assert.Contains("Giấy chứng nhận (mã KQ.H17.000312)", text);
        Assert.Contains("## Lưu ý", text);
    }

    [Fact]
    public void Legacy_method_text_is_unchanged()
    {
        var procedure = JsonSerializer.Deserialize<CatalogProcedure>("""
        { "id": "07000000-0000-0000-0000-000000000001", "title": "Kết hôn", "isActive": true, "updatedAt": "2026-11-10T00:00:00Z",
          "contentPayload": { "submissionMethods": [ { "methodName": "Trực tiếp", "feeAmount": 0, "feeUnit": "VND", "estimatedDays": 1 } ] } }
        """, Web)!;
        Assert.Contains("- Trực tiếp: không thu phí, thời gian dự kiến 1 ngày.", ProcedureKnowledgeText.Build(procedure));
    }

    [Fact]
    public void Extraction_maps_time_text_and_does_not_warn_for_optional_fields()
    {
        var payload = JsonSerializer.SerializeToElement(new
        {
            procedureCode = "3.000781", title = "Đăng ký đất đai",
            contentPayload = new
            {
                submissionMethods = new[] { new { methodName = "Trực tiếp", processingTimeText = "Trong ngày", feeText = (string?)null } },
                requirements = (string?)null, levels = Array.Empty<string>()
            },
            checklistSchema = new[] { new { checklistId = "DOC-1", itemName = "Đơn", formCodes = new[] { "M25" } } }
        });
        var warnings = new List<string>();
        var normalized = ProcedureAiContract.Normalize(payload, warnings);
        var method = normalized.GetProperty("contentPayload").GetProperty("submissionMethods")[0];
        Assert.False(method.TryGetProperty("processingTimeText", out _));
        Assert.Equal("Trong ngày", method.GetProperty("processingTime").GetProperty("text").GetString());
        Assert.Equal("M25", normalized.GetProperty("checklistSchema")[0].GetProperty("formCodes")[0].GetString());
        foreach (var key in new[] { "requirements", "levels", "feeText", "processingTimeText", "keywords", "stepsText", "quantityText", "formCodes" })
            Assert.DoesNotContain(warnings, w => w.Contains("." + key));
    }
}
