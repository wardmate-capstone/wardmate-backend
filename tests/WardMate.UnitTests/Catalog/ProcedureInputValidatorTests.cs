using System.Text.Json;
using WardMate.Services.ProcedureCatalog.Application.Management;
using WardMate.Services.ProcedureCatalog.Domain.JsonModels;

namespace WardMate.UnitTests.Catalog;

// PROC-02b (B3) và PROC-05a–f (BE-H).
public sealed class ProcedureInputValidatorTests
{
    private static readonly ProcedureInputValidator Validator = new();
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    private static ProcedureInput Input() => new() { CategoryId = 1, ProcedureCode = "P1", Title = "Thủ tục thử" };
    private static ChecklistItemSchema Item(string id = "GT01") => new() { ChecklistId = id, ItemName = "Giấy tờ" };
    private static FormDefinitionSchema Form(string code, string? caseCode = null) =>
        new() { FormCode = code, FormName = "Mẫu " + code, FormType = "DOCX_TEMPLATE", CaseCode = caseCode };

    private static bool Valid(ProcedureInput input) => Validator.Validate(input).IsValid;
    private static string Errors(ProcedureInput input) => string.Join(" | ", Validator.Validate(input).Errors.Select(e => e.ErrorMessage));

    [Fact]
    public void Minimal_procedure_is_valid() => Assert.True(Valid(Input()));

    [Theory]
    [InlineData(1000, true)]
    [InlineData(1001, false)]
    public void Title_limit_is_1000(int length, bool valid)
    {
        var input = Input(); input.Title = new string('a', length);
        Assert.Equal(valid, Valid(input));
    }

    [Theory]
    [InlineData(100, true)]
    [InlineData(101, false)]
    public void Procedure_code_limit_is_100(int length, bool valid)
    {
        var input = Input(); input.ProcedureCode = new string('A', length);
        Assert.Equal(valid, Valid(input));
    }

    [Theory]
    [InlineData(10_000, true)]
    [InlineData(10_001, false)]
    public void Item_name_and_target_audience_limit_is_10000(int length, bool valid)
    {
        var input = Input();
        input.TargetAudience = new string('d', length);
        input.ChecklistSchema = [new() { ChecklistId = "GT01", ItemName = new string('x', length) }];
        Assert.Equal(valid, Valid(input));
    }

