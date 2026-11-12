using System.Text.Json;
using WardMate.Services.AIOCR.Infrastructure.Extraction;

namespace WardMate.UnitTests.Aiocr;

// AI-02b: thủ tục chia thành phần hồ sơ theo "Trường hợp N:" (vd 1.001939) và một dòng giấy tờ nhiều mẫu (vd 1.014748).
public sealed class ProcedureCaseExtractionTests
{
    private const string Source = """
        --- Trang 2 ---
        THÀNH PHẦN HỒ SƠ
        Trường hợp 2: - Người chỉ tham gia BHYT:
        Tên giấy tờ Mẫu đơn, tờ khai Số lượng
        --- Trang 3 ---
        Trường hợp 3: Người có công với cách mạng
        --- Trang 4 ---
        Trường hợp 5: 3. Thân nhân của liệt sĩ
        """;

    private static JsonElement Payload(object[] cases, object[] checklist) => JsonSerializer.SerializeToElement(new
    {
        contentPayload = new { cases },
        checklistSchema = checklist
    });

    [Fact]
    public void Missing_cases_are_reported_with_source_numbers()
    {
        var warnings = new List<string>();
        ProcedureAiContract.CheckCaseCoverage(Source,
            Payload([new { caseCode = "TH-2" }], [new { caseCode = "TH-2", itemName = "Tờ khai" }]), warnings);
        var warning = Assert.Single(warnings);
        Assert.Contains("3 trường hợp (Trường hợp 2, 3, 5)", warning);
        Assert.Contains("chỉ bóc tách được 1", warning);
    }

    [Fact]
    public void Checklist_without_case_is_reported_when_source_has_cases()
    {
        var warnings = new List<string>();
        ProcedureAiContract.CheckCaseCoverage(Source,
            Payload([new { caseCode = "TH-2" }, new { caseCode = "TH-3" }, new { caseCode = "TH-5" }],
                [new { caseCode = (string?)"TH-2", itemName = "Tờ khai" }, new { caseCode = (string?)null, itemName = "Giấy ra viện" }]), warnings);
        var warning = Assert.Single(warnings);
        Assert.StartsWith("checklistSchema: 1 giấy tờ chưa gắn trường hợp", warning);
    }

    [Fact]
    public void Complete_case_mapping_and_sources_without_cases_produce_no_warning()
    {
        var warnings = new List<string>();
        ProcedureAiContract.CheckCaseCoverage(Source,
            Payload([new { caseCode = "TH-2" }, new { caseCode = "TH-3" }, new { caseCode = "TH-5" }],
                [new { caseCode = "TH-5", itemName = "Giấy chứng nhận thân nhân liệt sỹ" }]), warnings);
        ProcedureAiContract.CheckCaseCoverage("THÀNH PHẦN HỒ SƠ\nTên giấy tờ Mẫu đơn, tờ khai Số lượng",
            Payload([], [new { caseCode = (string?)null, itemName = "Đơn" }]), warnings);
        Assert.Empty(warnings);
    }

    [Fact]
    public void Cases_without_steps_do_not_warn_and_multi_form_row_keeps_one_item()
    {
        var payload = JsonSerializer.SerializeToElement(new
        {
            procedureCode = "1.014748", title = "Hưởng trợ cấp thất nghiệp",
            contentPayload = new { cases = new[] { new { caseCode = "TH-2", caseName = "Người chỉ tham gia BHYT", steps = Array.Empty<object>() } } },
            checklistSchema = new[] { new { checklistId = "DOC-1", caseCode = "TH-2", itemName = "Đề nghị hưởng trợ cấp thất nghiệp", formCodes = new[] { "Mẫu số 10", "Mẫu số 11" } } },
            formDefinitions = new[]
            {
                new { caseCode = "TH-2", formCode = "Mẫu số 10", formName = "Đề nghị hưởng trợ cấp thất nghiệp (Mẫu số 10)", isMandatory = true },
                new { caseCode = "TH-2", formCode = "Mẫu số 11", formName = "Đề nghị không hưởng trợ cấp thất nghiệp (Mẫu số 11)", isMandatory = false }
            }
        });
        var warnings = new List<string>();
        var normalized = ProcedureAiContract.Normalize(payload, warnings);
        Assert.DoesNotContain(warnings, w => w.Contains(".steps"));
        Assert.DoesNotContain(warnings, w => w.Contains("Không khớp trường hợp"));
        Assert.Equal(1, normalized.GetProperty("checklistSchema").GetArrayLength());
        Assert.Equal(2, normalized.GetProperty("checklistSchema")[0].GetProperty("formCodes").GetArrayLength());
        Assert.Equal(2, normalized.GetProperty("formDefinitions").GetArrayLength());
    }
}
