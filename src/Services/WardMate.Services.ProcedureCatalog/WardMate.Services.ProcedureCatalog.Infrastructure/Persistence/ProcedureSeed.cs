using Microsoft.EntityFrameworkCore;
using WardMate.Services.ProcedureCatalog.Domain.Entities;
using WardMate.Services.ProcedureCatalog.Domain.JsonModels;

namespace WardMate.Services.ProcedureCatalog.Infrastructure.Persistence;

public static class ProcedureSeed
{
    public static readonly Guid SampleProcedureId = Guid.Parse("07000000-0000-0000-0000-000000000001");
    public static void Configure(ModelBuilder builder)
    {
        builder.Entity<ProcedureCategory>().HasData(
            new ProcedureCategory { Id = 1, CategoryName = "Hộ tịch", Description = "Các thủ tục hộ tịch" },
            new ProcedureCategory { Id = 2, CategoryName = "Đất đai", Description = "Các thủ tục đất đai" },
            new ProcedureCategory { Id = 3, CategoryName = "Quản lý công sản", Description = "Các thủ tục quản lý công sản" });
        builder.Entity<Procedure>().HasData(CreateSample());
    }

    // Synthetic demonstration, not an authoritative published administrative procedure.
    public static Procedure CreateSample() => new()
    {
        Id = SampleProcedureId, CategoryId = 1, ProcedureCode = "DEMO-KET-HON",
        Title = "Đăng ký kết hôn (dữ liệu minh họa)", IssuingAuthority = "Cơ quan ban hành minh họa",
        ExecutingAgency = "Ủy ban nhân dân cấp xã", CreatedAt = new DateTime(2026, 9, 30, 0, 0, 0, DateTimeKind.Utc),
        UpdatedAt = new DateTime(2026, 9, 30, 0, 0, 0, DateTimeKind.Utc),
        ContentPayload = new ProcedureContentPayload
        {
            DecisionNumber = "DEMO-QD-01", ReceivingAddress = "Bộ phận tiếp nhận và trả kết quả (minh họa)",
            SubmissionMethods = [new() { MethodName = "Trực tiếp", FeeAmount = 0, FeeUnit = "VND", EstimatedDays = 1, Note = "Dữ liệu thử nghiệm ánh xạ JSONB" }],
            LegalReferences = [new() { DocumentNumber = "DEMO-VB-01", DocumentName = "Văn bản minh họa, không dùng làm căn cứ pháp lý", IssueDate = new DateOnly(2026, 9, 1), Authority = "Cơ quan minh họa" }],
            Results = ["Giấy chứng nhận kết hôn (minh họa)"],
            Cases = [new()
            {
                CaseCode = "THONG_THUONG", CaseName = "Trường hợp thông thường (minh họa)",
                Steps = [new() { StepOrder = 1, StepName = "Tiếp nhận", Executor = "Cán bộ tiếp nhận", ActionDetails = "Kiểm tra thành phần hồ sơ mẫu" },
                    new() { StepOrder = 2, StepName = "Trả kết quả", Executor = "Cán bộ hộ tịch", ActionDetails = "Trả kết quả theo quy trình minh họa" }]
            }]
        },
        ChecklistSchema = [new() { ChecklistId = "TO_KHAI", CaseCode = "THONG_THUONG", SubmissionType = "NOP", ItemName = "Tờ khai đăng ký kết hôn (mẫu)", DocumentCopyType = "ORIGINAL" },
            new() { ChecklistId = "DINH_DANH", SubmissionType = "XUAT_TRINH", ItemName = "Giấy tờ định danh (mẫu)", DocumentCopyType = "ORIGINAL", ConditionNote = "Đối chiếu thông tin minh họa" }],
        FormDefinitions = [new() { FormCode = "DEMO-TK-01", FormName = "Tờ khai điện tử minh họa", FormType = "ONLINE_INTERACTIVE", CaseCode = "THONG_THUONG" },
            new() { FormCode = "DEMO-DOCX-01", FormName = "Mẫu DOCX minh họa", FormType = "DOCX_TEMPLATE", IsMandatory = false }]
    };
}
