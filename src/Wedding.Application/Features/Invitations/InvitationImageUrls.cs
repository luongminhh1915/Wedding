using System.Text.RegularExpressions;

namespace Wedding.Application.Features.Invitations;

public static class InvitationImageUrls
{
    public static bool IsValid(string? url, Guid ownerId)
    {
        if (string.IsNullOrWhiteSpace(url)) return true;
        if (url.Length > 500) return false;
        if (url.StartsWith('/')) return Regex.IsMatch(url, $"^/uploads/invitations/{ownerId:N}/[a-f0-9]{{32}}\\.(jpg|png|webp)$", RegexOptions.CultureInvariant);
        return Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https";
    }
}
