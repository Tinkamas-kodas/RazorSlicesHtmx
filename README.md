# RazorSlicesHtmx

A server-rendered HTMX toolkit for ASP.NET Core built around [RazorSlices](https://github.com/DamianEdwards/RazorSlices). Provides feature module discovery, hierarchical navigation with authorization, HTMX response composition, and source generators for forms and state.

Licensed under the [MIT License](LICENSE). See the [changelog](CHANGELOG.md) for release history.

## Packages

| Package | Description |
|---------|-------------|
| `RazorSlicesHtmx.AspNetCore` | Core — feature registry, navigation, HTMX response builders, error middleware |
| `RazorSlicesHtmx.Bootstrap5` | Bootstrap 5 adapter — dialogs, toasts, form helpers, pager, sort headers |
| `RazorSlicesHtmx.FluentValidation` | FluentValidation → error dictionary mapping |
| `RazorSlicesHtmx.Generators` | Roslyn generators — HTML names, form binding, list request binding, state |

## Quick start

```bash
dotnet add package RazorSlicesHtmx.AspNetCore
dotnet add package RazorSlicesHtmx.Bootstrap5
dotnet add package RazorSlicesHtmx.Generators
```

```csharp
builder.Services.AddRazorSlicesHtmx();
builder.Services.AddRazorSlicesHtmxBootstrap5();
builder.Services.AddSingleton<IFeaturePageRenderer, MyFeaturePageRenderer>();
builder.Services.AddSingleton(sp => FeatureRegistry.Discover(typeof(Program).Assembly, sp));

var features = app.Services.GetRequiredService<FeatureRegistry>();
app.UseHtmxErrorHandling();
features.MapEndpoints(app);
app.MapFeaturePages(features);
```

See [docs/getting-started.md](docs/getting-started.md) for the full setup guide.

## Documentation

| Topic | Description |
|-------|-------------|
| [Getting Started](docs/getting-started.md) | Install, DI setup, minimal app, pipeline |
| [Feature Modules](docs/feature-modules.md) | `IFeatureModule`, `BaseFeatureModule`, `FeatureRegistry`, folder conventions |
| [Response Building](docs/response-building.md) | `FeatureResultBuilder` and `HtmxFragmentResult` full API |
| [Navigation & URL](docs/navigation-url.md) | `WithNavigation`, `WithLocation`, nav OOB strategy |
| [Authorization](docs/authorization.md) | Policy-based nav filtering, endpoint auth |
| [Dialogs](docs/dialogs.md) | `WithDialog`, `ClearDialog`, modal lifecycle |
| [Toasts](docs/toasts.md) | `WithToast`, `ToastTone`, trigger timing |
| [Triggers](docs/triggers.md) | `WithTrigger`, `WithTriggerAfterSwap`, `Hx.*` constants |
| [Retarget & Reswap](docs/retarget-reswap.md) | `WithRetarget`, `WithReswap`, `Hx.Swap.*`, validation error pattern |
| [Page Title](docs/page-title.md) | Default from nav label, `WithPageTitle`, OOB update |
| [State](docs/state.md) | `partial` models, `WithState`, `StateId`, `[StateNotMap]` |
| [Forms](docs/forms.md) | `[GenerateHtmlNames]`, `[GenerateFormBinder]`, `FieldContext<T>` |
| [Validation](docs/validation.md) | `IHaveValidationResult`, FluentValidation extension |
| [List Features](docs/list-features.md) | `ListRequest<T>`, paging, sorting, search, pager, sort headers |
| [Error Handling](docs/error-handling.md) | `UseHtmxErrorHandling`, custom messages |
| [Page Shell](docs/page-shell.md) | `IFeaturePageRenderer`, `ITransientUiRenderer`, custom UI adapters |
| [Bootstrap5](docs/bootstrap5.md) | Form helpers, pager, sort headers, `BootstrapFieldOptions` |
| [Configuration](docs/configuration.md) | All options — `RazorSlicesHtmxOptions`, Bootstrap options |
| [Templates](docs/templates.md) | `rshtmx-webapp`, `rshtmx-feature` project/feature scaffolding |

## Demo

See `demo/RazorSlicesHtmx.Demo` for a reference implementation with full CRUD, paged lists, dialogs, toasts, FluentValidation, and state management.
