namespace RshtmxApp.Shared.Routing;

public interface IFeatureUri
{
    string Base { get; }
    string Uri(string segment);
}

public sealed class HttpFeatureUri(IHttpContextAccessor httpContextAccessor) : IFeatureUri
{
    public string Base => GetFeatureBase(httpContextAccessor.HttpContext?.Request.Path.Value);

    public string Uri(string segment)
    {
        if (string.IsNullOrWhiteSpace(segment))
        {
            return string.IsNullOrEmpty(Base) ? "/" : Base;
        }

        if (segment.StartsWith('/'))
        {
            return segment;
        }

        var basePath = Base;
        return string.IsNullOrEmpty(basePath)
            ? $"/{segment.TrimStart('/')}"
            : $"{basePath.TrimEnd('/')}/{segment.TrimStart('/')}";
    }

    private static string GetFeatureBase(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || path == "/")
        {
            return string.Empty;
        }

        var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0)
        {
            return string.Empty;
        }

        if (segments.Length >= 2)
        {
            var last = segments[^1];
            if (last.Equals("list", StringComparison.OrdinalIgnoreCase) ||
                last.Equals("create", StringComparison.OrdinalIgnoreCase))
            {
                return "/" + string.Join('/', segments[..^1]);
            }
        }

        if (segments.Length >= 3)
        {
            var parent = segments[^2];
            if (parent.Equals("edit", StringComparison.OrdinalIgnoreCase) ||
                parent.Equals("delete", StringComparison.OrdinalIgnoreCase))
            {
                return "/" + string.Join('/', segments[..^2]);
            }
        }

        return "/" + segments[0];
    }
}
