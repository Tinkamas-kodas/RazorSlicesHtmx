namespace Apskaita.Shared;

/// <summary>
/// Authorization policy names shared between the app's authentication wiring (Apskaita.Web) and the
/// feature assemblies (Apskaita.Feature.Admin, Apskaita.Web). Values must match the policies
/// registered in <c>Program.cs</c>.
/// </summary>
public static class AuthPolicies
{
    /// <summary>Requires the admin role: all registries under <c>/admin/*</c> plus role assignment.</summary>
    public const string Admin = "Admin";

    /// <summary>Requires the user or admin role: operational features (time tracking, invoices).</summary>
    public const string Operational = "Operational";
}
