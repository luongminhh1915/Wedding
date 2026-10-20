using System.Security.Claims;
using Wedding.Infrastructure.Services;

namespace Wedding.Tests;

public class JwtSessionPolicyTests
{
    private static ClaimsPrincipal Actor(params Claim[] claims) => new(new ClaimsIdentity(claims, "Bearer"));

    [Fact]
    public void ExactlyOneCurrentRoleAndVersionIsRequired()
    {
        var current = Actor(new(ClaimTypes.Role, "Customer"), new("security_version", "0"));
        Assert.True(JwtSessionPolicy.Matches(current, "Customer", 0));
        Assert.False(JwtSessionPolicy.Matches(current, "Customer", 1));
        Assert.False(JwtSessionPolicy.Matches(current, "VendorOwner", 0));
        Assert.False(JwtSessionPolicy.Matches(null, "Customer", 0));
        Assert.False(JwtSessionPolicy.Matches(new ClaimsPrincipal(), "Customer", 0));
        Assert.False(JwtSessionPolicy.Matches(Actor(new Claim(ClaimTypes.Role, "Customer")), "Customer", 0));
        Assert.False(JwtSessionPolicy.Matches(Actor(new Claim("security_version", "0")), "Customer", 0));
        Assert.False(JwtSessionPolicy.Matches(Actor(new(ClaimTypes.Role, "Customer"), new(ClaimTypes.Role, "SuperAdmin"), new("security_version", "0")), "Customer", 0));
        Assert.False(JwtSessionPolicy.Matches(Actor(new(ClaimTypes.Role, "Customer"), new("security_version", "0"), new("security_version", "0")), "Customer", 0));
    }

    [Theory]
    [InlineData("")]
    [InlineData("-1")]
    [InlineData("00")]
    [InlineData(" 0")]
    [InlineData("0.0")]
    [InlineData("NaN")]
    public void MalformedVersionsNeverAuthorize(string value) =>
        Assert.False(JwtSessionPolicy.Matches(Actor(new(ClaimTypes.Role, "Customer"), new("security_version", value)), "Customer", 0));
}
