# Navigation & URL

## WithNavigation — push/replace URL without redirecting

Appends `HX-Replace-Url` or `HX-Push-Url` to the HTMX response. The browser URL updates without a page reload. The content is served as a normal fragment.

```csharp
// Replace current history entry
return Result.Page(detail)
    .WithNavigation("/items", replace: true)   // HX-Replace-Url
    .Build();

// Push a new history entry
return Result.Page(detail)
    .WithNavigation("/items/new", replace: false) // HX-Push-Url
    .Build();
```

Default: `replace: true`.

## WithLocation — client-side navigation

Sends `HX-Location` — htmx makes a new GET request to the specified URL and swaps the result into the detail target. The current response body is not swapped.

```csharp
return Result.Page(detail)
    .WithLocation("/items")
    .Build();
```

With a toast that fires after the location navigation completes:

```csharp
return Result.Page(detail)
    .WithLocation("/items")
    .WithToast("Item deleted", tone: ToastTone.Warning)
    .Build();
```

The `HX-Location` payload includes `target` (from `RazorSlicesHtmxOptions.DetailTargetSelector`) and `swap` (from `DefaultOobSwap`). Toasts are sent as `HX-Trigger` on the same response — they fire after the location follow-up swap completes.

## Navigation OOB

On HTMX requests, `FeatureResultBuilder` sends navigation as an OOB update so the active nav item updates in the sidebar. The `INavOobStrategy` decides whether to send it:

- **Default (`HxCurrentUrlNavStrategy`)** — sends nav OOB only when the active feature changes (compares `HX-Current-URL` header against the requested route item).
- **Override** — call `WithNav(true/false)` to force send or skip for a specific response.

When features are registered with route-group prefixes, navigation routes can stay relative in the feature module. The registry composes absolute routes from `prefix + relative route` before nav matching.

```csharp
return Result.Page(detail)
    .WithNav(false)  // skip nav OOB for this response
    .Build();
```

## Custom nav strategy

Implement `INavOobStrategy` and register it:

```csharp
public sealed class AlwaysSendNavStrategy : INavOobStrategy
{
    public bool ShouldSendNav(HttpRequest request, NavigationItem routeItem) => true;
}
```

```csharp
builder.Services.AddSingleton<INavOobStrategy, AlwaysSendNavStrategy>();
```

## See also

- [Feature Modules](feature-modules.md)
- [Response Building](response-building.md)
- [Configuration](configuration.md)
