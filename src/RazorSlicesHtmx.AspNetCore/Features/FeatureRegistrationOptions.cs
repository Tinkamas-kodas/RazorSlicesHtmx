namespace RazorSlicesHtmx.AspNetCore.Features;

public sealed class FeatureRegistrationOptions
{
    public List<FeatureRegistration> Registrations { get; } = [];
}

public sealed record FeatureRegistration(Type FeatureType, string? RouteGroupPrefix = null);