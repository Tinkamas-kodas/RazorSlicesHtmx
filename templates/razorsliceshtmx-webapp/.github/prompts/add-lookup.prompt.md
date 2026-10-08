---
agent: agent
description: "Add a reusable lookup field with HTMX search and paging"
---

# Add a Lookup Field to a Feature

Add a searchable lookup field that supports HTMX paging and can be rendered either with the Bootstrap helper or with manual web component markup.

## Ask the user for:
1. Feature name
2. Request model and property to bind selected value (for example `ClientId`)
3. Lookup endpoint route (for example `lookup/search`)
4. Data source and filter fields (code/name)
5. Page size
6. Preferred rendering mode: helper (`LookupSelect`) or manual web component

## Contracts and endpoint

Use core contracts from `RazorSlicesHtmx.AspNetCore.Models`:

- `LookupRequest`
- `LookupResponse`
- `LookupItem`

Add endpoint pattern:

```csharp
group.MapGet("search", (IFeatureUri featureUri, [AsParameters] LookupRequest request) =>
{
    request.Path = string.IsNullOrWhiteSpace(request.Path)
        ? featureUri.Uri("search")
        : request.Path;

    request.Normalize(defaultPageSize: 12, maxPageSize: 50);

    // Filter, page + 1 for hasMore
    var items = ...;
    var hasMore = ...;

    var response = request.ToResponse(items, hasMore);
    return Result.Fragment(_LookupResults.Create(response));
});
```

## Rendering mode A: Bootstrap helper

```cshtml
@using RazorSlicesHtmx.Bootstrap5.Extensions

@{ var f = Model.Request.For(); }

@f.ClientId.LookupSelect(
    path: FeatureUri.Uri("search"),
    placeholder: "Type code or name",
    pageSize: 12)
```

## Rendering mode B: no helper (manual web component)

Render `rsh-lookup-select` with matching `data-*` IDs and HTMX search input.

Required attributes:

- `data-hidden-id`
- `data-search-id`
- `data-clear-id`
- `data-path`
- `data-page-size`

Hidden input behavior:

- Keep hidden `value` and hidden `text` inputs disabled when no selection exists
- Enable both hidden inputs when an item is selected
- Disable both again on clear

Required HTMX input settings:

- `hx-get` to lookup endpoint
- `hx-target` to results container
- `hx-swap="innerHTML"`
- `hx-trigger="focus, input changed delay:250ms, search"`
- `hx-vals` including `query`, `page`, `pageSize`, `path`

## Results fragment

In `_LookupResults.cshtml` render each option as a button with:

- `data-rsh-lookup-value`
- `data-rsh-lookup-text`

For infinite scroll, append sentinel when `Model.HasMore`:

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

## Assets

Ensure layout includes:

```cshtml
<link rel="stylesheet" href="/_content/RazorSlicesHtmx.Bootstrap5/css/rsh-lookup-select.css">
<script src="/_content/RazorSlicesHtmx.Bootstrap5/js/rsh-lookup-select.js"></script>
```

## Important

- Keep lookup contracts in AspNetCore; UI behavior in Bootstrap5.
- Prefer helper mode for consistency, but manual mode is first-class and documented.
- Always verify results appear during typing and selection updates hidden field value.
- Verify clear state does not post empty hidden values.
