using RazorSlicesHtmx.AspNetCore.Models;

namespace RazorSlicesHtmx.AspNetCore.Features;

public interface IFeatureModule
{
    PageDefinition Page { get; }

    void MapEndpoints(WebApplication app);
}