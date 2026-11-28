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

    private static async Task<WorkflowResult<ApplicationDto>> Create(ProcedureChecklist[] items, ProcedureForm[] forms,
        string[]? cases = null, string? caseCode = null)
    {
        var procedureId = Guid.NewGuid();
        var content = new ProcedureContent((cases ?? []).Select(x => new ProcedureCase(x, x == "TH-NONAME" ? null : "  Trường hợp " + x + " ")).ToArray());
        var snapshot = new ProcedureSnapshot(procedureId, "Thủ tục thử", true, content, items, forms, 1, "Đất đai");
        var handler = new WorkflowHandlers(new Store(), new Catalog(snapshot), TimeProvider.System, new Directory(), new ReviewDeadlinePolicy());
        return await handler.Handle(new CreateApplicationCommand(Guid.NewGuid(),
            new CreateApplicationInput(procedureId, JsonSerializer.SerializeToElement(new { }), caseCode, "W01")), default);
    }

    // WF-CASE-01: giấy tờ chung (không gắn trường hợp) + giấy tờ riêng của từng trường hợp.
    // Ví dụ: 5 giấy tờ bắt buộc cho mọi người; trường hợp A thêm 2, trường hợp B thêm 3.
    private static readonly ProcedureChecklist[] CommonAndCases =
    [
        .. Enumerable.Range(1, 5).Select(i => Item($"C{i}")),
        Item("A1") with { CaseCode = "TH-A" }, Item("A2", mandatory: false) with { CaseCode = "TH-A" },
        Item("B1") with { CaseCode = "TH-B" }, Item("B2") with { CaseCode = "TH-B" }, Item("B3") with { CaseCode = "TH-B" },
    ];

    [Theory]
    [InlineData("TH-A", new[] { "C1", "C2", "C3", "C4", "C5", "A1", "A2" })]
    [InlineData("TH-B", new[] { "C1", "C2", "C3", "C4", "C5", "B1", "B2", "B3" })]
    public async Task Chosen_case_gets_common_items_plus_its_own_only(string caseCode, string[] expected)
    {
        var result = await Create(CommonAndCases, [], ["TH-A", "TH-B"], caseCode);
        Assert.Null(result.Error);
        Assert.Equal(caseCode, result.Value!.CaseCode);
        Assert.Equal("Trường hợp " + caseCode, result.Value.CaseName);
        Assert.Equal(expected, result.Value.Checklists.Select(x => x.Code));
        Assert.All(result.Value.Checklists.Where(x => x.Code.StartsWith('C')), x => Assert.True(x.IsRequired));
        if (caseCode == "TH-A") Assert.False(result.Value.Checklists.Single(x => x.Code == "A2").IsRequired);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("TH-C")]
    [InlineData("th-a")]
    public async Task Several_cases_require_a_valid_choice(string? caseCode)
    {
        var result = await Create(CommonAndCases, [], ["TH-A", "TH-B"], caseCode);
        Assert.Equal("application.invalid_case", result.Error!.Code);
    }

    [Fact]
    public async Task Single_case_is_chosen_automatically()
    {
        var items = CommonAndCases.Where(x => x.CaseCode is null or "TH-A").ToArray();
        var result = await Create(items, [], ["TH-A"]);
        Assert.Equal("TH-A", result.Value!.CaseCode);
        Assert.Equal(7, result.Value.Checklists.Length);
    }

    // WF-CASE-02: tên trường hợp được chụp vào hồ sơ; thủ tục không đặt tên thì null, hồ sơ không có trường hợp thì null.
    [Fact]
    public async Task Case_name_is_snapshotted_or_null()
    {
        var unnamed = await Create([Item("C1")], [], ["TH-NONAME", "TH-B"], "TH-NONAME");
        Assert.Equal("TH-NONAME", unnamed.Value!.CaseCode);
        Assert.Null(unnamed.Value.CaseName);
        var none = await Create([Item("C1")], []);
        Assert.Null(none.Value!.CaseCode);
        Assert.Null(none.Value.CaseName);
    }

    [Fact]
    public async Task Case_forms_follow_the_chosen_case()
    {
        Guid ta = Guid.NewGuid(), tb = Guid.NewGuid();
        var forms = new[] { Form("TK-CHUNG", T25), Form("TK-A", ta) with { CaseCode = "TH-A" }, Form("TK-B", tb) with { CaseCode = "TH-B" } };
        var rows = (await Create([Item("C1")], forms, ["TH-A", "TH-B"], "TH-B")).Value!.Checklists;
        Assert.Equal(new[] { "C1", "TK-CHUNG", "TK-B" }, rows.Select(x => x.Code));
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
