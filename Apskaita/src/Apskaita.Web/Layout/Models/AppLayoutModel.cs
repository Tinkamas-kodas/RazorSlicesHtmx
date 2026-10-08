using Apskaita.Web.Shared;

namespace Apskaita.Web.Layout.Models;

public sealed record AppLayoutModel(
    string Title,
    string ActiveKey,
    IReadOnlyList<AdminNavItem> Navigation);