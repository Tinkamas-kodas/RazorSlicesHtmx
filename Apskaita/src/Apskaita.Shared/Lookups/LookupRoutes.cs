namespace Apskaita.Shared.Lookups;

/// <summary>
/// Absolute route paths of the shared entity lookups — the single source for <see cref="LookupEndpoints"/>
/// and for every form pointing a <c>LookupSelect</c> at them. They are app-wide (not feature-relative),
/// so forms use these constants instead of <c>FeatureUri</c>.
/// </summary>
public static class LookupRoutes
{
    public const string Group = "/lookup";
    public const string Projects = "/lookup/projects";
    public const string ProjectsSelected = "/lookup/projects/selected";
    public const string Clients = "/lookup/clients";
    public const string ClientsSelected = "/lookup/clients/selected";
}
