namespace WardMate.SharedKernel.Common;

/// <summary>
/// Generic paged result returned by query handlers that support pagination.
/// </summary>
/// <typeparam name="T">Type of the items in this page.</typeparam>
public sealed class PagedResult<T>
{
    public IReadOnlyList<T> Items { get; init; } = [];
    public int Page { get; init; }
    public int PageSize { get; init; }
    public long TotalCount { get; init; }

    public int TotalPages => PageSize > 0 ? (int)Math.Ceiling((double)TotalCount / PageSize) : 0;
    public bool HasNextPage => Page < TotalPages;
    public bool HasPreviousPage => Page > 1;

    public static PagedResult<T> Create(IReadOnlyList<T> items, long totalCount, int page, int pageSize)
        => new() { Items = items, TotalCount = totalCount, Page = page, PageSize = pageSize };

    public static PagedResult<T> Empty(int page = 1, int pageSize = 20)
        => new() { Items = [], TotalCount = 0, Page = page, PageSize = pageSize };
}
