using RazorSlicesHtmx.AspNetCore.Models;

namespace Apskaita.Shared;

public sealed record AdminNavItem(
    string Group,
    string Key,
    string Label,
    string Route,
    int Order = 100,
    string? AuthorizationPolicy = null)
    : NavigationItem(Key, Label, Route, Order, AuthorizationPolicy);