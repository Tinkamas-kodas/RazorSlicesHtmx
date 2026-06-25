using RazorSlicesHtmx.AspNetCore.Models;

namespace RazorSlicesHtmx.AspNetCore.Tests;

public class PageableListExtensionsTests
{
    private static IQueryable<TestRow> CreateTestData(int count) =>
        Enumerable.Range(1, count)
            .Select(i => new TestRow { Code = $"C{i:D3}", Name = $"Name{i}", Value = i })
            .AsQueryable();

    [Fact]
    public void ToPagedList_returns_first_page_with_correct_counts()
    {
        var source = CreateTestData(25);
        var request = new TestListRequest { Page = 1, PageSize = 10 };

        var result = source.ToPagedList(request);

        Assert.Equal(25, result.Total);
        Assert.Equal(10, result.Items.Count);
        Assert.Equal(3, result.TotalPages);
    }

    [Fact]
    public void ToPagedList_returns_partial_last_page()
    {
        var source = CreateTestData(25);
        var request = new TestListRequest { Page = 3, PageSize = 10 };

        var result = source.ToPagedList(request);

        Assert.Equal(25, result.Total);
        Assert.Equal(5, result.Items.Count);
    }

    [Fact]
    public void ToPagedList_clamps_page_beyond_total()
    {
        var source = CreateTestData(10);
        var request = new TestListRequest { Page = 99, PageSize = 10 };

        var result = source.ToPagedList(request);

        Assert.Equal(1, result.Request.Page);
        Assert.Equal(10, result.Items.Count);
    }

    [Fact]
    public void ToPagedList_sorts_ascending_by_default()
    {
        var source = CreateTestData(5);
        var request = new TestListRequest
        {
            Page = 1,
            PageSize = 10,
            SortBy = "code",
            SortDirection = SortDirection.Asc
        };

        var result = source.ToPagedList(request);

        Assert.Equal("C001", result.Items[0].Code);
        Assert.Equal("C005", result.Items[^1].Code);
    }

    [Fact]
    public void ToPagedList_sorts_descending()
    {
        var source = CreateTestData(5);
        var request = new TestListRequest
        {
            Page = 1,
            PageSize = 10,
            SortBy = "code",
            SortDirection = SortDirection.Desc
        };

        var result = source.ToPagedList(request);

        Assert.Equal("C005", result.Items[0].Code);
        Assert.Equal("C001", result.Items[^1].Code);
    }

    [Fact]
    public void ToPagedList_ignores_unknown_sort_column()
    {
        var source = CreateTestData(5);
        var request = new TestListRequest
        {
            Page = 1,
            PageSize = 10,
            SortBy = "nonexistent",
            SortDirection = SortDirection.Asc
        };

        var result = source.ToPagedList(request);

        Assert.Equal(5, result.Items.Count);
    }

    [Fact]
    public void ToPagedList_handles_empty_source()
    {
        var source = CreateTestData(0);
        var request = new TestListRequest { Page = 1, PageSize = 10 };

        var result = source.ToPagedList(request);

        Assert.Equal(0, result.Total);
        Assert.Empty(result.Items);
        Assert.Equal(1, result.TotalPages);
    }

    [Fact]
    public void ToPagedList_normalizes_negative_page_size()
    {
        var source = CreateTestData(5);
        var request = new TestListRequest { Page = 1, PageSize = -1 };

        var result = source.ToPagedList(request);

        Assert.Equal(5, result.Total);
        Assert.True(result.Items.Count > 0);
    }

    [Fact]
    public async Task ToPagedListAsync_returns_same_result_as_sync()
    {
        var source = CreateTestData(15);
        var syncRequest = new TestListRequest { Page = 2, PageSize = 5, SortBy = "value", SortDirection = SortDirection.Asc };
        var asyncRequest = new TestListRequest { Page = 2, PageSize = 5, SortBy = "value", SortDirection = SortDirection.Asc };

        var syncResult = source.ToPagedList(syncRequest);
        var asyncResult = await source.ToPagedListAsync(asyncRequest);

        Assert.Equal(syncResult.Total, asyncResult.Total);
        Assert.Equal(syncResult.Items.Count, asyncResult.Items.Count);
        Assert.Equal(syncResult.Items[0].Value, asyncResult.Items[0].Value);
    }
}
