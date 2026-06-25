namespace RazorSlicesHtmx.Demo.Shared.Models;

public sealed record AppLayoutModel(
    string Title,
    string ActiveNavKey,
    IReadOnlyList<NavigationItem> NavigationItems);