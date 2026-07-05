using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace Atrium.ServiceDefaults;

/// <summary>
/// Shared API-docs viewer for the backend services, so the Redoc page isn't copy-pasted (and
/// version-drifted) into every host. Pairs with each host's built-in OpenAPI document endpoint.
/// </summary>
public static class ApiDocsExtensions
{
    /// <summary>
    /// Maps <c>/docs</c>, rendering the host's <c>/openapi/v1.json</c> document with Redoc
    /// (standalone, from CDN). The route is mapped at the app root — outside any bearer-only group —
    /// and <c>AllowAnonymous</c>, so the morning live-check can reach it without a token. Callers wrap
    /// it (together with <c>MapOpenApi</c>) in a Development-only block; the viewer is a UI
    /// convenience, not part of the API, so it is excluded from the OpenAPI document itself.
    /// </summary>
    public static WebApplication MapAtriumApiDocs(this WebApplication app, string title)
    {
        app.MapGet(
                "/docs",
                () =>
                    Results.Content(
                        $$"""
                        <!DOCTYPE html>
                        <html>
                          <head>
                            <title>{{title}}</title>
                            <meta charset="utf-8" />
                            <meta name="viewport" content="width=device-width, initial-scale=1" />
                            <style>body { margin: 0; padding: 0; }</style>
                          </head>
                          <body>
                            <redoc spec-url="/openapi/v1.json"></redoc>
                            <script src="https://cdn.redoc.ly/redoc/v2.5.0/bundles/redoc.standalone.js"></script>
                          </body>
                        </html>
                        """,
                        "text/html"
                    )
            )
            .AllowAnonymous()
            .ExcludeFromDescription();
        return app;
    }
}
