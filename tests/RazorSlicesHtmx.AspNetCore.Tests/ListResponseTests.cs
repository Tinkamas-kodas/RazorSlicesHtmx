using RazorSlicesHtmx.AspNetCore.Models;

namespace RazorSlicesHtmx.AspNetCore.Tests;

public class ListResponseTests
{
    [Fact]
    public void TotalPages_calculates_correctly_for_exact_fit()
    {
        var response = CreateResponse(total: 20, page: 1, pageSize: 10, itemCount: 10);

        Assert.Equal(2, response.TotalPages);
    }

    [Fact]
    public void TotalPages_rounds_up_for_partial_page()
    {
        var response = CreateResponse(total: 21, page: 1, pageSize: 10, itemCount: 10);

        Assert.Equal(3, response.TotalPages);
    }

    [Fact]
    public void TotalPages_returns_1_for_zero_items()
    {
        var response = CreateResponse(total: 0, page: 1, pageSize: 10, itemCount: 0);

        Assert.Equal(1, response.TotalPages);
    }

    [Fact]
    public void TotalPages_returns_1_for_single_item()
    {
        var response = CreateResponse(total: 1, page: 1, pageSize: 10, itemCount: 1);

        Assert.Equal(1, response.TotalPages);
    }

    [Fact]
    public void CurrentPage_does_not_exceed_TotalPages()
    {
        var response = CreateResponse(total: 10, page: 5, pageSize: 10, itemCount: 0);

        Assert.Equal(1, response.CurrentPage);
    }

    [Fact]
    public void CurrentPage_returns_requested_page_when_valid()
    {
        var response = CreateResponse(total: 50, page: 3, pageSize: 10, itemCount: 10);

        Assert.Equal(3, response.CurrentPage);
    }

    [Fact]
    public void FromItem_and_ToItem_correct_for_first_page()
    {
        var response = CreateResponse(total: 25, page: 1, pageSize: 10, itemCount: 10);

        Assert.Equal(1, response.FromItem);
        Assert.Equal(10, response.ToItem);
    }

    [Fact]
    public void FromItem_and_ToItem_correct_for_middle_page()
    {
        var response = CreateResponse(total: 25, page: 2, pageSize: 10, itemCount: 10);

        Assert.Equal(11, response.FromItem);
        Assert.Equal(20, response.ToItem);
    }

    [Fact]
    public void FromItem_and_ToItem_correct_for_last_partial_page()
    {
        var response = CreateResponse(total: 25, page: 3, pageSize: 10, itemCount: 5);

        Assert.Equal(21, response.FromItem);
        Assert.Equal(25, response.ToItem);
    }

    [Fact]
    public void FromItem_and_ToItem_zero_for_empty_items()
    {
        var response = CreateResponse(total: 0, page: 1, pageSize: 10, itemCount: 0);

        Assert.Equal(0, response.FromItem);
        Assert.Equal(0, response.ToItem);
    }

    [Fact]
    public void Default_constructor_creates_empty_response()
    {
        var response = new ListResponse<TestRow, TestListRequest>();

        Assert.Equal(0, response.Total);
        Assert.Empty(response.Items);
        // Request is null with default constructor (used only for generic new() constraint)
        Assert.Null(response.Request);
    }

    private static ListResponse<TestRow, TestListRequest> CreateResponse(
        int total, int page, int pageSize, int itemCount)
    {
        var request = new TestListRequest { Page = page, PageSize = pageSize };
        var items = Enumerable.Range(0, itemCount)
            .Select(i => new TestRow { Code = $"C{i}", Name = $"N{i}", Value = i })
            .ToList();
        return new ListResponse<TestRow, TestListRequest>(total, request, items);
    }
}
