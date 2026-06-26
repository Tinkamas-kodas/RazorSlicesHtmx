namespace RshtmxApp.Shared.Models;

public sealed record AppLayoutModel(
    string Title,
    string ActiveKey,
    IReadOnlyList<NavigationItem> Navigation);
