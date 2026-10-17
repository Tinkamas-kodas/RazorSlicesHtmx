using System.Diagnostics;

namespace Apskaita.Shared.Logging;

/// <summary>
/// Delegating handler that logs every outbound HTTP call made by the OpenIdConnect backchannel — provider
/// metadata discovery (<c>.well-known/openid-configuration</c>, JWKS) and the token exchange POST — so all
/// server-side traffic to Google/Entra is visible in the log.
/// </summary>
public sealed class ExternalCallLoggingHandler(HttpMessageHandler innerHandler, ILogger logger)
    : DelegatingHandler(innerHandler)
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var started = Stopwatch.GetTimestamp();
        var uri = request.RequestUri?.ToString() ?? "(none)";

        logger.LogInformation("--> external {Method} {Uri}", request.Method.Method, uri);

        try
        {
            var response = await base.SendAsync(request, cancellationToken);
            logger.LogInformation("<-- external {Status} {Uri} {Elapsed}",
                (int)response.StatusCode, uri, $"{Stopwatch.GetElapsedTime(started).TotalMilliseconds:F0}ms");
            return response;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "<-- external FAILED {Uri} {Error}", uri, ex.Message);
            throw;
        }
    }
}
