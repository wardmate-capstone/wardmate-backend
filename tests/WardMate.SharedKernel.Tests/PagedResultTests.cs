using WardMate.SharedKernel.Common;
using Xunit;

namespace WardMate.SharedKernel.Tests;

public sealed class PagedResultTests
{
    [Fact]
    public void Create_PopulatesAllProperties()
    {
        var items = new[] { "a", "b", "c" };
        var paged = PagedResult<string>.Create(items, 10, page: 1, pageSize: 3);

        Assert.Equal(3, paged.Items.Count);
        Assert.Equal(10, paged.TotalCount);
        Assert.Equal(1, paged.Page);
        Assert.Equal(3, paged.PageSize);
        Assert.Equal(4, paged.TotalPages); // ceil(10 / 3) = 4
        Assert.True(paged.HasNextPage);
        Assert.False(paged.HasPreviousPage);
    }

    [Fact]
    public void MiddlePage_HasBothPreviousAndNext()
    {
        var paged = PagedResult<int>.Create(new[] { 4, 5, 6 }, 9, page: 2, pageSize: 3);

        Assert.Equal(3, paged.TotalPages);
        Assert.True(paged.HasPreviousPage);
        Assert.True(paged.HasNextPage);
    }

    [Fact]
    public void LastPage_HasPreviousPageButNoNextPage()
    {
        var paged = PagedResult<int>.Create(new[] { 7, 8, 9 }, 9, page: 3, pageSize: 3);

        Assert.Equal(3, paged.TotalPages);
        Assert.True(paged.HasPreviousPage);
        Assert.False(paged.HasNextPage);
    }

    [Fact]
    public void Empty_ReturnsZeroCountAndEmptyList()
    {
        var paged = PagedResult<string>.Empty();

        Assert.Empty(paged.Items);
        Assert.Equal(0, paged.TotalCount);
        Assert.Equal(0, paged.TotalPages);
        Assert.False(paged.HasNextPage);
        Assert.False(paged.HasPreviousPage);
    }
}
