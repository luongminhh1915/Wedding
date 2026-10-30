using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Wedding.WebApi.Controllers;

namespace Wedding.Tests;

public class DevSeedEndpointTests
{
    [Fact]
    public void LegacySeedEndpointIsGoneAndDoesNotAdvertiseDemoCredentials()
    {
        // No database/initializer can be invoked through this retired HTTP route.
        var response = Assert.IsType<ObjectResult>(new DevSeedController().SeedDatabase());
        Assert.Equal(410, response.StatusCode);
        var body = JsonSerializer.Serialize(response.Value);
        Assert.DoesNotContain("123456", body);
        Assert.DoesNotContain("@wedding.com", body);
        Assert.DoesNotContain("password", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("testAccounts", body);
    }
}
