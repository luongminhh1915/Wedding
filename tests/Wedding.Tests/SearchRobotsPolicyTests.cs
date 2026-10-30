using Microsoft.AspNetCore.Http;
using Wedding.WebApi;

namespace Wedding.Tests;

public class SearchRobotsPolicyTests
{
    [Theory]
    [InlineData("/")]
    [InlineData("/browse")]
    [InlineData("/vendors/")]
    [InlineData("/combo")]
    [InlineData("/combo/template-id")]
    [InlineData("/listings/listing-id")]
    [InlineData("/cam-nang")]
    [InlineData("/wiki/ke-hoach-cuoi")]
    [InlineData("/invitation")]
    [InlineData("/assets/index.js")]
    [InlineData("/wedding-match/mark.svg")]
    [InlineData("/robots.txt")]
    [InlineData("/sitemap.xml")]
    public void PublicRoutesAndStaticResourcesDoNotReceiveNoIndex(string path) =>
        Assert.False(SearchRobotsPolicy.ShouldReturnNoIndex(new PathString(path)), path);

    [Theory]
    [InlineData("/login")]
    [InlineData("/register")]
    [InlineData("/reset-password")]
    [InlineData("/account")]
    [InlineData("/notifications")]
    [InlineData("/planner")]
    [InlineData("/customer/dashboard")]
    [InlineData("/vendor/dashboard")]
    [InlineData("/admin/dashboard")]
    [InlineData("/invitation/private-couple-slug")]
    [InlineData("/private-couple-slug")]
    [InlineData("/uploads/private/photo.jpg")]
    [InlineData("/api/auth/login")]
    [InlineData("/not-found")]
    public void PrivateApiAndUnknownRoutesReceiveNoIndex(string path) =>
        Assert.True(SearchRobotsPolicy.ShouldReturnNoIndex(new PathString(path)), path);

    [Theory]
    [InlineData("/", null)]
    [InlineData("/browse", null)]
    [InlineData("/customer/dashboard", "noindex, nofollow")]
    [InlineData("/invitation/couple-secret-slug", "noindex, nofollow")]
    [InlineData("/api/auth/login", "noindex, nofollow")]
    [InlineData("/sitemap.xml", null)]
    public void ResponseHeaderMatchesRoutePrivacy(string path, string? expected)
    {
        var context = new DefaultHttpContext();
        context.Request.Path = new PathString(path);

        SearchRobotsPolicy.ApplyResponseHeader(context);

        Assert.Equal(expected, context.Response.Headers["X-Robots-Tag"].FirstOrDefault());
    }
}
