using WardMate.Services.ProcedureCatalog.Application.Management;
using WardMate.Services.ProcedureCatalog.Domain.Entities;
using WardMate.Services.ProcedureCatalog.Domain.JsonModels;

namespace WardMate.UnitTests.Catalog;

// PROC-01c (B2), PROC-05b–f (BE-H): chuẩn hóa và suy ra trường cũ khi lưu.
public sealed class ProcedureInputApplyTests
{
    private static ProcedureInput Input() => new() { CategoryId = 1, ProcedureCode = "P1", Title = "Thủ tục thử" };

    private static Procedure Apply(ProcedureInput input)
    {
        var procedure = new Procedure();
        input.ApplyTo(procedure);
        return procedure;
    }

    [Fact]
    public void Update_without_pdf_keeps_current_source()
    {
        var procedure = new Procedure { OriginalPdfUrl = "https://blob.test/a.pdf", PdfFileName = "a.pdf" };
        Input().ApplyTo(procedure, keepSourceWhenMissing: true);
        Assert.Equal("https://blob.test/a.pdf", procedure.OriginalPdfUrl);
        Assert.Equal("a.pdf", procedure.PdfFileName);
    }

    [Fact]
    public void Resending_same_url_without_name_keeps_file_name()
    {
        var procedure = new Procedure { OriginalPdfUrl = "https://blob.test/a.pdf", PdfFileName = "a.pdf" };
        var input = Input(); input.OriginalPdfUrl = "https://blob.test/a.pdf";
        input.ApplyTo(procedure, keepSourceWhenMissing: true);
        Assert.Equal("a.pdf", procedure.PdfFileName);
    }

    [Fact]
    public void Create_and_rollback_assign_source_exactly()
    {
        var procedure = new Procedure { OriginalPdfUrl = "https://blob.test/a.pdf", PdfFileName = "a.pdf" };
        Input().ApplyTo(procedure);
        Assert.Null(procedure.OriginalPdfUrl);
        Assert.Null(procedure.PdfFileName);
    }

    [Fact]
    public void Several_fees_derive_fee_amount_and_summary()
    {
        var input = Input();
        input.ContentPayload = new()
        {
            SubmissionMethods = [new()
            {
                MethodName = "Trực tiếp",
                Fees = [new() { Kind = "LE_PHI", Amount = 0 }, new() { Kind = "PHI", Amount = 500_000 }, new() { Kind = "PHI", Percent = 0.5m, Unit = null }],
                ProcessingTime = new() { Value = 57, Unit = "WORKING_DAY" }
            }]
        };
        var procedure = Apply(input);
        var method = procedure.ContentPayload.SubmissionMethods[0];
        Assert.Equal(0, method.FeeAmount);
        Assert.Equal(57, method.EstimatedDays);
        Assert.Equal("VND", method.Fees![2].Unit);
        Assert.Equal("0 – 500.000 VND; 0,5%", procedure.FeeSummary);
        Assert.Equal("57 ngày làm việc", procedure.ProcessingTimeSummary);
    }

    [Fact]
    public void Fallback_text_wins_in_summaries()
    {
        var input = Input();
        input.ContentPayload = new()
        {
            SubmissionMethods = [new() { MethodName = "Trực tiếp", FeeText = "Theo quy định của Bộ Tài chính", ProcessingTime = new() { Text = "Không quá 15 phút" } }]
        };
        var procedure = Apply(input);
        Assert.Equal("Theo quy định của Bộ Tài chính", procedure.FeeSummary);
        Assert.Equal("Không quá 15 phút", procedure.ProcessingTimeSummary);
        Assert.Null(procedure.ContentPayload.SubmissionMethods[0].FeeAmount);
        Assert.Null(procedure.ContentPayload.SubmissionMethods[0].EstimatedDays);
    }

    [Fact]
    public void Minutes_do_not_become_estimated_days()
    {
        var input = Input();
        input.ContentPayload = new() { SubmissionMethods = [new() { MethodName = "Trực tiếp", ProcessingTime = new() { Value = 15, Unit = "MINUTE" } }] };
        var procedure = Apply(input);
        Assert.Null(procedure.ContentPayload.SubmissionMethods[0].EstimatedDays);
        Assert.Equal("15 phút", procedure.ProcessingTimeSummary);
    }

