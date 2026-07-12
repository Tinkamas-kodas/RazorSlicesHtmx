# Getting Started

## Install packages

```bash
dotnet add package RazorSlicesHtmx.AspNetCore
dotnet add package RazorSlicesHtmx.Bootstrap5        # optional — Bootstrap UI adapter
dotnet add package RazorSlicesHtmx.FluentValidation  # optional — FluentValidation mapping
dotnet add package RazorSlicesHtmx.Generators        # optional — source generators
```

## Register services

```csharp
using RazorSlicesHtmx.AspNetCore.Extensions;
using RazorSlicesHtmx.Bootstrap5.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorSlicesHtmx();
builder.Services.AddRazorSlicesHtmxBootstrap5(); // optional

// App-owned page shell renderer (required)
builder.Services.AddSingleton<IFeaturePageRenderer, MyFeaturePageRenderer>();

// Feature discovery — scans assembly for IFeatureModule implementations
builder.Services.AddSingleton(sp =>
    FeatureRegistry.Discover(typeof(Program).Assembly, sp));
```

Multi-assembly discovery:

```csharp
builder.Services.AddSingleton(sp =>
    FeatureRegistry.Discover([typeof(Program).Assembly, typeof(SharedLib).Assembly], sp));
```

## Configure the pipeline

```csharp
var app = builder.Build();
var features = app.Services.GetRequiredService<FeatureRegistry>();

app.UseStaticFiles();
app.UseHtmxErrorHandling(); // HTMX-aware error toasts — must be before endpoint routing

features.MapEndpoints(app);   // feature-local endpoints (GET /items/list, POST /items, ...)
app.MapFeaturePages(features); // page routes derived from NavigationRouteItem.Route

app.Run();
```

`UseHtmxErrorHandling()` must be placed **before** `features.MapEndpoints(app)` so it wraps all feature endpoints.

## Minimal feature module

```csharp
public sealed class ItemsEndpoints(FeatureResultBuilder resultBuilder) : BaseFeatureModule(resultBuilder)
{
    public override IReadOnlyList<NavigationItem> NavigationItems =>
    [
        new NavigationRouteItem("items", "Items", "/items",
            new PageDefinition(_ => _FeaturePage.Create()))
    ];

    public override void MapEndpoints(WebApplication app)
    {
        app.MapGet("/items", () =>
            Result.Page(TestNavItem, _FeaturePage.Create()));
    }
}
```

## See also

- [Feature Modules](feature-modules.md)
- [Page Shell](page-shell.md)
- [Configuration](configuration.md)
