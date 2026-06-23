using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using RazorSlices;
using RazorSlicesHtmx.AspNetCore.Features;
using RazorSlicesHtmx.AspNetCore.Infrastructure;
using RazorSlicesHtmx.AspNetCore.Models;
using RazorSlicesHtmx.AspNetCore.Options;
using RazorSlicesHtmx.AspNetCore.Rendering;
using RazorSlicesHtmx.AspNetCore.Slices;

namespace RazorSlicesHtmx.AspNetCore.Results;

public sealed class FeatureResultBuilder(
    IHttpContextAccessor httpContextAccessor,
    IServiceProvider services,
    IFeaturePageRenderer pageRenderer,
    ITransientUiRenderer transientUiRenderer,
    IOptions<RazorSlicesHtmxOptions> options)
{
    private readonly IFeaturePageRenderer _pageRenderer = pageRenderer;
    private readonly ITransientUiRenderer _transientUiRenderer = transientUiRenderer;
    private readonly RazorSlicesHtmxOptions _options = options.Value;
    private FeatureRegistry Features => services.GetRequiredService<FeatureRegistry>();

    public HttpRequest Request =>
        httpContextAccessor.HttpContext?.Request ??
        throw new InvalidOperationException("No active HttpContext is available for feature result creation.");

    public Builder Create(PageDefinition page, RazorSlice detail) => new(this, page, detail);

    public RazorSlice CreateDialog(RazorSlice content) => _transientUiRenderer.RenderDialog(content);

    public sealed class Builder(FeatureResultBuilder owner, PageDefinition page, RazorSlice detail)
    {
        private readonly List<RazorSlice> _directOobParts = [];
        private readonly List<(string Selector, RazorSlice Content, string Swap)> _targetedOobParts = [];
        private readonly List<string> _triggers = [];
        private readonly List<ToastModel> _toasts = [];
        private RazorSlice? _htmxFragment;
        private string? _navigationUrl;
        private bool _replaceNavigation = true;
        private string? _locationUrl;
        private bool _replaceLocation = true;

        public Builder AsFragment(RazorSlice fragment)
        {
            _htmxFragment = fragment;
            return this;
        }

        public Builder WithOob(RazorSlice part)
        {
            _directOobParts.Add(part);
            return this;
        }

        public Builder WithOob(IEnumerable<RazorSlice> parts)
        {
            _directOobParts.AddRange(parts);
            return this;
        }

        public Builder WithOob(string selector, RazorSlice content, string? swap = null)
        {
            _targetedOobParts.Add((selector, content, swap ?? owner._options.DefaultOobSwap));
            return this;
        }

        public Builder WithDialog(RazorSlice content) =>
            WithOob(owner._options.DialogHostSelector, owner.CreateDialog(content));

        public Builder ClearDialog() =>
            WithOob(owner._options.DialogHostSelector, _Empty.Create());

        public Builder WithToast(string message, string title = "Saved", string tone = "success")
        {
            _toasts.Add(new ToastModel(title, message, tone));
            return this;
        }

        public Builder WithTrigger(string eventName)
        {
            if (string.IsNullOrWhiteSpace(eventName))
            {
                throw new ArgumentException("Trigger name is required.", nameof(eventName));
            }

            _triggers.Add(eventName);
            return this;
        }

        public Builder WithNavigation(string url, bool replace = true)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                throw new ArgumentException("Navigation URL is required.", nameof(url));
            }

            _navigationUrl = url;
            _replaceNavigation = replace;
            return this;
        }

        public Builder WithLocation(string url, bool replace = true)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                throw new ArgumentException("Location URL is required.", nameof(url));
            }

            _locationUrl = url;
            _replaceLocation = replace;
            return this;
        }

        public IResult Build()
        {
            var request = owner.Request;
            var shellContext = owner.Features.CreateShellContext(page, detail);

            if (!request.IsHtmxRequest())
            {
                if (!string.IsNullOrWhiteSpace(_locationUrl))
                {
                    return Microsoft.AspNetCore.Http.Results.Redirect(_locationUrl);
                }

                if (!string.IsNullOrWhiteSpace(_navigationUrl))
                {
                    return Microsoft.AspNetCore.Http.Results.Redirect(_navigationUrl);
                }

                return owner._pageRenderer.RenderPage(shellContext);
            }

            if (!string.IsNullOrWhiteSpace(_locationUrl))
            {
                var locationPayload = BuildLocationPayload(owner._options, _locationUrl, _replaceLocation, _toasts);
                return new HeaderResult(detail, "HX-Location", locationPayload);
            }

            var primary = _htmxFragment ?? detail;
            var htmxBuilder = HtmxFragmentResult.Create(primary);

            if (_htmxFragment is null)
            {
                htmxBuilder.WithOob(owner._pageRenderer.RenderNavigation(shellContext));
            }

            foreach (var part in _directOobParts)
            {
                htmxBuilder.WithOob(part);
            }

            foreach (var toast in _toasts)
            {
                htmxBuilder.WithOob(owner._options.ToastHostSelector, owner._transientUiRenderer.RenderToast(toast), owner._options.DefaultOobSwap);
            }

            foreach (var targeted in _targetedOobParts)
            {
                htmxBuilder.WithOob(targeted.Selector, targeted.Content, targeted.Swap);
            }

            foreach (var trigger in _triggers)
            {
                htmxBuilder.WithTrigger(trigger);
            }

            var result = htmxBuilder.Build();

            if (string.IsNullOrWhiteSpace(_navigationUrl))
            {
                return result;
            }

            return new NavigationResult(result, _navigationUrl, _replaceNavigation);
        }

        private static string BuildLocationPayload(
            RazorSlicesHtmxOptions options,
            string url,
            bool replace,
            IReadOnlyCollection<ToastModel> toasts)
        {
            var headers = toasts.Count == 0
                ? null
                : new Dictionary<string, string>
                {
                    [TransientUiHeaders.FeatureToast] = JsonSerializer.Serialize(toasts)
                };

            var payload = new
            {
                path = url,
                target = options.DetailTargetSelector,
                swap = options.DefaultOobSwap,
                headers,
                replace = replace ? url : null,
                push = replace ? null : url
            };

            return JsonSerializer.Serialize(payload, new JsonSerializerOptions
            {
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            });
        }

        private sealed class HeaderResult(IResult inner, string headerName, string headerValue) : IResult
        {
            public Task ExecuteAsync(HttpContext httpContext)
            {
                httpContext.Response.Headers.Append(headerName, headerValue);
                return inner.ExecuteAsync(httpContext);
            }
        }

        private sealed class NavigationResult(IResult inner, string url, bool replace) : IResult
        {
            public Task ExecuteAsync(HttpContext httpContext)
            {
                httpContext.Response.Headers.Append(replace ? "HX-Replace-Url" : "HX-Push-Url", url);
                return inner.ExecuteAsync(httpContext);
            }
        }
    }
}