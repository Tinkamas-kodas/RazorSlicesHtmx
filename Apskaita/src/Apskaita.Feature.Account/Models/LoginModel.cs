namespace Apskaita.Feature.Account.Models;

/// <summary>
/// View model for the login page. Each flag reflects whether the matching provider's credentials are
/// configured, so only usable providers are offered (an unconfigured provider would 400 on challenge).
/// </summary>
/// <param name="EntraEnabled">Microsoft Entra ID client id is configured.</param>
/// <param name="GoogleOauthEnabled">Google client id is configured.</param>
public sealed record LoginModel(bool EntraEnabled, bool GoogleOauthEnabled);
