# Error Handling

`UseHtmxErrorHandling()` adds middleware that intercepts unhandled exceptions and non-success status codes for HTMX requests. Instead of a raw error page (which htmx ignores by default), it returns an error toast.

## Setup

Place before endpoint routing:

```csharp
app.UseStaticFiles();
app.UseHtmxErrorHandling(); // before features.MapEndpoints(app)
features.MapEndpoints(app);
```

## Behaviour

| Scenario | Result |
|----------|--------|
| HTMX request + unhandled exception | Catches error, responds with `show-toast` (tone: Error) |
| HTMX request + 4xx/5xx status | Responds with `show-toast` with status-appropriate message |
| Non-HTMX request | Passes through — standard ASP.NET error handling applies |

The middleware resets the response status to 200 so htmx processes the body (htmx ignores non-2xx by default).

The toast is fired via `HX-Trigger-After-Swap` so it appears after any DOM swap completes.

## Default messages

| Status | Default title | Default message |
|--------|--------------|-----------------|
| 400 | Bad Request | The request could not be processed. |
| 401 | Unauthorized | You must be logged in to perform this action. |
| 403 | Forbidden | You do not have permission to perform this action. |
| 404 | Not Found | The requested resource was not found. |
| 409 | Conflict | A conflict occurred while processing the request. |
| 422 | Validation Error | The submitted data is invalid. |
| 429 | Too Many Requests | Too many requests. Please try again later. |
| 5xx | Server Error | An unexpected error occurred. Please try again. |

## Custom messages

```csharp
app.UseHtmxErrorHandling(options =>
{
    options.FormatTitle = (statusCode, ex) => statusCode switch
    {
        404 => "Nerasta",
        _ => "Klaida"
    };

    options.FormatMessage = (statusCode, ex) => statusCode switch
    {
        404 => "Prašomas puslapis nerastas.",
        _ => "Įvyko nenumatyta klaida."
    };
});
```

Both `FormatTitle` and `FormatMessage` receive the status code and the exception (null for status-code-only errors).

## See also

- [Toasts](toasts.md)
- [Getting Started](getting-started.md)
