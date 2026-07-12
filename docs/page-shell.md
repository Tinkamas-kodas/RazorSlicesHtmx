# Page Shell

The full page shell is owned by the app, not by the library. The app provides a page renderer that turns the shell context into a Razor slice tree.

## IFeaturePageRenderer

Implemented by the app. Called by `FeatureResultBuilder.Build()` for full-page (non-HTMX) responses and to render the navigation OOB on HTMX responses:

```csharp
public interface IFeaturePageRenderer
{
    IResult RenderPage(FeatureShellContext shellContext);
    RazorSlice RenderNavigation(FeatureShellContext shellContext);
}
```

Minimal implementation:

```csharp
public sealed class MyFeaturePageRenderer(IOptions<RazorSlicesHtmxOptions> options)
    : IFeaturePageRenderer
{
    public IResult RenderPage(FeatureShellContext ctx) =>
        _Layout.Create(new AppLayoutModel(ctx.NavigationItems, ctx.Detail, ctx.PageTitle));

    public RazorSlice RenderNavigation(FeatureShellContext ctx) =>
        _TopNav.Create(new AppLayoutModel(ctx.NavigationItems, ctx.Detail, ctx.PageTitle));
}
```

Register it:

```csharp
builder.Services.AddSingleton<IFeaturePageRenderer, MyFeaturePageRenderer>();
```

## FeatureShellContext

Passed to both `RenderPage` and `RenderNavigation`:

| Property | Type | Description |
|----------|------|-------------|
| `RouteItem` | `NavigationRouteItem` | Currently active route item |
| `NavigationItems` | `IReadOnlyList<NavigationItem>` | Authorization-filtered navigation tree |
| `Detail` | `RazorSlice` | Current feature detail slice |
| `PageTitle` | `string` | Resolved title (from `WithPageTitle` or `NavigationRouteItem.Label`) |

## ITransientUiRenderer

Implemented by UI adapters (e.g. `Bootstrap5TransientUiRenderer`). Renders dialogs and toasts as Razor slices:

```csharp
public interface ITransientUiRenderer
{
    RazorSlice RenderDialog(RazorSlice content);
    RazorSlice RenderToast(ToastModel toast);
}
```

Bootstrap5 registers its own implementation via `AddRazorSlicesHtmxBootstrap5()`. For custom UI frameworks, implement this interface.

## Custom UI adapter

If your app uses Tailwind, daisyUI, or another framework instead of Bootstrap5:

```csharp
public static IServiceCollection AddMyUiAdapter(this IServiceCollection services)
{
    services.AddSingleton<IPostConfigureOptions<RazorSlicesHtmxOptions>, MyUiDefaults>();
    services.AddSingleton<ITransientUiRenderer, MyTransientUiRenderer>();
    return services;
}

private sealed class MyUiDefaults : IPostConfigureOptions<RazorSlicesHtmxOptions>
{
    public void PostConfigure(string? name, RazorSlicesHtmxOptions options)
    {
        options.DialogClass ??= "w-full max-w-2xl rounded-2xl bg-white shadow-2xl";
        options.ToastDelayMilliseconds ??= 4000;
    }
}

public sealed class MyTransientUiRenderer(IOptions<RazorSlicesHtmxOptions> options)
    : ITransientUiRenderer
{
    private readonly RazorSlicesHtmxOptions _options = options.Value;

    public RazorSlice RenderDialog(RazorSlice content) =>
        MyDialog.Create(new DialogModel(content, _options.DialogClass ?? string.Empty));

    public RazorSlice RenderToast(ToastModel toast) =>
        MyToast.Create(toast);
}
```

The app still owns the full page shell via `IFeaturePageRenderer`. The adapter only covers transient UI (dialogs, toasts).

## Architecture boundaries

```
app                      — page shell, layout, branding, navigation UI
                           implements IFeaturePageRenderer
RazorSlicesHtmx.Bootstrap5 — transient UI only: dialogs, toasts, validation partials
                           implements ITransientUiRenderer
RazorSlicesHtmx.AspNetCore — HTMX orchestration, feature registry, response builders
```

## See also

- [Getting Started](getting-started.md)
- [Configuration](configuration.md)
- [Bootstrap5](bootstrap5.md)
- [Dialogs](dialogs.md)
- [Toasts](toasts.md)
