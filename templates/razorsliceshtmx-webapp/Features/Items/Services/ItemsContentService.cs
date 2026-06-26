using RazorSlicesHtmx.AspNetCore.Models;
using RshtmxApp.Features.Items.Models;
using RshtmxApp.Features.Items.Slices;

namespace RshtmxApp.Features.Items.Services;

public sealed class ItemsContentService
{
    private static readonly List<ItemRow> Store =
    [
        new() { Id = 1, Name = "Widget A", Description = "First sample widget", IsActive = true },
        new() { Id = 2, Name = "Widget B", Description = "Second sample widget", IsActive = true },
        new() { Id = 3, Name = "Gadget C", Description = "A handy gadget", IsActive = false },
        new() { Id = 4, Name = "Doohickey D", Description = "Mystery item", IsActive = true },
        new() { Id = 5, Name = "Thingamajig E", Description = "Essential thing", IsActive = true },
    ];

    private static int _nextId = 6;

    public NavigationRouteItem CreateNavigationItem() => new(
        "items",
        "Items",
        "/items",
        new PageDefinition(() => _ItemsPage.Create()),
        Order: 10);

    public ItemListModel CreateListModel(ItemSearch search, ItemListQuery query)
    {
        IEnumerable<ItemRow> items = Store;

        if (!string.IsNullOrWhiteSpace(search.SearchTerm))
        {
            var term = search.SearchTerm.Trim();
            items = items.Where(x =>
                x.Name.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                x.Description.Contains(term, StringComparison.OrdinalIgnoreCase));
        }

        var queryable = items.AsQueryable();
        var response = queryable.ToPagedList(query);

        return new ItemListModel { Response = response, Search = search };
    }

    public ItemUpsertRequest? GetForEdit(int id)
    {
        var item = Store.FirstOrDefault(x => x.Id == id);
        if (item is null) return null;

        return new ItemUpsertRequest
        {
            Id = item.Id,
            Name = item.Name,
            Description = item.Description,
            IsActive = item.IsActive,
        };
    }

    public ItemDeleteModel? GetForDelete(int id)
    {
        var item = Store.FirstOrDefault(x => x.Id == id);
        return item is null ? null : new ItemDeleteModel(item.Id, item.Name);
    }

    public void Create(ItemUpsertRequest request)
    {
        Store.Add(new ItemRow
        {
            Id = _nextId++,
            Name = request.Name,
            Description = request.Description,
            IsActive = request.IsActive,
        });
    }

    public bool Update(ItemUpsertRequest request)
    {
        var item = Store.FirstOrDefault(x => x.Id == request.Id);
        if (item is null) return false;

        item.Name = request.Name;
        item.Description = request.Description;
        item.IsActive = request.IsActive;
        return true;
    }

    public bool Delete(int id)
    {
        var item = Store.FirstOrDefault(x => x.Id == id);
        if (item is null) return false;
        Store.Remove(item);
        return true;
    }
}
