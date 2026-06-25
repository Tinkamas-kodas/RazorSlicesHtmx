using Microsoft.EntityFrameworkCore;
using RazorSlicesHtmx.Demo.Data;
using RazorSlicesHtmx.Demo.Features.Items.Models;
using RazorSlicesHtmx.Demo.Features.Items.Slices;

namespace RazorSlicesHtmx.Demo.Features.Items.Services;

public sealed class ItemsContentService
{
    private static readonly IReadOnlyDictionary<string, (string errorCode, string errorMessage)[]> EmptyErrors =
        new Dictionary<string, (string errorCode, string errorMessage)[]>();

    public FeatureMetadata CreateMetadata() => new(
        200,
        "items",
        "Items",
        "/items",
        "Paging, search, sort and CRUD with HTMX");

    public PageDefinition CreatePageDefinition() => new(
        CreateMetadata(),
        httpContext =>
        {
            var db = httpContext.RequestServices.GetRequiredService<AppDbContext>();
            var search = new ItemSearchModel(httpContext.Request.Query["Search"].ToString() is { Length: > 0 } s ? s : null);
            var query = ItemListQuery.FromQuery(httpContext.Request.Query);
            return _FeaturePage.Create(CreateListModel(db, search, query));
        });


    public ItemListModel CreateListModel(AppDbContext db, ItemSearchModel search, ItemListQuery query)
    {
        var items = db.Items
            .AsNoTracking()
            .Select(item => new ItemRowModel(item.Id, item.Code, item.Name, item.IsEnabled));

        if (!string.IsNullOrWhiteSpace(search.Search))
        {
            var term = search.Search.Trim();
            items = items.Where(row => row.Code.Contains(term) || row.Name.Contains(term));
        }

        var paged = items.ToPagedList(query);
        return new ItemListModel(paged.Total, search, query, paged.Items);
    }



    public ItemUpsertRequest CreateCreateFormModel() => new()
    {
        IsEnabled = true,
        Errors = EmptyErrors
    };

    public ItemUpsertRequest CreateEditFormModel(Item item) => new()
    {
        Id = item.Id,
        Code = item.Code,
        Name = item.Name,
        IsEnabled = item.IsEnabled,
        Errors = EmptyErrors
    };

    

    public ItemDeleteDialogModel CreateDeleteDialogModel(Item item) => new(
        item.Id,
        item.Code,
        item.Name);

    public void TrimRequest(ItemUpsertRequest request)
    {
        request.Code = request.Code?.Trim();
        request.Name = request.Name?.Trim();
    }

    public Item CreateEntity(ItemUpsertRequest request) => new()
    {
        Code = NormalizeCode(request.Code),
        Name = NormalizeName(request.Name),
        IsEnabled = request.IsEnabled
    };

    public void Apply(Item entity, ItemUpsertRequest request)
    {
        entity.Code = NormalizeCode(request.Code);
        entity.Name = NormalizeName(request.Name);
        entity.IsEnabled = request.IsEnabled;
    }

    public static string NormalizeCode(string? code) => (code ?? string.Empty).Trim().ToUpperInvariant();

    public static string NormalizeName(string? name) => (name ?? string.Empty).Trim();
}