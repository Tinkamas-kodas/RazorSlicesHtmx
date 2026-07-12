# State Management

State is persisted as hidden-field divs in the DOM and included via `hx-include`. This keeps search, paging, and sort state synchronized across HTMX interactions without storing anything server-side.

## Declaring a state model

Any `partial` class or record passed to `WithState(...)` gets `IHasState` generated automatically. No attribute needed.

```csharp
// Search/filter state
public sealed partial record ItemSearchModel(string? Search);

// Paging and sort state — see List Features for ListRequest<T>
public sealed partial class ItemListQuery : ListRequest<ItemRowModel> { ... }
```

The generator produces three members on the type:

```csharp
public const string StateId = "item-search-model"; // kebab-case type name — no instance needed

public IHtmlContent Serialize();      // <div id="item-search-model"><input ...></div>
public IHtmlContent SerializeOob();   // same div wrapped in hx-swap-oob
```

## Using state in an endpoint

```csharp
app.MapGet("/items/list", ([AsParameters] ItemSearchModel search, ItemListQuery query, AppDbContext db) =>
{
    var model = _content.CreateListModel(db, search, query);
    return Result.Page(_FeaturePage.Create(model))
        .WithState(search)   // sends OOB update on HTMX responses
        .WithState(query)
        .Build();
});
```

On HTMX fragment responses, `WithState` sends each model as an OOB update so the hidden divs stay current.

## Host slice — initial render

Render state divs once in the feature host slice (shown on full-page loads):

```cshtml
@Model.Search.Serialize()
@Model.List.Serialize()
```

## HTMX include patterns

Use `StateId` directly in `hx-include` — it's a `const`, no model instance needed:

```cshtml
{{-- Sort/paging: include both search and list state --}}
hx-include="#@ItemSearchModel.StateId, #@ItemListQuery.StateId"

{{-- Search submit: include sort state; reset page via hx-vals --}}
hx-include="#@ItemListQuery.StateId"
hx-vals='{"Page":"1"}'

{{-- Actions that don't need state --}}
hx-params="none"
```

Separating search and list state means you control exactly which state each request carries — no CSS `:is()` filters needed.

## Excluding properties

Mark a property with `[StateNotMap]` to exclude it from serialization:

```csharp
public sealed partial record ItemSearchModel(
    string? Search,
    [property: StateNotMap] string? InternalToken);
```

## See also

- [List Features](list-features.md)
- [Response Building](response-building.md)
- [Forms](forms.md)
