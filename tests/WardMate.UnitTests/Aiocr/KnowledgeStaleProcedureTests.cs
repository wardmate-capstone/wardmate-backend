using WardMate.Services.AIOCR.Application;
using WardMate.Services.AIOCR.Infrastructure.Knowledge;

namespace WardMate.UnitTests.Aiocr;

// KB-STALE-01: chỉ danh sách Catalog đầy đủ mới chứng minh thủ tục nào đã ngừng công khai.
public sealed class KnowledgeStaleProcedureTests
{
    private static readonly Guid Published = Guid.NewGuid(), Hidden = Guid.NewGuid();

    [Fact]
    public void Sources_of_procedures_missing_from_a_complete_listing_are_stale()
    {
        var stale = KnowledgeBaseService.StaleProcedureIds([Published, Hidden, Hidden], new CatalogListing([Published], Complete: true));
        Assert.Equal([Hidden], stale);
    }

    [Fact]
    public void Partial_listing_never_marks_anything_stale()
    {
        Assert.Empty(KnowledgeBaseService.StaleProcedureIds([Published, Hidden], new CatalogListing([Published], Complete: false)));
    }

    [Fact]
    public void Empty_complete_listing_marks_every_synced_procedure_stale()
    {
        Assert.Equal(2, KnowledgeBaseService.StaleProcedureIds([Published, Hidden], new CatalogListing([], Complete: true)).Length);
    }
}
