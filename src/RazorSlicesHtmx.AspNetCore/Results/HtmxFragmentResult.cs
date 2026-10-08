using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Html;
using RazorSlices;
using RazorSlicesHtmx.AspNetCore.Htmx;
using RazorSlicesHtmx.AspNetCore.Models;
using RazorSlicesHtmx.AspNetCore.Slices;

namespace RazorSlicesHtmx.AspNetCore.Results;

public static class HtmxFragmentResult
{
    public static Builder Create(RazorSlice primary) => new(primary);

    public sealed class Builder(RazorSlice primary) : IResult
    {
        private readonly List<RazorSlice> _oobParts = [];
        private readonly List<IHtmlContent> _rawOobParts = [];
        private readonly List<string> _triggers = [];
        private readonly List<(string Name, string Json)> _jsonTriggers = [];
        private string? _retarget;
        private string? _reswap;

        public Builder WithOob(string selector, RazorSlice content, string swap = Hx.Swap.InnerHtml)
        {
            if (string.IsNullOrWhiteSpace(selector))
            {
                throw new ArgumentException("OOB selector is required.", nameof(selector));
            }

            var resolvedSwap = string.IsNullOrWhiteSpace(swap) ? Hx.Swap.InnerHtml : swap;
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

        public Builder WithTrigger(string eventName, string jsonValue)
        {
            if (string.IsNullOrWhiteSpace(eventName))
            {
                throw new ArgumentException("Trigger name is required.", nameof(eventName));
            }

            if (string.IsNullOrWhiteSpace(jsonValue))
            {
                throw new ArgumentException("JSON value is required.", nameof(jsonValue));
            }

            _jsonTriggers.Add((eventName, jsonValue));
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

        public Task ExecuteAsync(HttpContext httpContext) => Build().ExecuteAsync(httpContext);

        public IResult Build()
        {
            RazorSlice fragment = _oobParts.Count == 0 && _rawOobParts.Count == 0
                ? primary
                : HtmxFragment.Create(new HtmxFragmentModel(
                    primary,
                    _oobParts,
                    _rawOobParts.Count > 0 ? _rawOobParts : null));

            if (_triggers.Count == 0 && _jsonTriggers.Count == 0 && _retarget is null && _reswap is null)
            {
                return fragment;
            }

            return new HeaderDecoratedResult(fragment, _triggers, _jsonTriggers, _retarget, _reswap);
        }

        private sealed class HeaderDecoratedResult(
            RazorSlice fragment,
            IReadOnlyCollection<string> triggers,
            IReadOnlyCollection<(string Name, string Json)> jsonTriggers,
            string? retarget,
            string? reswap) : IResult
        {
            public Task ExecuteAsync(HttpContext httpContext)
            {
                if (triggers.Count > 0 || jsonTriggers.Count > 0)
                {
                    httpContext.Response.Headers.Append(Hx.Trigger, BuildTriggerHeader(triggers, jsonTriggers));
                }

                if (retarget is not null)
                {
                    httpContext.Response.Headers.Append(Hx.Retarget, retarget);
                }

                if (reswap is not null)
                {
                    httpContext.Response.Headers.Append(Hx.Reswap, reswap);
                }

                return ((IResult)fragment).ExecuteAsync(httpContext);
            }

            private static string BuildTriggerHeader(
                IReadOnlyCollection<string> simpleTriggers,
                IReadOnlyCollection<(string Name, string Json)> jsonTriggers)
            {
                if (jsonTriggers.Count == 0)
                {
                    return string.Join(",", simpleTriggers);
                }

                using var ms = new MemoryStream();
                using (var writer = new Utf8JsonWriter(ms))
                {
                    writer.WriteStartObject();
                    foreach (var t in simpleTriggers)
                    {
                        // htmx 4 trigger dispatcher expects a non-null detail payload.
                        writer.WritePropertyName(t);
                        writer.WriteStartObject();
                        writer.WriteEndObject();
                    }
                    foreach (var (name, json) in jsonTriggers)
                    {
                        writer.WritePropertyName(name);
                        writer.WriteRawValue(json);
                    }
                    writer.WriteEndObject();
                }
                return Encoding.UTF8.GetString(ms.ToArray());
            }
        }
    }
}