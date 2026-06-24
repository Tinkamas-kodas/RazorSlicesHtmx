# RazorSlicesHtmx

`RazorSlicesHtmx` is a small server-rendered HTMX toolkit built around RazorSlices and feature discovery.

Licensed under the [MIT License](LICENSE).

See the [changelog](CHANGELOG.md) for release history.

The codebase is intentionally split so the core package handles feature registration and HTMX response composition, while application UI stays outside the core.

## Packages

### `RazorSlicesHtmx.AspNetCore`

Core package for:

- feature discovery and registration
- `IFeatureModule`, `PageDefinition`, `FeatureMetadata`, and `FeatureRegistry`
- HTMX response composition through `FeatureResultBuilder`
- OOB fragments, triggers, navigation, and location responses
- technical slices that are not tied to a CSS framework, such as `_Empty`, `HtmxFragment`, and `HtmxOob`
- app-facing contracts such as `FeatureShellContext`, `IFeaturePageRenderer`, and `ITransientUiRenderer`

Notably, this package does **not** own your app layout, branding, page shell, or navigation UI.

### `RazorSlicesHtmx.Bootstrap5`

Bootstrap adapter for transient UI only:

- dialog rendering
- toast rendering
- validation message partial
- Bootstrap-specific helper extensions
- Bootstrap-specific default settings registered through `PostConfigure<RazorSlicesHtmxOptions>`

This package is intentionally **not** a full app shell or admin template.

What it implements:

- `ITransientUiRenderer` for Bootstrap dialog and toast rendering
- Bootstrap validation message markup for field-level validation feedback
- Bootstrap-specific helper extensions such as `InputClass(...)`
- Bootstrap fallback defaults for `DialogClass` and `ToastDelayMilliseconds`

How to use it:

```csharp
using RazorSlicesHtmx.Bootstrap5.Extensions;

builder.Services.AddRazorSlicesHtmxBootstrap5();
```

Optional override example:

```csharp
builder.Services.AddRazorSlicesHtmxBootstrap5(options =>
{
  options.DialogClass = "modal-dialog modal-dialog-centered modal-lg";
  options.ToastDelayMilliseconds = 3200;
});
```

Use this package when you want Bootstrap-styled dialogs, toasts, and validation messages, but still want the app to own the full page shell.

### `RazorSlicesHtmx.FluentValidation`

Validation integration package for:

- mapping `FluentValidation.Results.ValidationResult` into UI-friendly dictionaries

What it implements:

- `ToErrorDictionary()` for transforming `ValidationResult` into the error structure used by the view layer

How to use it:

```csharp
using FluentValidation.Results;
using RazorSlicesHtmx.FluentValidation.Extensions;

ValidationResult validationResult = validator.Validate(request);

var errors = validationResult.ToErrorDictionary();
```

This package is intentionally narrow. It is meant to transform FluentValidation errors, not to own validation UI rendering.

### `RazorSlicesHtmx.Generators`

Roslyn source generator package for strongly typed HTML field metadata used in forms.

What it provides:

- `[GenerateHtmlNames(maxDepth: 0)]` attribute (injected at compile time)
- generated constants grouped per field as `For.<Field>.Name`, `For.<Field>.Id`, and `For.<Field>.Path`
- compile-time safe form metadata without runtime expression parsing

How to use it:

```csharp
using RazorSlicesHtmx.HtmlNames;

[GenerateHtmlNames]
public sealed class ItemUpsertRequest
{
  public string? Code { get; set; }
  public string? Name { get; set; }
}
```

In Razor:

```csharp
@using For = RazorSlicesHtmx.Demo.HtmlNames.RazorSlicesHtmx.Demo.Features.Items.Models.ItemUpsertRequestHtml
```

```html
<label for="@For.Code.Id">Code</label>
<input id="@For.Code.Id" name="@For.Code.Name">
```

NuGet packaging notes:

- package is shipped as an analyzer (`analyzers/dotnet/cs`)
- analyzer assembly is not included as a runtime `lib` dependency

### Demo app

The demo lives in `demo/RazorSlicesHtmx.Demo`.

The demo is responsible for:

- page layout
- page title and branding
- sidebar/navigation UI
- app-level page shell rendering
- feature examples and sample styling

## Architecture Boundaries

Current intended split:

- `AspNetCore`: HTMX orchestration and feature model
- `Bootstrap5`: transient UI adapter for dialogs, toasts, and validation partials
- `Generators`: compile-time HTML Name/Id/Path generation for request/view models
- app: full page shell and branding

That means the app provides its own page renderer and shell models, while the Bootstrap package only plugs into transient UI concerns.

## Core Contracts

The most important core contracts are:

- `IFeatureModule`
  feature registration unit; exposes a `PageDefinition` and maps feature-local endpoints
- `FeatureMetadata`
  lightweight description of a feature page for discovery and navigation projection
- `PageDefinition`
  binds feature metadata to a detail slice factory
- `FeatureShellContext`
  minimal shell context passed to the app-level page renderer: current page, all pages, and current detail slice
- `IFeaturePageRenderer`
  implemented by the app; responsible for full-page shell rendering and navigation rendering
- `FeatureResultBuilder`
  fluent orchestration API for feature endpoints

## Quick Start

Minimal service registration looks like this:

```csharp
using RazorSlicesHtmx.AspNetCore.Extensions;
using RazorSlicesHtmx.Bootstrap5.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorSlicesHtmx();
builder.Services.AddRazorSlicesHtmxBootstrap5();

// App-owned page shell renderer
builder.Services.AddSingleton<IFeaturePageRenderer, DemoFeaturePageRenderer>();

// Feature discovery
builder.Services.AddSingleton(sp =>
    FeatureRegistry.Discover(typeof(Program).Assembly, sp));
```

