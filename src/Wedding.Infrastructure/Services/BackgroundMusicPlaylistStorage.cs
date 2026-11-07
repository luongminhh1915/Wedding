using System.Text.Json;
using System.Text.RegularExpressions;
using Wedding.Domain.Exceptions;

namespace Wedding.Infrastructure.Services;

public record BackgroundMusicTrack(string Id, string Title, string Type, string? Url = null, bool IsCustom = false, bool Hidden = false);

/// <summary>Public curated music metadata in the configured durable audio directory; no arbitrary JSON or filesystem paths.</summary>
public sealed class BackgroundMusicPlaylistStorage(InvitationAudioStorage audio)
{
    public static readonly Guid SiteOwnerId = Guid.ParseExact("c8e62673872e40a09bed56b3d42c8ebf", "N");
    public const int MaximumTracks = 20;
    public const int MaximumJsonBytes = 64 * 1024;
    private static readonly SemaphoreSlim WriteGate = new(1, 1);
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly BackgroundMusicTrack[] Defaults =
    [
        new("canon-synth", "Canon in D (Piano Synth)", "synth"),
        new("canon-romantic", "Không khí lễ cưới (âm thanh minh họa)", "audio", "https://actions.google.com/sounds/v1/ambiences/outdoor_celebration.ogg")
    ];
    private string PlaylistPath => Path.Combine(audio.Root, "site-playlist.json");

    public async Task<IReadOnlyList<BackgroundMusicTrack>> ReadAsync(CancellationToken ct)
    {
        await WriteGate.WaitAsync(ct);
        try
        {
            if (!File.Exists(PlaylistPath)) return Defaults.ToArray();
            await using var stream = new FileStream(PlaylistPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, 81920, FileOptions.Asynchronous);
            if (stream.Length is <= 0 or > MaximumJsonBytes) return Defaults.ToArray();
            using var document = await JsonDocument.ParseAsync(stream, new JsonDocumentOptions { MaxDepth = 8 }, ct);
            return Normalize(document.RootElement);
        }
        catch (Exception ex) when (ex is JsonException or DomainException or FileNotFoundException)
        {
            // Preserve an invalid historical file for explicit review; never serve its unchecked contents.
            return Defaults.ToArray();
        }
        finally { WriteGate.Release(); }
    }

