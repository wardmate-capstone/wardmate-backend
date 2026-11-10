using System.Text.Json;
using WardMate.Services.ApplicationWorkflow.Application;
using WardMate.Services.ApplicationWorkflow.Domain;

namespace WardMate.UnitTests.Workflow;

// WF-01a (BE-H): tên giấy tờ dài, loại bản OTHER, gắn mẫu theo formCodes không sinh dòng lặp.
public sealed class CreateApplicationTests
{
    private static readonly Guid T25 = Guid.NewGuid(), TLptb = Guid.NewGuid(), TAlt = Guid.NewGuid();

    private static ProcedureChecklist Item(string id, string? name = null, bool mandatory = true, string? copy = "ORIGINAL", params string[] formCodes) =>
        new(id, name ?? "Giấy tờ " + id, mandatory, null, "NOP", copy, 1, null, null, null, formCodes.Length == 0 ? null : formCodes);

    private static ProcedureForm Form(string code, Guid? template, bool mandatory = true) => new(template, null, code, "Mẫu " + code, mandatory);

    private static async Task<WorkflowResult<ApplicationDto>> Create(ProcedureChecklist[] items, ProcedureForm[] forms)
    {
        var procedureId = Guid.NewGuid();
        var snapshot = new ProcedureSnapshot(procedureId, "Thủ tục thử", true, new ProcedureContent([]), items, forms, 1, "Đất đai");
        var handler = new WorkflowHandlers(new Store(), new Catalog(snapshot), TimeProvider.System, new Directory(), new ReviewDeadlinePolicy());
        return await handler.Handle(new CreateApplicationCommand(Guid.NewGuid(),
            new CreateApplicationInput(procedureId, JsonSerializer.SerializeToElement(new { }), null, "W01")), default);
    }

    [Fact]
    public async Task Forms_linked_by_form_codes_do_not_create_duplicate_rows()
    {
        var result = await Create(
            [Item("GT01", formCodes: ["M25"]), Item("GT02"), Item("GT03", formCodes: ["01.LPTB"])],
            [Form("M25", T25), Form("01.LPTB", TLptb)]);
        var rows = result.Value!.Checklists;
        Assert.Equal(new[] { "GT01", "GT02", "GT03" }, rows.Select(x => x.Code));
        Assert.Equal("M25", Assert.Single(rows[0].Forms!).FormCode);
        Assert.Equal(T25, rows[0].FormTemplateId);
        Assert.Null(rows[1].Forms);
        Assert.Equal(TLptb, Assert.Single(rows[2].Forms!).FormTemplateId);
    }

    [Fact]
    public async Task Item_with_two_forms_accepts_either_template()
    {
        var result = await Create([Item("GT01", formCodes: ["M25", "ALT"])], [Form("M25", T25), Form("ALT", TAlt)]);
        var row = Assert.Single(result.Value!.Checklists);
        Assert.Equal(2, row.Forms!.Length);
        Assert.Equal(T25, row.FormTemplateId);
        Assert.Equal(new[] { T25, TAlt }.OrderBy(x => x), row.AcceptedTemplateIds().OrderBy(x => x));
    }

    [Fact]
    public async Task Linked_form_does_not_make_optional_item_required()
    {
        var result = await Create([Item("GT01", mandatory: false, formCodes: ["M25"])], [Form("M25", T25, mandatory: true)]);
        Assert.False(Assert.Single(result.Value!.Checklists).IsRequired);
    }

    [Fact]
    public async Task Unlinked_forms_keep_legacy_behaviour()
    {
        var result = await Create([Item("GT01"), Item("TK01")], [Form("TK01", T25), Form("F9", TAlt)]);
        var rows = result.Value!.Checklists;
        Assert.Equal(new[] { "GT01", "TK01", "F9" }, rows.Select(x => x.Code));
        Assert.Equal(T25, rows[1].FormTemplateId);
        Assert.Equal(TAlt, rows[2].FormTemplateId);
        Assert.Equal("TK01", Assert.Single(rows[1].Forms!).FormCode);
    }

    [Fact]
    public async Task Long_names_and_other_copy_type_are_accepted()
    {
        var result = await Create([Item("GT01", name: new string('x', 10_000), copy: "OTHER")], []);
        Assert.Null(result.Error);
        var row = Assert.Single(result.Value!.Checklists);
        Assert.Equal(10_000, row.Title.Length);
        Assert.Equal("OTHER", row.DocumentCopyType);
    }

    [Fact]
    public async Task Name_over_catalog_limit_is_invalid_schema()
    {
        var result = await Create([Item("GT01", name: new string('x', 10_001))], []);
        Assert.Equal("application.invalid_schema", result.Error!.Code);
    }

    [Fact]
    public async Task Quantity_text_and_note_are_snapshotted()
    {
        var item = Item("GT01") with { QuantityText = "01 bản chính và 01 bản sao", Note = "Kèm bản dịch" };
        var row = Assert.Single((await Create([item], [])).Value!.Checklists);
        Assert.Equal("01 bản chính và 01 bản sao", row.QuantityText);
        Assert.Equal("Kèm bản dịch", row.ItemNote);
    }

    private sealed class Store : IApplicationStore
    {
        public Task Add(ApplicationRecord application, CancellationToken ct) => Task.CompletedTask;
        public Task<string> NextCode(DateTime now, CancellationToken ct) => Task.FromResult("HS-TEST-001");
        public Task<ApplicationRecord?> Get(Guid userId, Guid applicationId, CancellationToken ct) => throw new NotSupportedException();
        public Task<ApplicationPage> List(Guid userId, int page, int pageSize, CancellationToken ct) => throw new NotSupportedException();
        public Task<WorkflowResult<ApplicationDto>> WithLock(Guid userId, Guid applicationId,
            Func<ApplicationRecord, Task<WorkflowResult<ApplicationDto>>> operation, CancellationToken ct) => throw new NotSupportedException();
    }

    private sealed class Catalog(ProcedureSnapshot snapshot) : IProcedureCatalogClient
    {
        public Task<WorkflowResult<ProcedureSnapshot>> Get(Guid procedureId, CancellationToken ct) =>
            Task.FromResult(WorkflowResult<ProcedureSnapshot>.Ok(snapshot));
    }

    private sealed class Directory : IWorkflowDirectory
    {
        public Task<bool> WardExists(string code, CancellationToken ct) => Task.FromResult(true);
        public Task<WorkflowAccess?> Access(string token, CancellationToken ct) => throw new NotSupportedException();
        public Task<OfficerTarget?> Officer(Guid id, string token, CancellationToken ct) => throw new NotSupportedException();
        public Task<string?> WardName(string code, CancellationToken ct) => throw new NotSupportedException();
    }
}
