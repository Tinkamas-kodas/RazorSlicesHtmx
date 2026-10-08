namespace Apskaita.Web.Layout.Models;

public sealed record AppPageShellModel(
    AppLayoutModel Layout,
    RazorSlice Detail);
