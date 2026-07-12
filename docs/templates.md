# Templates

## Installation

```bash
dotnet new install RazorSlicesHtmx.Templates
```

Specific version:

```bash
dotnet new install RazorSlicesHtmx.Templates::1.0.0
```

---

## Project template — `rshtmx-webapp`

Scaffolds a complete web application with sidebar navigation, HTMX error handling, and optional Bootstrap5/FluentValidation integration.

```bash
# Full stack (Bootstrap5 + FluentValidation)
dotnet new rshtmx-webapp -n MyApp

# Core only
dotnet new rshtmx-webapp -n MyApp --bootstrap5 false --fluentValidation false
```

| Parameter | Description | Default |
|-----------|-------------|---------|
| `-n`, `--name` | Project name | `RshtmxApp` |
| `--bootstrap5` | Include Bootstrap5 adapter | `true` |
| `--fluentValidation` | Include FluentValidation | `true` |
| `--framework` | Target framework | `net10.0` |

### Included AI development support

The generated project includes:

- `.github/copilot-instructions.md` — project conventions for Copilot
- `.github/prompts/create-feature.prompt.md` — scaffold a new feature module
- `.github/prompts/add-list.prompt.md` — add a list page
- `.github/prompts/add-form.prompt.md` — add a form
- `.github/prompts/add-endpoint.prompt.md` — add an HTMX endpoint

---

## Feature module template — `rshtmx-feature`

Scaffolds a feature module into an existing project:

```bash
dotnet new rshtmx-feature -n Products --entity Product --rootNamespace MyApp --route /products --order 200
```

Generated structure:

```
Products/
├── Endpoints/ProductsEndpoints.cs
├── Models/
│   ├── ProductRowModel.cs
│   ├── ProductsListModel.cs
│   └── ProductUpsertRequest.cs
├── Services/ProductsContentService.cs
└── Slices/
    ├── _FeaturePage.cshtml
    ├── _ListPage.cshtml
    ├── _CreatePage.cshtml
    └── _ViewImports.cshtml
```

| Parameter | Description | Default |
|-----------|-------------|---------|
| `-n`, `--name` | Feature name (e.g. `Products`) | `MyFeature` |
| `--entity` | Entity name (e.g. `Product`) | Same as feature name |
| `--rootNamespace` | Root namespace of the project | `MyApp` |
| `--route` | Base route (e.g. `/products`) | `/my-feature-route` |
| `--order` | Navigation order (lower = first) | `100` |

After scaffolding, ensure the feature assembly is included in `FeatureRegistry.Discover()`.

## See also

- [Getting Started](getting-started.md)
- [Feature Modules](feature-modules.md)
