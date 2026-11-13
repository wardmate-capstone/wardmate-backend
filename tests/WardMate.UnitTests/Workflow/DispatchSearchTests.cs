using WardMate.Services.ApplicationWorkflow.Application;
using WardMate.Services.ApplicationWorkflow.Infrastructure;

namespace WardMate.UnitTests.Workflow;

// DISPATCH-SEARCH-01: tham số search/sort/status của danh sách điều phối. Câu lệnh ILIKE + unaccent cần PostgreSQL nên
// test kiểm phần xác định ở BE: kiểm tra tham số và cách tách/escape từ khóa.
public sealed class DispatchSearchTests
{
    [Fact]
    public void Default_query_is_valid_and_keeps_old_behaviour()
    {
        Assert.Null(DispatchService.InvalidQuery(new DispatchListQuery()));
        Assert.Empty(DispatchService.SearchPatterns(null));
        Assert.Empty(DispatchService.SearchPatterns("   "));
    }

    [Theory]
    [InlineData("createdAt_asc", null)]
    [InlineData("createdAt_desc", null)]
    [InlineData("newest", "sort")]
    [InlineData("CREATEDAT_DESC", "sort")]
    public void Sort_accepts_only_two_values(string sort, string? error)
    {
        var result = DispatchService.InvalidQuery(new DispatchListQuery(Sort: sort));
        if (error is null) Assert.Null(result); else Assert.Contains(error, result);
    }

    [Fact]
    public void Status_and_search_length_are_validated()
    {
        Assert.Null(DispatchService.InvalidQuery(new DispatchListQuery(Status: "under_review")));
        Assert.Contains("status", DispatchService.InvalidQuery(new DispatchListQuery(Status: "DONE")));
        Assert.Null(DispatchService.InvalidQuery(new DispatchListQuery(Search: new string('a', 200))));
        Assert.Contains("200", DispatchService.InvalidQuery(new DispatchListQuery(Search: new string('a', 201))));
    }

    [Fact]
    public void Each_word_becomes_an_and_pattern_with_wildcards_escaped()
    {
        Assert.Equal(["%khai%", "%tu%"], DispatchService.SearchPatterns("  khai   tu "));
        Assert.Equal(["%50\\%%", "%a\\_b%", "%c\\\\d%"], DispatchService.SearchPatterns("50% a_b c\\d"));
        Assert.Equal(["%HS-20261112%"], DispatchService.SearchPatterns("HS-20261112 hs-20261112"));
        Assert.Equal(10, DispatchService.SearchPatterns(string.Join(' ', Enumerable.Range(1, 15))).Length);
    }
}
