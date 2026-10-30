using System.Text.RegularExpressions;
using Wedding.Domain.Exceptions;

namespace Wedding.Application.Features.Invitations;

/// <summary>Music is a durable owner-scoped upload, a canonical YouTube reference, or a legacy audio URL. No server fetches occur.</summary>
public static class InvitationMusicUrls
{
    private static readonly HashSet<string> YouTubeHosts = new(StringComparer.OrdinalIgnoreCase)
    {
        "youtube.com", "www.youtube.com", "m.youtube.com", "music.youtube.com",
        "youtu.be", "www.youtu.be", "youtube-nocookie.com", "www.youtube-nocookie.com"
    };

    public static string? Normalize(string? value, Guid ownerId)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var url = value.Trim();
        if (url.Length > 500 || url.Any(character => char.IsControl(character) || char.IsWhiteSpace(character)) || url.Contains('\\')) throw Invalid();
        if (url.StartsWith('/'))
        {
            if (ownerId == Guid.Empty || !Regex.IsMatch(url,
                $"^/uploads/invitation-audio/{ownerId:N}/[a-f0-9]{{32}}\\.mp3$", RegexOptions.CultureInvariant)) throw Invalid();
            return url;
        }
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https") ||
            !string.IsNullOrEmpty(uri.UserInfo) || string.IsNullOrWhiteSpace(uri.Host)) throw Invalid();
        if (!YouTubeHosts.Contains(uri.Host))
        {
            // Do not treat a misspelled/spoofed video host as a legacy audio source.
            if (uri.Host.Contains("youtube", StringComparison.OrdinalIgnoreCase) ||
                uri.Host.Contains("youtu.be", StringComparison.OrdinalIgnoreCase)) throw Invalid();
            return url;
        }
        if (uri.Scheme != "https" || !uri.IsDefaultPort) throw Invalid();
        // Uri can decode unreserved percent escapes; validate the supplied video ID,
        // not a normalized lookalike that bypasses the exact-ID input contract.
        var beforeFragment = url.Split('#', 2)[0];
        var authorityEnd = beforeFragment.IndexOf('/', beforeFragment.IndexOf("://", StringComparison.Ordinal) + 3);
        var path = authorityEnd < 0 ? "/" : beforeFragment[authorityEnd..].Split('?', 2)[0];
        var query = beforeFragment.Split('?', 2);
        string? id = null;
        if (uri.Host is "youtu.be" or "www.youtu.be")
            id = path.StartsWith('/') ? path[1..] : null;
        else if (uri.Host.EndsWith("youtube-nocookie.com", StringComparison.OrdinalIgnoreCase))
            id = path.StartsWith("/embed/", StringComparison.Ordinal) ? path[7..] : null;
        else if (path == "/watch")
        {
            var videoValues = (query.Length == 2 ? query[1] : "").Split('&', StringSplitOptions.RemoveEmptyEntries)
                .Select(item => item.Split('=', 2)).Where(pair => Uri.UnescapeDataString(pair[0]) == "v").ToArray();
            if (videoValues.Length == 1 && videoValues[0].Length == 2) id = videoValues[0][1];
        }
        else
        {
            var prefix = new[] { "/embed/", "/shorts/", "/live/" }.FirstOrDefault(item => path.StartsWith(item, StringComparison.Ordinal));
            if (prefix != null) id = path[prefix.Length..];
        }
        if (id == null || !Regex.IsMatch(id, "^[A-Za-z0-9_-]{11}$", RegexOptions.CultureInvariant)) throw Invalid();
        return $"https://www.youtube.com/watch?v={id}";
    }

    private static DomainException Invalid() => new("Nhạc cần là file MP3 đã tải lên hồ sơ này, liên kết âm thanh HTTP/HTTPS hợp lệ hoặc URL YouTube HTTPS có video ID hợp lệ.");
}
