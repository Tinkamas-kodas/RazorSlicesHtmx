using RazorSlices;

namespace RazorSlicesHtmx.AspNetCore.Results;

/// <summary>
/// Base class for feature workspace factories.
/// Encapsulates workspace slice construction and determines whether the workspace
/// or only the inner slice should be used as the HTMX fragment based on <c>HX-Target</c>.
/// </summary>
public abstract class BaseWorkspaceFactory
{
    private readonly string[] _innerTargets;

    /// <param name="innerTargets">
    /// <c>HX-Target</c> values that indicate the workspace is already in the DOM
    /// (e.g. <c>"feature-details"</c>). When the request targets one of these,
    /// only the inner slice is used as the HTMX fragment.
    /// </param>
    protected BaseWorkspaceFactory(params string[] innerTargets)
        => _innerTargets = innerTargets;

    /// <summary>Wraps <paramref name="inner"/> in the workspace slice.</summary>
    protected abstract RazorSlice Wrap(RazorSlice inner);

    internal (RazorSlice Workspace, RazorSlice Fragment) Resolve(RazorSlice inner, string htmxTarget)
    {
        var workspace = Wrap(inner);
        var isInner = _innerTargets.Contains(htmxTarget, StringComparer.OrdinalIgnoreCase);
        return (workspace, isInner ? inner : workspace);
    }
}