    [Fact]
    public void Total_content_over_500000_is_rejected()
    {
        var input = Input();
        input.ContentPayload = new() { StepsText = new string('b', 500_001) };
        var result = Validator.Validate(input);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "ContentPayload" && e.ErrorMessage.Contains("500.000"));
    }

    [Fact]
    public void Other_copy_type_is_accepted()
    {
        var input = Input();
        var item = Item(); item.DocumentCopyType = "OTHER"; item.QuantityText = "01 bản chính và 01 bản sao";
        input.ChecklistSchema = [item];
        Assert.True(Valid(input));
    }

    [Fact]
    public void Form_codes_must_exist_in_form_definitions()
    {
        var input = Input();
        var item = Item("GT01"); item.FormCodes = ["M99"];
        input.ChecklistSchema = [item];
        input.FormDefinitions = [Form("M25")];
        var errors = Errors(input);
        Assert.Contains("GT01", errors);
        Assert.Contains("M99", errors);
    }

    [Fact]
    public void Item_can_link_several_shared_forms()
    {
        var input = Input();
        var item = Item(); item.FormCodes = ["M25", "01.LPTB"];
        input.ChecklistSchema = [item];
        input.FormDefinitions = [Form("M25"), Form("01.LPTB")];
        Assert.True(Valid(input));
    }

    [Fact]
    public void Form_codes_cannot_point_to_another_case()
    {
        var input = Input();
        input.ContentPayload = new() { Cases = [new() { CaseCode = "TH1", CaseName = "A" }, new() { CaseCode = "TH2", CaseName = "B" }] };
        var item = Item(); item.CaseCode = "TH1"; item.FormCodes = ["M2"];
        input.ChecklistSchema = [item];
        input.FormDefinitions = [Form("M2", "TH2")];
        Assert.False(Valid(input));
    }

    [Fact]
    public void Duplicate_form_codes_in_item_are_rejected()
    {
        var input = Input();
        var item = Item(); item.FormCodes = ["M25", "M25"];
        input.ChecklistSchema = [item];
        input.FormDefinitions = [Form("M25")];
        Assert.False(Valid(input));
    }

    [Fact]
    public void Step_needs_only_name_or_details()
    {
        var input = Input();
        input.ContentPayload = new() { CommonSteps = [new() { ActionDetails = "Nộp hồ sơ" }] };
        Assert.True(Valid(input));
        input.ContentPayload.CommonSteps = [new() { StepOrder = 1 }];
        Assert.False(Valid(input));
    }

    [Fact]
    public void Fee_and_time_values_are_checked()
    {
        var input = Input();
        input.ContentPayload = new() { SubmissionMethods = [new() { MethodName = "Trực tiếp", FeeAmount = null, EstimatedDays = null }] };
        Assert.True(Valid(input));
        input.ContentPayload.SubmissionMethods[0].Fees = [new() { Percent = 101 }];
        Assert.False(Valid(input));
        input.ContentPayload.SubmissionMethods[0].Fees = [new() { Kind = "LE_PHI", Percent = 0.5m }];
        input.ContentPayload.SubmissionMethods[0].ProcessingTime = new() { Value = 15, Unit = "WEEK" };
        Assert.False(Valid(input));
        input.ContentPayload.SubmissionMethods[0].ProcessingTime = new() { Value = 15, Unit = "MINUTE" };
        Assert.True(Valid(input));
    }

    [Fact]
    public void Method_case_code_must_exist()
    {
        var input = Input();
        input.ContentPayload = new() { SubmissionMethods = [new() { MethodName = "Trực tuyến", CaseCode = "KHONG_CO" }] };
        Assert.False(Valid(input));
    }

    [Fact]
    public void Additional_sections_are_limited()
    {
        var input = Input();
        input.ContentPayload = new() { AdditionalSections = Enumerable.Range(0, 50).Select(i => new AdditionalSection { Title = $"Mục {i}" }).ToList() };
        Assert.True(Valid(input));
        input.ContentPayload.AdditionalSections.Add(new() { Title = "Mục 51" });
        Assert.False(Valid(input));
        input.ContentPayload.AdditionalSections = [new() { Title = " ", Content = "x" }];
        Assert.False(Valid(input));
    }

    // PROC-02b (B3): chỉ báo lỗi khi body THỰC SỰ gửi documentCopyType/quantity mâu thuẫn.
    [Theory]
    [InlineData("""{"checklistId":"A","itemName":"x","copyRequirements":[{"documentCopyType":"ORIGINAL","quantity":1}],"documentCopyType":"CERTIFIED_COPY"}""", false)]
    [InlineData("""{"checklistId":"A","itemName":"x","copyRequirements":[{"documentCopyType":"ORIGINAL","quantity":1}],"quantity":2}""", false)]
    [InlineData("""{"checklistId":"A","itemName":"x","copyRequirements":[{"documentCopyType":"ORIGINAL","quantity":1},{"documentCopyType":"REGULAR_COPY","quantity":1}]}""", true)]
    [InlineData("""{"checklistId":"A","itemName":"x","copyRequirements":[{"documentCopyType":"REGULAR_COPY","quantity":3}]}""", true)]
    [InlineData("""{"checklistId":"A","itemName":"x","copyRequirements":[{"documentCopyType":"ORIGINAL","quantity":1}],"documentCopyType":"ORIGINAL","quantity":1}""", true)]
    [InlineData("""{"checklistId":"A","itemName":"x","copyRequirements":[{"documentCopyType":"ORIGINAL","quantity":1},{"documentCopyType":"REGULAR_COPY","quantity":1}],"documentCopyType":null,"quantity":2}""", true)]
    public void Copy_requirements_conflict_is_reported(string json, bool valid)
    {
        var input = Input();
        input.ChecklistSchema = [JsonSerializer.Deserialize<ChecklistItemSchema>(json, Web)!];
        Assert.Equal(valid, Valid(input));
    }
}
