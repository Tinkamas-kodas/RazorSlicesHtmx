using RazorSlices;

namespace RazorSlicesHtmx.Demo.Shared.Models;

public sealed record AppPageShellModel(
    AppLayoutModel Layout,
    RazorSlice Detail);