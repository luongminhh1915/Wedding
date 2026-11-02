using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Wedding.Infrastructure.Services;

/// <summary>Serve the packaged React client without turning missing API/media URLs into HTML.</summary>
public static class SingleOriginClientHosting
{
    public static bool IsClientPath(PathString path) =>
        !path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase)
        && !path.StartsWithSegments("/hubs", StringComparison.OrdinalIgnoreCase)
        && !path.StartsWithSegments("/uploads", StringComparison.OrdinalIgnoreCase)
        && !path.StartsWithSegments("/private", StringComparison.OrdinalIgnoreCase)
        && !path.StartsWithSegments("/App_Data", StringComparison.OrdinalIgnoreCase)
        && !path.StartsWithSegments("/health", StringComparison.OrdinalIgnoreCase);

    public static IApplicationBuilder UseWeddingClientFiles(this IApplicationBuilder app) =>
        app.UseWhen(context => IsClientPath(context.Request.Path), client =>
            client.UseStaticFiles(new StaticFileOptions
            {
                OnPrepareResponse = context =>
                {
                    context.Context.Response.Headers.CacheControl = context.Context.Request.Path.StartsWithSegments("/assets")
                        ? "public,max-age=31536000,immutable" : "no-cache";
                    context.Context.Response.Headers["X-Content-Type-Options"] = "nosniff";
                }
            }));

    public static IEndpointConventionBuilder MapWeddingClient(this IEndpointRouteBuilder endpoints, string? webRoot)
    {
        var index = string.IsNullOrWhiteSpace(webRoot) ? null : Path.Combine(webRoot, "index.html");
        return endpoints.MapFallback(async context =>
        {
            if (!IsClientPath(context.Request.Path) || index == null || !File.Exists(index)
                || !(HttpMethods.IsGet(context.Request.Method) || HttpMethods.IsHead(context.Request.Method)))
            {
                context.Response.StatusCode = StatusCodes.Status404NotFound;
                return;
            }

            context.Response.ContentType = "text/html; charset=utf-8";
            context.Response.Headers.CacheControl = "no-store";
            context.Response.Headers["X-Content-Type-Options"] = "nosniff";
            context.Response.ContentLength = new FileInfo(index).Length;
            if (!HttpMethods.IsHead(context.Request.Method))
                await context.Response.SendFileAsync(index, context.RequestAborted);
        });
    }
}
