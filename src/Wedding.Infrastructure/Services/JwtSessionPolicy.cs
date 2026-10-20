using System.Globalization;
using System.Security.Claims;

namespace Wedding.Infrastructure.Services;

/// <summary>Fail closed for pre-version tokens, duplicated claims, locked/replaced actor sessions.</summary>
public static class JwtSessionPolicy
{
    public static bool Matches(ClaimsPrincipal? principal, string expectedRole, int expectedVersion)
    {
        if (principal?.Identity?.IsAuthenticated != true || expectedVersion < 0) return false;
        var roles = principal.FindAll(ClaimTypes.Role).ToArray();
        var versions = principal.FindAll("security_version").ToArray();
        return roles.Length == 1 && roles[0].Value == expectedRole && versions.Length == 1
            && versions[0].Value == expectedVersion.ToString(CultureInfo.InvariantCulture);
    }
}
