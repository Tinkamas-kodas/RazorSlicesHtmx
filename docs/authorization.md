# Authorization

Authorization uses the standard ASP.NET Core `IAuthorizationService`. Each `NavigationItem` can declare an `AuthorizationPolicy` that gates both navigation visibility and endpoint access.

## Declaring a policy on a navigation item

```csharp
new NavigationItem("admin-users", "Users", "/admin/users",
    AuthorizationPolicy: "AdminOnly")

new NavigationGroupItem("admin", "Administration",
[
    new NavigationItem("users", "Users", "/admin/users", AuthorizationPolicy: "AdminOnly"),
    new NavigationItem("roles", "Roles", "/admin/roles", AuthorizationPolicy: "AdminOnly")
],
AuthorizationPolicy: "AdminOnly")
```

## How it works

1. **Navigation filtering** — `FeatureRegistry.CreateShellContext` (called inside `Build()`) recursively filters the navigation tree using `IAuthorizationService.AuthorizeAsync`. Items whose policy check fails are excluded. Groups with no remaining children are excluded entirely.

2. **Endpoint authorization** — feature modules add `.RequireAuthorization(policy)` to their endpoint registrations as normal. Navigation filtering and endpoint authorization are independent.

3. **No auth configured** — when `IAuthorizationService` is not registered or `HttpContext.User` is null, all items pass through (backward compatible with apps that don't use auth).

`Build()` handles filtering automatically:

```csharp
return Result.Page(detail)
    .Build(); // auth filtering happens here
```

## Endpoint authorization

```csharp
public override void MapEndpoints(WebApplication app)
{
    app.MapGet("/admin/users", ...)
        .RequireAuthorization("AdminOnly");

    app.MapPost("/admin/users", ...)
        .RequireAuthorization("AdminOnly");
}
```

## See also

- [Feature Modules](feature-modules.md)
- [Response Building](response-building.md)
