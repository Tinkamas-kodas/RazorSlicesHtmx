using RazorSlices;

namespace RazorSlicesHtmx.AspNetCore.Results;

/// <summary>
/// Base class for feature workspace factories.
/// Encapsulates workspace slice construction and determines whether the workspace
/// or only the inner slice should be used as the HTMX fragment based on <c>HX-Target</c>.
/// </summary>
public abstract class BaseWorkspaceFactory
{
    /// <summary>Wraps <paramref name="inner"/> in the workspace slice.</summary>
    protected abstract RazorSlice Wrap(RazorSlice inner);

    internal (RazorSlice Workspace, RazorSlice Fragment) Resolve(RazorSlice inner, string htmxTarget, string featureDivId)
    {
        var workspace = Wrap(inner);
        var isInner = TargetsMatch(htmxTarget, featureDivId);
        return (workspace, isInner ? inner : workspace);
    }

    private static bool TargetsMatch(string actual, string expected)
    {
        if (string.IsNullOrWhiteSpace(actual) || string.IsNullOrWhiteSpace(expected))
        {
            return false;
        }

        static string NormalizeTarget(string value)
        {
            var normalized = value.Trim();
            var hashIndex = normalized.LastIndexOf('#');
            if (hashIndex >= 0 && hashIndex < normalized.Length - 1)
            {
                normalized = normalized[(hashIndex + 1)..];
            }

            return normalized.TrimStart('#');
        }

        return string.Equals(NormalizeTarget(actual), NormalizeTarget(expected), StringComparison.OrdinalIgnoreCase);
    }
}
