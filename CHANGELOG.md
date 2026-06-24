# Changelog

All notable changes to this project will be documented in this file.

## 0.1.0

Initial public package split and documentation baseline.

Included in `0.1.0`:

### `RazorSlicesHtmx.AspNetCore`

- feature discovery and registration through `IFeatureModule`, `PageDefinition`, `FeatureMetadata`, and `FeatureRegistry`
- HTMX result composition through `FeatureResultBuilder`
- OOB fragments, triggers, location/navigation responses, and `_Empty`
- minimal shell context through `FeatureShellContext`
- app-facing contracts `IFeaturePageRenderer` and `ITransientUiRenderer`

### `RazorSlicesHtmx.Bootstrap5`

- Bootstrap dialog renderer
- Bootstrap toast renderer
- Bootstrap validation message partial
- Bootstrap-specific helper extensions
- Bootstrap fallback defaults registered through `PostConfigure<RazorSlicesHtmxOptions>`

### `RazorSlicesHtmx.FluentValidation`

- validation result view contracts
- helper extensions for rendering validation state
- mapping helpers from `FluentValidation.Results.ValidationResult`
