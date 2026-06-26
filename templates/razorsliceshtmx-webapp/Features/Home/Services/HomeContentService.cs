using RazorSlicesHtmx.AspNetCore.Models;
using RshtmxApp.Features.Home.Slices;

namespace RshtmxApp.Features.Home.Services;

public sealed class HomeContentService
{
    public NavigationRouteItem CreateNavigationItem() => new(
        "home",
        "Home",
        "/",
        new PageDefinition(() => _HomePage.Create()),
        Order: 0);
}