    public async Task<IReadOnlyList<BackgroundMusicTrack>> SaveAsync(JsonElement payload, CancellationToken ct)
    {
        if (payload.GetRawText().Length > MaximumJsonBytes) throw Invalid();
        var tracks = Normalize(payload);
        var bytes = JsonSerializer.SerializeToUtf8Bytes(tracks, JsonOptions);
        if (bytes.Length > MaximumJsonBytes) throw Invalid();
        await WriteGate.WaitAsync(ct);
        var temporary = Path.Combine(audio.Root, $".site-playlist-{Guid.NewGuid():N}.tmp");
        try
        {
            Directory.CreateDirectory(audio.Root);
            await using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, FileOptions.Asynchronous))
            {
                await stream.WriteAsync(bytes, ct); await stream.FlushAsync(ct);
            }
            ct.ThrowIfCancellationRequested();
            if (File.Exists(PlaylistPath)) File.Replace(temporary, PlaylistPath, null);
            else File.Move(temporary, PlaylistPath);
            return tracks;
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
            WriteGate.Release();
        }
    }

    private IReadOnlyList<BackgroundMusicTrack> Normalize(JsonElement payload)
    {
        if (payload.ValueKind != JsonValueKind.Array || payload.GetArrayLength() > MaximumTracks) throw Invalid();
        var tracks = new List<BackgroundMusicTrack>(); var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in payload.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object) throw Invalid();
            var id = Text(item, "id", 64); var title = Text(item, "title", 150); var type = Text(item, "type", 10);
            if (!Regex.IsMatch(id, "^[A-Za-z0-9_-]+$", RegexOptions.CultureInvariant) || !ids.Add(id)) throw Invalid();
            var custom = item.TryGetProperty("isCustom", out var flag) && (flag.ValueKind switch
            { JsonValueKind.True => true, JsonValueKind.False => false, _ => throw Invalid() });
            var hidden = item.TryGetProperty("hidden", out var hiddenProp) && (hiddenProp.ValueKind switch
            { JsonValueKind.True => true, JsonValueKind.False => false, _ => false });
            string? url = null;
            if (type == "synth")
            {
                if (id != "canon-synth" || item.TryGetProperty("url", out var supplied) && supplied.ValueKind != JsonValueKind.Null) throw Invalid();
                custom = false;
            }
            else if (type == "audio")
            {
                url = Text(item, "url", 500);
                if (url.StartsWith('/'))
                {
                    var prefix = $"/uploads/invitation-audio/{SiteOwnerId:N}/";
                    if (!url.StartsWith(prefix, StringComparison.Ordinal)) throw Invalid();
                    var fileName = url[prefix.Length..];
                    if (!Regex.IsMatch(fileName, "^[a-f0-9]{32}\\.mp3$", RegexOptions.CultureInvariant)) throw Invalid();
                    var path = audio.Resolve(SiteOwnerId, fileName);
                    if (path == null || !File.Exists(path)) throw Invalid();
                }
                else if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps || !uri.IsDefaultPort ||
                    !uri.IsWellFormedOriginalString() || string.IsNullOrWhiteSpace(uri.Host) || !string.IsNullOrEmpty(uri.UserInfo) ||
                    !(uri.AbsolutePath.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase) || uri.AbsolutePath.EndsWith(".ogg", StringComparison.OrdinalIgnoreCase))) throw Invalid();
            }
            else if (type == "youtube")
            {
                url = Text(item, "url", 500);
                if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps || !uri.IsDefaultPort ||
                    !uri.IsWellFormedOriginalString() || string.IsNullOrWhiteSpace(uri.Host) || !string.IsNullOrEmpty(uri.UserInfo)) throw Invalid();
                var videoId = ExtractYouTubeId(uri);
                if (string.IsNullOrEmpty(videoId) || !Regex.IsMatch(videoId, "^[A-Za-z0-9_-]{11}$")) throw Invalid();
            }
            else throw Invalid();
            tracks.Add(new(id, title, type, url, custom, hidden));
        }
        return tracks;
    }

    private static string Text(JsonElement item, string name, int maximum)
    {
        if (!item.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.String) throw Invalid();
        var text = value.GetString()!;
        if (string.IsNullOrWhiteSpace(text) || text.Length > maximum || text != text.Trim() || text.Any(char.IsControl) || text.Contains('\\')) throw Invalid();
        return text;
    }
    public static string? ExtractYouTubeId(Uri uri)
    {
        var host = uri.Host.ToLowerInvariant();
        if (host == "youtu.be")
        {
            var path = uri.AbsolutePath.Trim('/');
            return path.Length == 11 ? path : null;
        }
        if (host is "www.youtube.com" or "youtube.com" or "m.youtube.com")
        {
            if (uri.AbsolutePath.StartsWith("/shorts/", StringComparison.OrdinalIgnoreCase) ||
                uri.AbsolutePath.StartsWith("/embed/", StringComparison.OrdinalIgnoreCase))
            {
                var parts = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 2 && parts[1].Length == 11) return parts[1];
            }
            var match = Regex.Match(uri.Query, @"[?&]v=([A-Za-z0-9_-]{11})(?:&|$)");
            if (match.Success) return match.Groups[1].Value;
        }
        return null;
    }

    private static DomainException Invalid() => new("Danh sách nhạc không hợp lệ: tối đa 20 bài, tên 150 ký tự, liên kết YouTube/MP3/OGG HTTPS hoặc MP3 đã tải lên thư viện nhạc chung.");
}
