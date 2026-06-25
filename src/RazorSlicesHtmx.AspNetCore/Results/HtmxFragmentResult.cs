using Microsoft.AspNetCore.Html;
using RazorSlices;
using RazorSlicesHtmx.AspNetCore.Htmx;
using RazorSlicesHtmx.AspNetCore.Models;
using RazorSlicesHtmx.AspNetCore.Slices;

namespace RazorSlicesHtmx.AspNetCore.Results;

public static class HtmxFragmentResult
{
    public static Builder Create(RazorSlice primary) => new(primary);

    public sealed class Builder(RazorSlice primary)
    {
        private readonly List<RazorSlice> _oobParts = [];
        private readonly List<IHtmlContent> _rawOobParts = [];
        private readonly List<string> _triggers = [];
        private string? _retarget;
        private string? _reswap;

        public Builder WithOob(string selector, RazorSlice content, string swap = HtmxSwap.InnerHtml)
        {
            if (string.IsNullOrWhiteSpace(selector))
            {
                throw new ArgumentException("OOB selector is required.", nameof(selector));
            }

            var resolvedSwap = string.IsNullOrWhiteSpace(swap) ? HtmxSwap.InnerHtml : swap;
            _oobParts.Add(HtmxOob.Create(new HtmxOobModel(selector, resolvedSwap, content)));
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

        public Builder WithOob(params RazorSlice[] parts)
        {
            _oobParts.AddRange(parts);
            return this;
        }

        public Builder WithOob(IEnumerable<RazorSlice> parts)
        {
            _oobParts.AddRange(parts);
            return this;
        }

        public Builder WithRawOob(IHtmlContent raw)
        {
            _rawOobParts.Add(raw);
            return this;
        }

        public Builder WithRawOob(IEnumerable<IHtmlContent> parts)
        {
            _rawOobParts.AddRange(parts);
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

        public IResult Build()
        {
            RazorSlice fragment = _oobParts.Count == 0 && _rawOobParts.Count == 0
                ? primary
                : HtmxFragment.Create(new HtmxFragmentModel(
                    primary,
                    _oobParts,
                    _rawOobParts.Count > 0 ? _rawOobParts : null));

            if (_triggers.Count == 0 && _retarget is null && _reswap is null)
            {
                return fragment;
            }

            return new HeaderDecoratedResult(fragment, _triggers, _retarget, _reswap);
        }

        private sealed class HeaderDecoratedResult(
            RazorSlice fragment,
            IReadOnlyCollection<string> triggers,
            string? retarget,
            string? reswap) : IResult
        {
            public Task ExecuteAsync(HttpContext httpContext)
            {
                if (triggers.Count > 0)
                {
                    httpContext.Response.Headers.Append("HX-Trigger", string.Join(",", triggers));
                }

                if (retarget is not null)
                {
                    httpContext.Response.Headers.Append("HX-Retarget", retarget);
                }

                if (reswap is not null)
                {
                    httpContext.Response.Headers.Append("HX-Reswap", reswap);
                }

                return ((IResult)fragment).ExecuteAsync(httpContext);
            }
        }
    }
}