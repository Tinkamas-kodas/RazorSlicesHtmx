using RazorSlicesHtmx.AspNetCore.Models;

namespace RazorSlicesHtmx.AspNetCore.Tests;

public class ListSortHeaderContextTests
{
    [Fact]
    public void GetModel_returns_inactive_state_for_different_column()
    {
        var request = new TestListRequest { SortBy = "name", SortDirection = SortDirection.Asc };
        var response = new ListResponse<TestRow, TestListRequest>(10, request, []);
        var context = response.SortHeader("/items/list", new NullSortHeaderRenderer());

        var model = context.GetModel("code", "Code");

        Assert.Equal("code", model.Column);
        Assert.Equal("Code", model.Label);
        Assert.Equal("/items/list", model.GetUrl);
        Assert.Equal(SortState.Inactive, model.State);
        Assert.Equal(SortDirection.Asc, model.NextDirection);
    }

    [Fact]
    public void GetModel_returns_SortedAsc_for_current_asc_column()
    {
        var request = new TestListRequest { SortBy = "code", SortDirection = SortDirection.Asc };
        var response = new ListResponse<TestRow, TestListRequest>(10, request, []);
        var context = response.SortHeader("/items/list", new NullSortHeaderRenderer());

        var model = context.GetModel("code", "Code");

        Assert.Equal(SortState.SortedAsc, model.State);
        Assert.Equal(SortDirection.Desc, model.NextDirection);
    }

    [Fact]
    public void GetModel_returns_SortedDesc_for_current_desc_column()
    {
        var request = new TestListRequest { SortBy = "code", SortDirection = SortDirection.Desc };
        var response = new ListResponse<TestRow, TestListRequest>(10, request, []);
        var context = response.SortHeader("/items/list", new NullSortHeaderRenderer());

        var model = context.GetModel("code", "Code");

        Assert.Equal(SortState.SortedDesc, model.State);
        Assert.Equal(SortDirection.Asc, model.NextDirection);
    }

    [Fact]
    public void GetModel_is_case_insensitive()
    {
        var request = new TestListRequest { SortBy = "CODE", SortDirection = SortDirection.Asc };
        var response = new ListResponse<TestRow, TestListRequest>(10, request, []);
        var context = response.SortHeader("/items/list", new NullSortHeaderRenderer());

        var model = context.GetModel("code", "Code");

        Assert.Equal(SortState.SortedAsc, model.State);
    }

    [Fact]
    public void ToPagerModel_creates_correct_model()
    {
        var request = new TestListRequest { Page = 2, PageSize = 5 };
        var items = Enumerable.Range(6, 5).Select(i => new TestRow { Code = $"C{i}" }).ToList();
        var response = new ListResponse<TestRow, TestListRequest>(23, request, items);

        var pager = response.ToPagerModel("/items/list");

        Assert.Equal("/items/list", pager.GetUrl);
        Assert.Equal(5, pager.TotalPages);
        Assert.Equal(2, pager.CurrentPage);
        Assert.Equal(5, pager.PageSize);
        Assert.Equal(23, pager.Total);
        Assert.Equal(6, pager.FromItem);
        Assert.Equal(10, pager.ToItem);
    }

    [Fact]
    public void ToPagerModel_with_texts_uses_custom_texts()
    {
        var request = new TestListRequest { Page = 1, PageSize = 10 };
        var response = new ListResponse<TestRow, TestListRequest>(5, request, []);
        var texts = new PagerTexts(Showing: "Rodomi", Of: "iš");

        var pager = response.ToPagerModel("/items/list", texts);

        Assert.Equal("Rodomi", pager.EffectiveTexts.Showing);
        Assert.Equal("iš", pager.EffectiveTexts.Of);
    }

    private sealed class NullSortHeaderRenderer : IListSortHeaderRenderer
    {
        public Microsoft.AspNetCore.Html.IHtmlContent RenderHeader(SortButtonModel model) =>
            Microsoft.AspNetCore.Html.HtmlString.Empty;
    }
}
