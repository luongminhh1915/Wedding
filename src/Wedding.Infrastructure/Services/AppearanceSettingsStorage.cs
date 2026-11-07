using System.Text.Json;
using Wedding.Domain.Exceptions;

namespace Wedding.Infrastructure.Services;

public sealed record AppearanceSettings(
    bool PetalsEnabled = true,
    string PetalsStyle = "blush",
    string PetalsSpeed = "normal",
    int PetalsDensity = 14,
    double DefaultMusicVolume = 0.35,
    bool MusicAutoPlay = false);

/// <summary>Only validated public appearance settings are persisted beside the private site audio library.</summary>
public sealed class AppearanceSettingsStorage(InvitationAudioStorage audio)
{
    public const int MaximumJsonBytes = 4096;
    private static readonly SemaphoreSlim WriteGate = new(1, 1);
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly HashSet<string> KnownProperties = new(StringComparer.Ordinal)
    {
        "petalsEnabled", "petalsStyle", "petalsSpeed", "petalsDensity", "defaultMusicVolume", "musicAutoPlay"
    };
    private string SettingsPath => Path.Combine(audio.Root, "site-appearance.json");

    public async Task<AppearanceSettings> ReadAsync(CancellationToken ct)
    {
        await WriteGate.WaitAsync(ct);
        try
        {
            if (!File.Exists(SettingsPath)) return new();
            await using var input = new FileStream(SettingsPath, FileMode.Open, FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete, 4096, FileOptions.Asynchronous);
            if (input.Length is <= 0 or > MaximumJsonBytes) return new();
            using var document = await JsonDocument.ParseAsync(input, new JsonDocumentOptions { MaxDepth = 8 }, ct);
            return Normalize(document.RootElement);
        }
        catch (Exception ex) when (ex is JsonException or DomainException or FileNotFoundException or DirectoryNotFoundException)
        {
            // Preserve corrupt/historical files for review; never return their unchecked contents.
            return new();
        }
        finally { WriteGate.Release(); }
    }

    public async Task<AppearanceSettings> SaveAsync(JsonElement payload, CancellationToken ct)
    {
        var settings = Normalize(payload);
        var bytes = JsonSerializer.SerializeToUtf8Bytes(settings, JsonOptions);
        if (bytes.Length > MaximumJsonBytes) throw Invalid();
        await WriteGate.WaitAsync(ct);
        var temporary = Path.Combine(audio.Root, $".site-appearance-{Guid.NewGuid():N}.tmp");
        try
        {
            ct.ThrowIfCancellationRequested();
            Directory.CreateDirectory(audio.Root);
            await using (var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write,
                FileShare.None, 4096, FileOptions.Asynchronous))
            {
                await output.WriteAsync(bytes, ct);
                await output.FlushAsync(ct);
            }
            ct.ThrowIfCancellationRequested();
            if (File.Exists(SettingsPath)) File.Replace(temporary, SettingsPath, null);
            else File.Move(temporary, SettingsPath);
            return settings;
        }
        finally
        {
            try
            {
                if (File.Exists(temporary)) File.Delete(temporary);
            }
            finally { WriteGate.Release(); }
        }
    }

    private static AppearanceSettings Normalize(JsonElement payload)
    {
        if (payload.ValueKind != JsonValueKind.Object || payload.GetRawText().Length > MaximumJsonBytes) throw Invalid();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var property in payload.EnumerateObject())
            if (!KnownProperties.Contains(property.Name) || !seen.Add(property.Name)) throw Invalid();
        var defaults = new AppearanceSettings();
        var enabled = Boolean(payload, "petalsEnabled", defaults.PetalsEnabled);
        var style = Text(payload, "petalsStyle", defaults.PetalsStyle, ["blush", "red", "white", "mixed"]);
        var speed = Text(payload, "petalsSpeed", defaults.PetalsSpeed, ["slow", "normal", "fast"]);
        var density = defaults.PetalsDensity;
        if (payload.TryGetProperty("petalsDensity", out var count))
        {
            if (count.ValueKind != JsonValueKind.Number || !count.TryGetInt32(out density) || density is < 6 or > 24 || density % 2 != 0)
                throw Invalid();
        }
        var volume = defaults.DefaultMusicVolume;
        if (payload.TryGetProperty("defaultMusicVolume", out var amount))
        {
            if (amount.ValueKind != JsonValueKind.Number || !amount.TryGetDouble(out volume) || !double.IsFinite(volume) || volume is < 0 or > 1)
                throw Invalid();
        }
        return new(enabled, style, speed, density, volume, Boolean(payload, "musicAutoPlay", defaults.MusicAutoPlay));
    }

    private static bool Boolean(JsonElement payload, string name, bool fallback)
        => !payload.TryGetProperty(name, out var value) ? fallback : value.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => throw Invalid()
        };

    private static string Text(JsonElement payload, string name, string fallback, string[] allowed)
    {
        if (!payload.TryGetProperty(name, out var value)) return fallback;
        if (value.ValueKind != JsonValueKind.String || !allowed.Contains(value.GetString(), StringComparer.Ordinal)) throw Invalid();
        return value.GetString()!;
    }

    private static DomainException Invalid() => new("Cấu hình giao diện không hợp lệ: mật độ 6–24 cánh theo bước 2, âm lượng từ 0 đến 1 và các lựa chọn được hỗ trợ.");
}
