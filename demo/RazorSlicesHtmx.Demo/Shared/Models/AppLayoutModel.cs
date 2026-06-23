using RazorSlicesHtmx.AspNetCore.Models;

namespace razr_slices_htmx2.Shared.Models;

public sealed record AppLayoutModel(
    string Title,
    string ActiveNavKey,
    IReadOnlyList<FeatureMetadata> NavigationItems);