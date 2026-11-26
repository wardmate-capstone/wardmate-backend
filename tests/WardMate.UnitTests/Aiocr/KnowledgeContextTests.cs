using WardMate.Services.AIOCR.Application;

namespace WardMate.UnitTests.Aiocr;

// CHAT-CONTEXT-01: câu hỏi gộp nhiều ý vẫn nhận đủ mục Thành phần hồ sơ / phí / thời hạn của thủ tục khớp nhất.
public sealed class KnowledgeContextTests
{
    private static readonly Guid Doc = Guid.NewGuid(), Other = Guid.NewGuid();
    private const string Title = "Đăng ký thành lập hộ kinh doanh";

    private static KnowledgeHit Hit(Guid id, Guid doc, string? heading) =>
        new(id, doc, Title, "PROCEDURE", Guid.NewGuid(), heading, "...", 0.8);

    // Thứ tự giống ProcedureKnowledgeText: khối thông tin chung, cách thức, trình tự, thành phần hồ sơ (2 đoạn), căn cứ.
    private static readonly SectionChunk Info = new(Guid.NewGuid(), 0, Title, 600);
    private static readonly SectionChunk Methods = new(Guid.NewGuid(), 1, "Cách thức thực hiện", 500);
    private static readonly SectionChunk Steps = new(Guid.NewGuid(), 2, "Trình tự thực hiện", 1200);
    private static readonly SectionChunk Docs1 = new(Guid.NewGuid(), 3, "Thành phần hồ sơ", 1200);
    private static readonly SectionChunk Docs2 = new(Guid.NewGuid(), 4, "Thành phần hồ sơ", 900);
    private static readonly SectionChunk Legal = new(Guid.NewGuid(), 5, "Căn cứ pháp lý", 400);
    private static readonly SectionChunk[] All = [Info, Methods, Steps, Docs1, Docs2, Legal];

    [Theory]
    [InlineData("Đăng ký thành lập hộ kinh doanh cần giấy tờ gì, lệ phí bao nhiêu và mất bao lâu?", KnowledgeContext.Aspect.Documents | KnowledgeContext.Aspect.Fee | KnowledgeContext.Aspect.Time)]
    [InlineData("Hưởng trợ cấp thất nghiệp cần chuẩn bị những gì?", KnowledgeContext.Aspect.Documents)]
    [InlineData("bao lau thi co ket qua", KnowledgeContext.Aspect.Time)]
    [InlineData("Nộp ở đâu và các bước thế nào?", KnowledgeContext.Aspect.Steps)]
    [InlineData("Xin chào", KnowledgeContext.Aspect.None)]
    public void Aspects_follow_question_words(string question, KnowledgeContext.Aspect expected) =>
        Assert.Equal(expected, KnowledgeContext.Aspects(question));

    [Fact]
    public void Combined_question_adds_document_list_when_fee_and_time_chunks_filled_topk()
    {
        var hits = new[] { Hit(Info.ChunkId, Doc, Info.Heading), Hit(Methods.ChunkId, Doc, Methods.Heading), Hit(Guid.NewGuid(), Other, "Phí") };
        var extra = KnowledgeContext.Expand("cần giấy tờ gì, lệ phí bao nhiêu và mất bao lâu?", hits, All);
        Assert.Equal([Docs1.ChunkId, Docs2.ChunkId], extra);
    }

    [Fact]
    public void Section_already_touched_is_completed_so_first_items_are_not_lost()
    {
        var hits = new[] { Hit(Docs2.ChunkId, Doc, "Thành phần hồ sơ") };
        var extra = KnowledgeContext.Expand("trợ cấp thất nghiệp", hits, All);
        Assert.Equal([Docs1.ChunkId], extra);
    }

    [Fact]
    public void Fee_question_adds_info_block_and_methods_but_not_unrelated_sections()
    {
        var hits = new[] { Hit(Legal.ChunkId, Doc, Legal.Heading) };
        var extra = KnowledgeContext.Expand("lệ phí bao nhiêu", hits, All);
        Assert.Equal([Info.ChunkId, Methods.ChunkId], extra);
    }

    [Fact]
    public void Nothing_is_added_twice_and_limits_are_kept()
    {
        var hits = new[] { Hit(Docs1.ChunkId, Doc, "Thành phần hồ sơ"), Hit(Docs2.ChunkId, Doc, "Thành phần hồ sơ") };
        Assert.Empty(KnowledgeContext.Expand("cần giấy tờ gì", hits, All));

        var many = Enumerable.Range(0, 20).Select(i => new SectionChunk(Guid.NewGuid(), i + 10, "Thành phần hồ sơ", 1000)).ToArray();
        var extra = KnowledgeContext.Expand("cần giấy tờ gì", [Hit(Info.ChunkId, Doc, Info.Heading)], [Info, .. many]);
        Assert.True(extra.Length <= KnowledgeContext.MaxExtraChunks);
        Assert.True(extra.Length * 1000 <= KnowledgeContext.MaxExtraChars);
        Assert.Empty(KnowledgeContext.Expand("cần giấy tờ gì", [], All));
    }

    [Fact]
    public void Procedure_text_chunks_carry_the_document_list_heading()
    {
        var items = Enumerable.Range(1, 8).Select(i => new CatalogChecklistItem(null, null,
            $"Giấy tờ số {i} " + new string('x', 150), "ORIGINAL", 1, null, true)).ToArray();
        var p = new CatalogProcedure(Guid.NewGuid(), "1.001612", Title, null, null, null, null, null,
            "100.000 đồng", "3 ngày làm việc", true, null, items, null, DateTime.UtcNow);
        var chunks = KnowledgeChunker.Split(KnowledgeChunker.Normalize(ProcedureKnowledgeText.Build(p)));
        var docChunks = chunks.Where(c => c.Heading == "Thành phần hồ sơ").ToArray();
        Assert.True(docChunks.Length >= 2);
        Assert.Contains("Giấy tờ số 1", docChunks[0].Content);
        Assert.Contains("Giấy tờ số 8", string.Join('\n', docChunks.Select(c => c.Content)));
    }
}
