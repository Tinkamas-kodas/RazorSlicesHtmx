# Lookup

Reusable searchable lookup with HTMX paging.

This feature is split intentionally:

- Contracts live in `RazorSlicesHtmx.AspNetCore`
- UI helper and browser behavior live in `RazorSlicesHtmx.Bootstrap5`

## Contracts

Use these core models from `RazorSlicesHtmx.AspNetCore.Models`:

- `LookupRequest` — incoming query/paging parameters
- `LookupItem` — one result item (`Value`, `Text`)
- `LookupResponse` — normalized lookup response with paging metadata

Typical endpoint flow:

1. Accept `[AsParameters] LookupRequest request`
2. Call `request.Normalize(...)`
3. Filter data
4. Return `request.ToResponse(items, hasMore)`

## Option A: Use helper + web component

Use the Bootstrap helper to render markup and attach HTMX attributes:

```cshtml
@using RazorSlicesHtmx.Bootstrap5.Extensions

@{ var f = Model.Request.For(); }

<label class="form-label" for="@(f.ClientId.Id + "_lookup_search")">Client</label>
@f.ClientId.LookupSelect(
    path: FeatureUri.Uri("search"),
    selectedValueTextFieldName: f.ClientName.Name,
    selectedValueResolvePath: FeatureUri.Uri("search-selected"),
    placeholder: "Type code or client name",
    selectedText: Model.Request.ClientName,
    pageSize: 12)
```

Load assets in layout:

```cshtml
<link rel="stylesheet" href="/_content/RazorSlicesHtmx.Bootstrap5/css/rsh-lookup-select.css">
<script src="/_content/RazorSlicesHtmx.Bootstrap5/js/rsh-lookup-select.js"></script>
```

## Option B: Use web component directly (without helper)

You can skip the helper and write markup manually.

```cshtml
<rsh-lookup-select
    class="rsh-lookup-select d-block"
    data-hidden-id="ClientId"
    data-search-id="ClientId_lookup_search"
    data-clear-id="ClientId_lookup_clear"
    data-text-hidden-id="ClientId_lookup_text"
    data-resolve-path="/lookup/search-selected"
    data-component-disabled="false"
    data-path="/lookup/search"
    data-page-size="12">

    <input type="hidden" id="ClientId" name="ClientId" value="" disabled />
    <input type="hidden" id="ClientId_lookup_text" name="ClientName" value="" disabled />

    <div class="rsh-lookup-surface position-relative">
        <div class="input-group">
            <input type="search"
                   id="ClientId_lookup_search"
                   class="form-control"
                   autocomplete="off"
                   placeholder="Type code or client name"
                   hx-get="/lookup/search"
                    hx-target="next .rsh-lookup-results"
                   hx-swap="innerHTML"
                   hx-trigger="focus, input changed delay:250ms, search"
                    hx-vals="js:{ query: this.value, page: 1, pageSize: 12, path: '/lookup/search' }" />

              <div class="rsh-lookup-results list-group shadow-sm d-none"
                  role="listbox"
                  aria-label="Lookup results"></div>

            <button type="button"
                    id="ClientId_lookup_clear"
                    class="btn btn-outline-secondary"
                    aria-label="Clear selection">Clear</button>
        </div>
    </div>
</rsh-lookup-select>
```

Important: `data-*` attributes for hidden/search/clear/text fields must match actual element IDs.

Hidden field submission behavior:

- empty value => hidden value/text inputs stay disabled (not posted)
- selected value => hidden value/text inputs are enabled
- clear action => hidden value/text inputs are disabled again

When `value` is present but the search text is empty, component calls `data-resolve-path?value=...` and expects JSON with `value` and `text`.

## Results fragment contract

Lookup result partial should render options with item metadata attributes.

```cshtml
@foreach (var item in Model.Items)
{
    <button type="button"
            class="list-group-item list-group-item-action rsh-lookup-item"
            role="option"
            data-rsh-lookup-value="@item.Value"
            data-rsh-lookup-text="@item.Text">
        <span class="rsh-lookup-item-text">@item.Text</span>
        <span class="rsh-lookup-item-value">@item.Value</span>
    </button>
}
```

For infinite scroll add sentinel element when `Model.HasMore == true`:

```cshtml
<div class="rsh-lookup-more"
     hx-get="@nextUrl"
    hx-target="closest .rsh-lookup-results"
     hx-swap="beforeend"
     hx-trigger="revealed once"
     hx-on::after-request="this.remove()">
    Loading more...
</div>
```

## Notes

- Use `focus, input changed delay:250ms, search` as default trigger for reliable typing behavior.
- Keep lookup endpoint stateless and driven entirely by query parameters.
- The custom element handles select/clear/show-hide behavior; server returns only HTML fragments.

## See also

- [Bootstrap5](bootstrap5.md)
- [Forms](forms.md)
- [List Features](list-features.md)