    [Fact]
    public void Legacy_summaries_still_work()
    {
        var input = Input();
        input.ContentPayload = new()
        {
            SubmissionMethods = [new() { MethodName = "Trực tiếp", FeeAmount = 0, EstimatedDays = 0 }, new() { MethodName = "Trực tuyến", FeeAmount = 0, EstimatedDays = 0 }]
        };
        var procedure = Apply(input);
        Assert.Equal("Miễn phí", procedure.FeeSummary);
        Assert.Equal("Trong ngày", procedure.ProcessingTimeSummary);

        input.ContentPayload.SubmissionMethods = [new() { MethodName = "A", FeeAmount = 10_000, EstimatedDays = 3 }, new() { MethodName = "B", FeeAmount = 20_000, EstimatedDays = 7 }];
        procedure = Apply(input);
        Assert.Equal("10.000 – 20.000 VND", procedure.FeeSummary);
        Assert.Equal("3 – 7 ngày", procedure.ProcessingTimeSummary);
    }

    [Fact]
    public void Lists_fill_legacy_fields_when_empty()
    {
        var input = Input();
        input.ContentPayload = new()
        {
            Levels = ["Cấp Tỉnh", " ", "Cấp Xã"],
            CompetentAuthorities = ["UBND tỉnh"],
            ExecutingAgencies = ["Văn phòng đăng ký đất đai", "Chi nhánh"],
            ReceivingAddresses = ["Bộ phận Một cửa"],
            ResultDetails = [new() { Name = "Giấy chứng nhận", Code = "KQ.H17.000312" }],
            Keywords = ["đất đai", "sổ đỏ"]
        };
        var procedure = Apply(input);
        Assert.Equal("Cấp Tỉnh, Cấp Xã", procedure.LevelOfImplementation);
        Assert.Equal("UBND tỉnh", procedure.IssuingAuthority);
        Assert.Equal("Văn phòng đăng ký đất đai; Chi nhánh", procedure.ExecutingAgency);
        Assert.Equal("Bộ phận Một cửa", procedure.ContentPayload.ReceivingAddress);
        Assert.Equal(new[] { "Giấy chứng nhận" }, procedure.ContentPayload.Results);
        Assert.Equal("đất đai | sổ đỏ", procedure.SearchKeywords);
    }

    [Fact]
    public void Explicit_legacy_fields_are_kept()
    {
        var input = Input();
        input.LevelOfImplementation = "Cấp Xã";
        input.ContentPayload = new() { Levels = ["Cấp Tỉnh"] };
        Assert.Equal("Cấp Xã", Apply(input).LevelOfImplementation);
    }

    [Fact]
    public void Blank_new_fields_become_null_and_steps_are_numbered()
    {
        var input = Input();
        input.ContentPayload = new()
        {
            StepsText = "   ", Requirements = "\n", Keywords = [], AdditionalSections = [],
            CommonSteps = [new() { ActionDetails = "Nộp" }, new() { StepName = "Trả kết quả", StepOrder = 0 }]
        };
        var content = Apply(input).ContentPayload;
        Assert.Null(content.StepsText);
        Assert.Null(content.Requirements);
        Assert.Null(content.Keywords);
        Assert.Null(content.AdditionalSections);
        Assert.Equal(new[] { 1, 2 }, content.CommonSteps!.Select(x => x.StepOrder));
    }

    [Fact]
    public void Checklist_fallback_fields_are_normalized()
    {
        var item = new ChecklistItemSchema { ChecklistId = "GT01", ItemName = "x", DocumentCopyType = "OTHER", QuantityText = "  ", Note = " Lưu ý ", FormCodes = [] };
        var normalized = ProcedureInput.NormalizedChecklist([item])![0];
        Assert.Null(normalized.QuantityText);
        Assert.Equal("Lưu ý", normalized.Note);
        Assert.Null(normalized.FormCodes);
        Assert.Equal("OTHER", Assert.Single(normalized.CopyRequirements!).DocumentCopyType);
    }
}
