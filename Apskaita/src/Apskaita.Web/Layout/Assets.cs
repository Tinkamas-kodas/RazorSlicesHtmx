namespace Apskaita.Web.Layout;

/// <summary>
/// Cache-busting tag for app-owned static assets (icon sprite, stylesheets, scripts). Bump it
/// whenever a file under <c>wwwroot</c> changes so browsers drop their cached copies.
/// Compile-time constant — nothing is computed at runtime.
/// </summary>
public static class Assets
{
    public const string Version = "20261012-3";
}
