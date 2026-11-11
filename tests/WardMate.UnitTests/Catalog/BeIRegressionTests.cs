using FluentValidation;
using WardMate.Services.ProcedureCatalog.Application.Drafts;
using WardMate.Services.ProcedureCatalog.Application.DTOs;
using WardMate.Services.ProcedureCatalog.Application.Interfaces;
using WardMate.Services.ProcedureCatalog.Application.Management;
using WardMate.Services.ProcedureCatalog.Domain.Entities;
using WardMate.Services.ProcedureCatalog.Domain.JsonModels;

namespace WardMate.UnitTests.Catalog;

// BE-I: (1) gắn PDF cho thủ tục trả 500 vì Category chưa nạp; (2) PUT checklist bỏ qua formDefinitions của thủ tục.
public sealed class BeIRegressionTests
{
    private static Procedure Procedure() => new()
    {
        Id = Guid.NewGuid(), CategoryId = 7, ProcedureCode = "P1", Title = "Thủ tục nhập tay", IsActive = false,
        UpdatedAt = new DateTime(2026, 11, 10, 3, 0, 0, DateTimeKind.Utc),
        ContentPayload = new() { DecisionNumber = "QĐ-1" },
        ChecklistSchema = [new() { ChecklistId = "GT_01", ItemName = "Đơn", FormCodes = ["M25"] }],
        FormDefinitions = [new() { FormCode = "M25", FormName = "Mẫu số 25", FormType = "DOCX_TEMPLATE" }]
        // Category cố ý null: giống LockProcedure (SQL FOR UPDATE) không Include.
    };

    [Fact]
    public async Task Attach_source_to_hidden_procedure_returns_detail()
    {
        var store = new Store(Procedure());
        var service = new ProcedureDraftService(new Drafts(), new Blobs(), null!, store, new(false));
        var result = await service.AttachSource(store.Procedure.Id, new MemoryStream("%PDF-1.7 test"u8.ToArray()), "chi-tiet.pdf", "manager", default);
        Assert.Null(result.Error);
        Assert.Equal("Đất đai", result.Value!.CategoryName);
        Assert.Equal("chi-tiet.pdf", result.Value.PdfFileName);
        Assert.Single(store.Versions);
    }

    [Fact]
    public async Task Attach_source_blob_failure_is_503()
    {
        var store = new Store(Procedure());
        var service = new ProcedureDraftService(new Drafts(), new Blobs(fail: true), null!, store, new(false));
        var result = await service.AttachSource(store.Procedure.Id, new MemoryStream("%PDF-1.7 test"u8.ToArray()), "a.pdf", "manager", default);
        Assert.Equal(("draft.storage_unavailable", 503), (result.Error!.Code, result.Error.Status));
    }

    [Theory]
    [InlineData("M25", null, true)]
    [InlineData("M99", null, false)]
    public async Task Checklist_update_validates_form_codes_against_saved_forms(string code, string? _, bool valid)
    {
        var store = new Store(Procedure());
        var handler = new UpdateProcedureChecklistHandler(store, new ProcedureInputValidator());
        var items = new List<ChecklistItemSchema> { new() { ChecklistId = "GT_01", ItemName = "Đơn", Quantity = 2, FormCodes = [code] } };
        var result = await handler.Handle(new UpdateProcedureChecklistCommand(store.Procedure.Id,
            new ChecklistUpdateInput(store.Procedure.UpdatedAt, "QĐ-2", new DateOnly(2026, 11, 11), items)), default);
        Assert.Equal(valid, result.Error is null);
    }

    private sealed class Store(Procedure procedure) : IProcedureManagementStore
    {
        public Procedure Procedure { get; } = procedure;
        public List<ProcedureVersion> Versions { get; } = [];
        public Task<ProcedureCategory?> FindCategory(int id, CancellationToken ct) =>
            Task.FromResult<ProcedureCategory?>(new ProcedureCategory { Id = id, CategoryName = "Đất đai" });
        public Task<Procedure?> LockProcedure(Guid id, CancellationToken ct) => Task.FromResult<Procedure?>(id == Procedure.Id ? Procedure : null);
        public Task<bool> Exists(Guid id, CancellationToken ct) => Task.FromResult(id == Procedure.Id);
        public Task<int> NextVersion(Guid id, CancellationToken ct) => Task.FromResult(Versions.Count + 1);
        public void AddVersion(ProcedureVersion version) => Versions.Add(version);
        public Task<ProcedureResult<T>> Transaction<T>(Func<Task<ProcedureResult<T>>> action, CancellationToken ct) => action();
        public Task<bool> CodeExists(string code, Guid? excludingId, CancellationToken ct) => throw new NotSupportedException();
        public Task<Procedure?> LockProcedureByCode(string code, CancellationToken ct) => throw new NotSupportedException();
        public void Add(Procedure p) => throw new NotSupportedException();
        public Task<IReadOnlyList<ProcedureVersion>> ListVersions(Guid id, CancellationToken ct) => throw new NotSupportedException();
    }

    private sealed class Blobs(bool fail = false) : IDraftFileStorage
    {
        public bool IsConfigured => true;
        public Task<string> Upload(Stream pdf, string blobName, CancellationToken ct) =>
            fail ? throw new InvalidOperationException("blob down") : Task.FromResult("https://blob.test/" + blobName);
        public Task<string> ReadUrl(string blobName, CancellationToken ct) => throw new NotSupportedException();
    }

    private sealed class Drafts : IDraftPersistence
    {
        public void Add(ProcedureDraft draft) { }
        public Task<ProcedureDraft?> Find(Guid id, CancellationToken ct) => throw new NotSupportedException();
        public Task<IReadOnlyList<DraftSummaryDto>> List(int page, int pageSize, CancellationToken ct) => throw new NotSupportedException();
        public Task Insert(ProcedureDraft draft, CancellationToken ct) => throw new NotSupportedException();
        public Task<bool> Save(CancellationToken ct) => throw new NotSupportedException();
        public Task<Guid?> FindPublishedDraftId(Guid procedureId, bool includeInactive, CancellationToken ct) => throw new NotSupportedException();
        public Task<ProcedureResult<ProcedureDetailDto>> PublishLocked(Guid id,
            Func<ProcedureDraft?, Task<ProcedureResult<ProcedureDetailDto>>> action, CancellationToken ct) => throw new NotSupportedException();
    }
}
