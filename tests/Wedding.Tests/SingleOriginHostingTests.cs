using Microsoft.AspNetCore.Http;
using Wedding.Infrastructure.Services;

namespace Wedding.Tests;

public class SingleOriginHostingTests
{
    [Theory]
    [InlineData("/")]
    [InlineData("/combo")]
    [InlineData("/customer/dashboard")]
    [InlineData("/invitation/ngay-vui")]
    [InlineData("/assets/index-test.js")]
    public void ClientRoutesAndAssetsAreAllowed(string path) => Assert.True(SingleOriginClientHosting.IsClientPath(new PathString(path)));

    [Theory]
    [InlineData("/api")]
    [InlineData("/api/missing")]
    [InlineData("/API/auth/login")]
    [InlineData("/hubs/chat")]
    [InlineData("/uploads/invitations/not-an-image")]
    [InlineData("/uploads/listings/unsafe.html")]
    [InlineData("/private/wedding.production.json")]
    [InlineData("/App_Data/invitation-uploads/photo.png")]
    [InlineData("/health/ready")]
    public void ApiAndUserFilesNeverBypassControllersOrBecomeSpaHtml(string path) => Assert.False(SingleOriginClientHosting.IsClientPath(new PathString(path)));
}
