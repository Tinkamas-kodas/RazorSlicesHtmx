using RazorSlices;

namespace RshtmxApp.Shared.Models;

public sealed record AppPageShellModel(
    AppLayoutModel Layout,
    RazorSlice Detail);
