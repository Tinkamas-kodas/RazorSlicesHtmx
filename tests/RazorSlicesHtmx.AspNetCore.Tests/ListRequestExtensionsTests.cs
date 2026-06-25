using System.Linq.Expressions;
using RazorSlicesHtmx.AspNetCore.Models;

namespace RazorSlicesHtmx.AspNetCore.Tests;

public class ListRequestExtensionsTests
{
    [Fact]
    public void NextSortDirection_returns_Asc_for_different_column()
    {
        var request = CreateRequest(sortBy: "name", sortDirection: SortDirection.Asc);

        var next = request.NextSortDirection("code");

        Assert.Equal(SortDirection.Asc, next);
    }

    [Fact]
    public void NextSortDirection_toggles_Asc_to_Desc_for_same_column()
    {
        var request = CreateRequest(sortBy: "code", sortDirection: SortDirection.Asc);

        var next = request.NextSortDirection("code");

        Assert.Equal(SortDirection.Desc, next);
    }

    [Fact]
    public void NextSortDirection_toggles_Desc_to_Asc_for_same_column()
    {
        var request = CreateRequest(sortBy: "code", sortDirection: SortDirection.Desc);

        var next = request.NextSortDirection("code");

        Assert.Equal(SortDirection.Asc, next);
    }

    [Fact]
    public void NextSortDirection_returns_Asc_when_SortBy_is_null()
    {
        var request = CreateRequest(sortBy: null, sortDirection: SortDirection.Asc);

        var next = request.NextSortDirection("code");

        Assert.Equal(SortDirection.Asc, next);
    }

    [Fact]
    public void NextSortDirection_is_case_insensitive()
    {
        var request = CreateRequest(sortBy: "Code", sortDirection: SortDirection.Asc);

        var next = request.NextSortDirection("code");

        Assert.Equal(SortDirection.Desc, next);
    }

    [Fact]
    public void NormalizePaging_returns_defaults_for_invalid_page()
    {
        var request = CreateRequest(page: -1, pageSize: 10);

        var (page, pageSize) = request.NormalizePaging(defaultPage: 1, defaultPageSize: 10);

        Assert.Equal(1, page);
        Assert.Equal(10, pageSize);
    }

    [Fact]
    public void NormalizePaging_returns_defaults_for_zero_page_size()
    {
        var request = CreateRequest(page: 1, pageSize: 0);

        var (page, pageSize) = request.NormalizePaging(defaultPage: 1, defaultPageSize: 10);

        Assert.Equal(1, page);
        Assert.Equal(10, pageSize);
    }

    [Fact]
    public void NormalizePaging_caps_page_size_at_max()
    {
        var request = CreateRequest(page: 1, pageSize: 500);

        var (_, pageSize) = request.NormalizePaging(defaultPage: 1, defaultPageSize: 10, maxPageSize: 100);

        Assert.Equal(100, pageSize);
    }

    [Fact]
    public void NormalizePaging_preserves_valid_values()
    {
        var request = CreateRequest(page: 3, pageSize: 25);

        var (page, pageSize) = request.NormalizePaging(defaultPage: 1, defaultPageSize: 10, maxPageSize: 100);

        Assert.Equal(3, page);
        Assert.Equal(25, pageSize);
    }

    [Fact]
    public void SetPaging_updates_request_values()
    {
        var request = CreateRequest(page: 1, pageSize: 10);

        request.SetPaging(5, 20);

        Assert.Equal(5, request.Page);
        Assert.Equal(20, request.PageSize);
    }

    private static TestListRequest CreateRequest(
        string? sortBy = "code",
        SortDirection sortDirection = SortDirection.Asc,
        int page = 1,
        int pageSize = 10) => new()
    {
        SortBy = sortBy,
        SortDirection = sortDirection,
        Page = page,
        PageSize = pageSize
    };
}

public sealed class TestRow
{
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public int Value { get; set; }
}

public sealed class TestListRequest : ListRequest<TestRow>
{
    public override IReadOnlyDictionary<string, Expression<Func<TestRow, object?>>> SortMap { get; } =
        new Dictionary<string, Expression<Func<TestRow, object?>>>(StringComparer.OrdinalIgnoreCase)
        {
            ["code"] = row => row.Code,
            ["name"] = row => row.Name,
            ["value"] = row => row.Value
        };
}
