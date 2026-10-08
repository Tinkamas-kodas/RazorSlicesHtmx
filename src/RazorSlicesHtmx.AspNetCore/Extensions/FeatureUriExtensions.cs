using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Html;

namespace RazorSlicesHtmx.AspNetCore.Extensions;

public static class FeatureUriExtensions
{
    public static IHtmlContent HistoryBack(this string fallbackUri)
    {
        var safeFallback = JavaScriptEncoder.Default.Encode(fallbackUri);
        return new HtmlString($"if (window.history.length > 1) {{ window.history.back(); }} else {{ window.location.href = '{safeFallback}'; }}");
    }
}
