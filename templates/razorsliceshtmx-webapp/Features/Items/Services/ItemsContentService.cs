using RshtmxApp.Features.Items.Models;

namespace RshtmxApp.Features.Items.Services;

public sealed class ItemsContentService
{
    private static readonly IReadOnlyDictionary<string, (string errorCode, string errorMessage)[]> EmptyErrors =
        new Dictionary<string, (string errorCode, string errorMessage)[]>();

    private static readonly List<ItemRowModel> Store =
    [
        new(1, "Widget Alpha", "First sample widget", true),
        new(2, "Widget Beta", "Second sample widget", true),
        new(3, "Gadget Gamma", "A handy gadget", false),
        new(4, "Doohickey Delta", "Mystery item", true),
        new(5, "Thingamajig Epsilon", "Essential thing", true),
        new(6, "Contraption Zeta", "Useful device", false),
        new(7, "Apparatus Eta", "Lab equipment", true),
        new(8, "Mechanism Theta", "Precision part", true),
        new(9, "Device Iota", "Smart device", false),
        new(10, "Implement Kappa", "Garden tool", true),
        new(11, "Tool Lambda", "Power tool", true),
        new(12, "Instrument Mu", "Measurement device", true),
    ];

    private static int _nextId = 13;

    public ItemListModel CreateListModel(ItemSearchModel search, ItemListQuery query)
    {
        IEnumerable<ItemRowModel> items = Store;

        if (!string.IsNullOrWhiteSpace(search.Search))
        {
            var term = search.Search.Trim();
            items = items.Where(row =>
                row.Name.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                row.Description.Contains(term, StringComparison.OrdinalIgnoreCase));
        }

        var paged = items.ToPagedList(query);
        return new ItemListModel(paged.Total, search, query, paged.Items);
    }

    public ItemUpsertRequest CreateCreateFormModel() => new()
    {
        IsActive = true,
        Errors = EmptyErrors
    };

    public ItemUpsertRequest CreateEditFormModel(ItemRowModel row) => new()
    {
        Id = row.Id,
        Name = row.Name,
        Description = row.Description,
        IsActive = row.IsActive,
        Errors = EmptyErrors
    };

    public ItemDeleteDialogModel CreateDeleteDialogModel(ItemRowModel row) => new(row.Id, row.Name);

    public async Task<ItemUpsertRequest> ReadUpsertRequestAsync(HttpRequest request, int? id = null)
    {
        var form = await request.ReadFormAsync();

        return new ItemUpsertRequest
        {
            Id = id,
            Name = form["Name"].ToString(),
            Description = form["Description"].ToString(),
            IsActive = form["IsActive"].ToString().Contains("true", StringComparison.OrdinalIgnoreCase)
        };
    }

    public void TrimRequest(ItemUpsertRequest request)
    {
        request.Name = request.Name?.Trim();
        request.Description = request.Description?.Trim();
    }

    public ItemRowModel? FindById(int id) => Store.FirstOrDefault(x => x.Id == id);

    public void Create(ItemUpsertRequest request)
    {
        Store.Add(new ItemRowModel(
            _nextId++,
            request.Name ?? string.Empty,
            request.Description ?? string.Empty,
            request.IsActive));
    }

    public void Apply(int id, ItemUpsertRequest request)
    {
        var index = Store.FindIndex(x => x.Id == id);
        if (index >= 0)
        {
            Store[index] = new ItemRowModel(
                id,
                request.Name ?? string.Empty,
                request.Description ?? string.Empty,
                request.IsActive);
        }
    }

    public bool Delete(int id)
    {
        var index = Store.FindIndex(x => x.Id == id);
        if (index < 0) return false;
        Store.RemoveAt(index);
        return true;
    }
}
