using Microsoft.AspNetCore.Http;

namespace Wedding.WebApi;

/// <summary>Default-deny indexing for app/API routes while leaving public catalog routes crawlable.</summary>
public static class SearchRobotsPolicy
{
    private static readonly HashSet<string> PublicExactPaths = new(StringComparer.OrdinalIgnoreCase)
    {
        "/",
        "/browse",
        "/vendors",
        "/combo",
        "/cam-nang",
        "/wiki",
        "/invitation"
    };

    private static readonly HashSet<string> StaticFileExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".js", ".css", ".map", ".svg", ".png", ".jpg", ".jpeg", ".webp", ".gif",
        ".ico", ".woff", ".woff2", ".ttf", ".otf", ".eot", ".pdf", ".xml", ".txt"
    };

    public static bool ShouldReturnNoIndex(PathString requestPath)
    {
        var path = Normalize(requestPath.Value);
        if (path.Equals("/robots.txt", StringComparison.OrdinalIgnoreCase)
            || path.Equals("/sitemap.xml", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("/assets/", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("/wedding-match/", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (path.StartsWith("/uploads/", StringComparison.OrdinalIgnoreCase))
            return true;

        var lastSegment = path[(path.LastIndexOf('/') + 1)..];
        if (StaticFileExtensions.Contains(Path.GetExtension(lastSegment)))
            return false;

        if (PublicExactPaths.Contains(path))
            return false;

        var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        return !(segments.Length == 2
            && (segments[0].Equals("listings", StringComparison.OrdinalIgnoreCase)
                || segments[0].Equals("combo", StringComparison.OrdinalIgnoreCase)
                || segments[0].Equals("wiki", StringComparison.OrdinalIgnoreCase)));
    }

    public static void ApplyResponseHeader(HttpContext context)
    {
        if (ShouldReturnNoIndex(context.Request.Path))
            context.Response.Headers["X-Robots-Tag"] = "noindex, nofollow";
    }

    private static string Normalize(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || path == "/")
            return "/";

        return path.TrimEnd('/');
    }
}
