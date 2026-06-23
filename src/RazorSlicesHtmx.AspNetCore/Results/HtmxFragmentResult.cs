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
        private readonly List<string> _triggers = [];

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

        public IResult Build()
        {
            RazorSlice fragment = _oobParts.Count == 0
                ? primary
                : HtmxFragment.Create(new HtmxFragmentModel(primary, _oobParts));

            if (_triggers.Count == 0)
            {
                return fragment;
            }

            return new TriggeredSliceResult(fragment, _triggers);
        }

        private sealed class TriggeredSliceResult(RazorSlice fragment, IReadOnlyCollection<string> triggers) : IResult
        {
            public Task ExecuteAsync(HttpContext httpContext)
            {
                httpContext.Response.Headers.Append("HX-Trigger", string.Join(",", triggers));
                return ((IResult)fragment).ExecuteAsync(httpContext);
            }
        }
    }
}