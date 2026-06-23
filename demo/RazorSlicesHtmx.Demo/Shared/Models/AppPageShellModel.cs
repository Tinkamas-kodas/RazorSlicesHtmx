using RazorSlices;

namespace razr_slices_htmx2.Shared.Models;

public sealed record AppPageShellModel(
    AppLayoutModel Layout,
    RazorSlice Detail);