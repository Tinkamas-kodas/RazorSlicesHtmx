using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Html;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using RazorSlices;
using RazorSlicesHtmx.AspNetCore.Features;
using RazorSlicesHtmx.AspNetCore.Htmx;
using RazorSlicesHtmx.AspNetCore.Infrastructure;
using RazorSlicesHtmx.AspNetCore.Models;
using RazorSlicesHtmx.AspNetCore.Options;
using RazorSlicesHtmx.AspNetCore.Rendering;

namespace RazorSlicesHtmx.AspNetCore.Results;

public sealed class FeatureResultBuilder(
    IHttpContextAccessor httpContextAccessor,
    IServiceProvider services,
    IFeaturePageRenderer pageRenderer,
    ITransientUiRenderer transientUiRenderer,
    IOptions<RazorSlicesHtmxOptions> options,
    INavOobStrategy navOobStrategy)
{
    private readonly IFeaturePageRenderer _pageRenderer = pageRenderer;
    private readonly ITransientUiRenderer _transientUiRenderer = transientUiRenderer;
    private readonly RazorSlicesHtmxOptions _options = options.Value;
    private readonly INavOobStrategy _navOobStrategy = navOobStrategy;
    private FeatureRegistry Features => services.GetRequiredService<FeatureRegistry>();

    public HttpRequest Request =>
        httpContextAccessor.HttpContext?.Request ??
        throw new InvalidOperationException("No active HttpContext is available for feature result creation.");

    public Builder Create(NavigationItem routeItem, RazorSlice detail) => new(this, routeItem, detail);

    public RazorSlice CreateDialog(RazorSlice content) => _transientUiRenderer.RenderDialog(content);

    public sealed class Builder(FeatureResultBuilder owner, NavigationItem routeItem, RazorSlice detail)
    {
        private readonly List<RazorSlice> _directOobParts = [];
        private readonly List<(string Selector, RazorSlice Content, string Swap)> _targetedOobParts = [];
        private readonly List<IHtmlContent> _rawOobParts = [];
        private readonly List<string> _triggers = [];
        private readonly List<string> _triggersAfterSwap = [];
        private readonly List<ToastModel> _toasts = [];
        private RazorSlice? _htmxFragment;
        private RazorSlice? _workspaceDetail;
        private bool? _navOverride;
        private string? _navigationUrl;
        private bool _replaceNavigation = true;
        private string? _locationUrl;
        private bool _replaceLocation = true;
        private string? _retarget;
        private string? _reswap;
        private string? _pageTitle;

        public Builder AsFragment(RazorSlice fragment)
        {
            _htmxFragment = fragment;
            return this;
        }

        /// <summary>
        /// Explicitly controls whether the navigation OOB slice is included in this response.
        /// When not called the registered <see cref="INavOobStrategy"/> decides (default: send only on feature change).
        /// </summary>
        public Builder WithNav(bool sendNavOob = true)
        {
            _navOverride = sendNavOob;
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

        public Builder WithDialog(RazorSlice content)
        {
            WithOob(owner._options.DialogHostSelector, owner.CreateDialog(content));
            _triggersAfterSwap.Add("show-dialog");
            return this;
        }

        public Builder ClearDialog()
        {
            _triggers.Add("dialog:clear");
            return this;
        }

        public Builder WithToast(string message, string title = "Saved", ToastTone tone = ToastTone.Success)
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

        public Builder WithState(IHasState state)
        {
            ArgumentNullException.ThrowIfNull(state);
            _rawOobParts.Add(state.SerializeOob());
            owner.Request.HttpContext.RequestServices
                .GetRequiredService<HtmxStateContext>()
                .Register(state);
            return this;
        }

        /// <summary>
        /// Wraps the detail slice in a workspace using <typeparamref name="TFactory"/>.
        /// The factory determines which <c>HX-Target</c> values indicate the workspace is already
        /// in the DOM — for those the inner slice is used as fragment; otherwise the full workspace.
        /// </summary>
        public Builder WithWorkspace<TFactory>() where TFactory : BaseWorkspaceFactory
        {
            var sp = owner.Request.HttpContext.RequestServices;
            var factory = ActivatorUtilities.CreateInstance<TFactory>(sp);
            var target = owner.Request.Headers[Hx.Target].ToString();
            var (workspace, fragment) = factory.Resolve(detail, target);
            _workspaceDetail = workspace;
            _htmxFragment = fragment;
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

        public Builder WithRetarget(string selector)
        {
            if (string.IsNullOrWhiteSpace(selector))
            {
                throw new ArgumentException("Retarget selector is required.", nameof(selector));
            }

            _retarget = selector;
            return this;
        }

        public Builder WithReswap(string swapMode)
        {
            if (string.IsNullOrWhiteSpace(swapMode))
            {
                throw new ArgumentException("Reswap mode is required.", nameof(swapMode));
            }

            _reswap = swapMode;
            return this;
        }

        public Builder WithPageTitle(string title)
        {
            if (string.IsNullOrWhiteSpace(title))
            {
                throw new ArgumentException("Page title is required.", nameof(title));
            }

            _pageTitle = title;
            return this;
        }

        public async Task<IResult> BuildAsync(CancellationToken cancellationToken = default)
        {
            var request = owner.Request;
            var httpContext = request.HttpContext;
            var user = httpContext.User;
            var authService = httpContext.RequestServices.GetService<IAuthorizationService>();
            var shellContext = await owner.Features.CreateShellContextAsync(routeItem, _workspaceDetail ?? detail, user, authService, _pageTitle, cancellationToken);

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
                var locationPayload = BuildLocationPayload(owner._options, _locationUrl, _replaceLocation);
                var locationBuilder = HtmxFragmentResult.Create(detail);
                foreach (var toast in _toasts)
                {
                    var toastPayload = JsonSerializer.Serialize(new { title = toast.Title, message = toast.Message, tone = toast.Tone.ToString().ToLower() });
                    locationBuilder.WithTriggerAfterSwap("show-toast", toastPayload);
                }
                return new HeaderResult(locationBuilder.Build(), Hx.Location, locationPayload);
            }

            var primary = _htmxFragment ?? detail;
            var htmxBuilder = HtmxFragmentResult.Create(primary);

            var sendNav = _navOverride ?? owner._navOobStrategy.ShouldSendNav(request, routeItem);
            if (sendNav)
            {
                htmxBuilder.WithOob(owner._pageRenderer.RenderNavigation(shellContext));
            }

            foreach (var part in _directOobParts)
            {
                htmxBuilder.WithOob(part);
            }

            foreach (var toast in _toasts)
            {
                var payload = JsonSerializer.Serialize(new { title = toast.Title, message = toast.Message, tone = toast.Tone.ToString().ToLower() });
                htmxBuilder.WithTriggerAfterSwap("show-toast", payload);
            }

            foreach (var targeted in _targetedOobParts)
            {
                htmxBuilder.WithOob(targeted.Selector, targeted.Content, targeted.Swap);
            }

            if (_rawOobParts.Count > 0)
            {
                htmxBuilder.WithRawOob(_rawOobParts);
            }

            if (!string.IsNullOrWhiteSpace(shellContext.PageTitle))
            {
                htmxBuilder.WithRawOob(CreatePageTitleOob(shellContext.PageTitle));
            }

            foreach (var trigger in _triggers)
            {
                htmxBuilder.WithTrigger(trigger);
            }

            foreach (var trigger in _triggersAfterSwap)
            {
                htmxBuilder.WithTriggerAfterSwap(trigger);
            }

            if (_retarget is not null)
            {
                htmxBuilder.WithRetarget(_retarget);
            }

            if (_reswap is not null)
            {
                htmxBuilder.WithReswap(_reswap);
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
            bool replace)
        {
            var payload = new
            {
                path = url,
                target = options.DetailTargetSelector,
                swap = options.DefaultOobSwap,
                replace = replace ? url : null,
                push = replace ? null : url
            };

            return JsonSerializer.Serialize(payload, new JsonSerializerOptions
            {
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            });
        }

        private static IHtmlContent CreatePageTitleOob(string title)
        {
            var encoded = HtmlEncoder.Default.Encode(title);
            return new HtmlString($"<title hx-swap-oob=\"true\">{encoded}</title>");
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
                httpContext.Response.Headers.Append(replace ? Hx.ReplaceUrl : Hx.PushUrl, url);
                return inner.ExecuteAsync(httpContext);
            }
        }
    }
}