And the app pipeline:

```csharp
var app = builder.Build();
var features = app.Services.GetRequiredService<FeatureRegistry>();

app.UseStaticFiles();

features.MapEndpoints(app);
app.MapFeaturePages(features);

app.Run();
```

## Configuration

Shared configuration goes through `RazorSlicesHtmxOptions` from `RazorSlicesHtmx.AspNetCore`.

Current settings:

- `DetailTargetSelector`: HTMX target for page/detail navigation
- `DialogHostSelector`: selector used for dialog OOB updates and dialog lifecycle hooks
- `DialogClass`: CSS class applied to the dialog container
- `ToastHostSelector`: selector used for toast OOB updates and toast lifecycle hooks
- `ToastDelayMilliseconds`: toast auto-hide delay
- `DefaultOobSwap`: default HTMX OOB swap mode

Example `appsettings.json` section:

```json
{
  "RazorSlicesHtmx": {
    "DetailTargetSelector": "#details-pane",
    "DialogHostSelector": "#dialog-host",
    "DialogClass": "modal-dialog modal-dialog-centered modal-lg",
    "ToastHostSelector": "#toast-host",
    "ToastDelayMilliseconds": 3200,
    "DefaultOobSwap": "innerHTML"
  }
}
```

Example binding:

```csharp
using RazorSlicesHtmx.AspNetCore.Options;

builder.Services.Configure<RazorSlicesHtmxOptions>(
    builder.Configuration.GetSection("RazorSlicesHtmx"));

builder.Services.AddRazorSlicesHtmx();
builder.Services.AddRazorSlicesHtmxBootstrap5();
```

## Bootstrap5 Defaults

`AddRazorSlicesHtmxBootstrap5()` uses `PostConfigure<RazorSlicesHtmxOptions>` to apply Bootstrap-specific fallback defaults.

Today that means:

- `DialogClass` falls back to `modal-dialog modal-dialog-centered`
- `ToastDelayMilliseconds` falls back to `2600`

Those values are only used when the app has not already set them through configuration or `Configure<RazorSlicesHtmxOptions>(...)`.

## Custom UI Adapter Example

You are not required to use the Bootstrap5 package.

If your app uses another UI approach, such as Tailwind, you can provide your own `ITransientUiRenderer` and register your own fallback defaults.

Example shape:

```csharp
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using RazorSlicesHtmx.AspNetCore.Models;
using RazorSlicesHtmx.AspNetCore.Options;
using RazorSlicesHtmx.AspNetCore.Rendering;

public static class TailwindServiceCollectionExtensions
{
  public static IServiceCollection AddRazorSlicesHtmxTailwind(
    this IServiceCollection services,
    Action<RazorSlicesHtmxOptions>? configure = null)
  {
    if (configure is not null)
    {
      services.Configure(configure);
    }

    services.AddSingleton<IPostConfigureOptions<RazorSlicesHtmxOptions>, TailwindDefaults>();
    services.AddSingleton<ITransientUiRenderer, TailwindTransientUiRenderer>();

    return services;
  }

  private sealed class TailwindDefaults : IPostConfigureOptions<RazorSlicesHtmxOptions>
  {
    public void PostConfigure(string? name, RazorSlicesHtmxOptions options)
    {
      if (string.IsNullOrWhiteSpace(options.DialogClass))
      {
        options.DialogClass = "w-full max-w-2xl rounded-2xl bg-white shadow-2xl";
      }

      if (options.ToastDelayMilliseconds is null)
      {
        options.ToastDelayMilliseconds = 4000;
      }
    }
  }
}

public sealed class TailwindTransientUiRenderer(IOptions<RazorSlicesHtmxOptions> options)
  : ITransientUiRenderer
{
  private readonly RazorSlicesHtmxOptions _options = options.Value;

  public RazorSlice RenderDialog(RazorSlice content) =>
    TailwindDialog.Create(new DialogModel(content, _options.DialogClass ?? string.Empty));

  public RazorSlice RenderToast(ToastModel toast) =>
    TailwindToast.Create(toast);
}
```

What stays the same:

- `AspNetCore` still owns HTMX orchestration
- the app still owns the full page shell through `IFeaturePageRenderer`
- your adapter only owns transient UI concerns such as dialog and toast rendering

This is the intended extension model for non-Bootstrap apps.

## App-Owned Shell

The full page shell is intentionally outside the libraries.

An app is expected to provide:

- its own layout model
- its own page shell model
- its own Razor layout/page/sidebar slices
- an `IFeaturePageRenderer` implementation

In the demo app this is done by `DemoFeaturePageRenderer`, which turns `FeatureShellContext` into app-specific shell models and Razor slices.

This keeps branding, navigation, title formatting, and page shell decisions in the app instead of in a reusable package.

## Validation Integration

`RazorSlicesHtmx.FluentValidation` stays UI-agnostic.

It provides validation result contracts and helper methods, while Bootstrap-specific validation UI lives in `RazorSlicesHtmx.Bootstrap5`.

That split is deliberate:

- FluentValidation package: data and helper logic
- Bootstrap5 package: concrete validation markup

## What The Libraries Do Not Try To Be

These packages are not intended to be:

- a full frontend framework
- a design system
- a complete layout/navigation framework
- an opinionated admin shell

The main goal is to make HTMX feature flows, transient UI responses, and feature registration easier to compose in a RazorSlices-based app.

## Demo Reference

If you want a concrete reference, look at `demo/RazorSlicesHtmx.Demo` for:

- feature registration
- app-owned page shell rendering
- Bootstrap5 transient UI integration
- FluentValidation integration
- vertical slice examples including CRUD and nested HTMX interactions