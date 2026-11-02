using System.Buffers.Binary;
using Wedding.Domain.Exceptions;

namespace Wedding.Infrastructure.Services;

public record StoredInvitationAudio(string Url, long Size);

/// <summary>Bounded MP3 storage outside the public web root. Existing files are never overwritten or removed by replacement.</summary>
public sealed class InvitationAudioStorage
{
    public const long MaxFileBytes = 12 * 1024 * 1024;
    public const long OwnerQuotaBytes = 100 * 1024 * 1024;
    public const int OwnerMaxFiles = 20;
    private static readonly SemaphoreSlim WriteGate = new(1, 1);
    public string Root { get; }

    public InvitationAudioStorage(string contentRoot, string? configuredRoot = null)
        => Root = Path.GetFullPath(string.IsNullOrWhiteSpace(configuredRoot)
            ? Path.Combine(contentRoot, "App_Data", "invitation-audio")
            : Path.IsPathRooted(configuredRoot) ? configuredRoot : Path.Combine(contentRoot, configuredRoot));

    public async Task<StoredInvitationAudio> SaveAsync(Guid ownerId, string originalName, string contentType, Stream input, long declaredLength, CancellationToken ct)
    {
        if (ownerId == Guid.Empty || declaredLength is <= 0 or > MaxFileBytes)
            throw new DomainException("Mỗi bài nhạc cần có dữ liệu và không vượt quá 12 MB.");
        if (!Path.GetExtension(originalName).Equals(".mp3", StringComparison.OrdinalIgnoreCase) ||
            !new[] { "audio/mpeg", "audio/mp3", "application/octet-stream", "" }.Contains(contentType, StringComparer.OrdinalIgnoreCase))
            throw new DomainException("Chỉ hỗ trợ MP3 (audio/mpeg). Hãy xuất lại file MP3 rồi tải lên.");
        using var memory = new MemoryStream();
        var buffer = new byte[81920]; int read;
        while ((read = await input.ReadAsync(buffer, ct)) > 0)
        {
            if (memory.Length + read > MaxFileBytes) throw new DomainException("Bài nhạc vượt quá giới hạn 12 MB.");
            memory.Write(buffer, 0, read);
        }
        if (memory.Length != declaredLength) throw new DomainException("Kích thước file nhạc không khớp dữ liệu tải lên.");
        var bytes = Mp3AudioValidation.Sanitize(memory.ToArray());
        var directory = Path.Combine(Root, ownerId.ToString("N"));
        var fileName = $"{Guid.NewGuid():N}.mp3";
        await WriteGate.WaitAsync(ct);
        try
        {
            Directory.CreateDirectory(directory);
            var existing = new DirectoryInfo(directory).EnumerateFiles().Take(OwnerMaxFiles + 1).ToArray();
            if (existing.Length >= OwnerMaxFiles || existing.Sum(file => file.Length) + bytes.Length > OwnerQuotaBytes)
                throw new DomainException("Bộ nhớ nhạc của hồ sơ đã đầy (20 bài / 100 MB). Liên hệ quản trị để dọn file không dùng.");
            var path = Path.Combine(directory, fileName);
            try
            {
                await using var output = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, FileOptions.Asynchronous);
                await output.WriteAsync(bytes, ct);
            }
            catch { if (File.Exists(path)) File.Delete(path); throw; }
        }
        finally { WriteGate.Release(); }
        return new($"/uploads/invitation-audio/{ownerId:N}/{fileName}", bytes.LongLength);
    }

    public string? Resolve(Guid ownerId, string fileName)
    {
        if (ownerId == Guid.Empty || fileName.Length != 36 ||
            !Guid.TryParseExact(fileName[..32], "N", out var id) || fileName[32..] != ".mp3") return null;
        return Path.Combine(Root, ownerId.ToString("N"), $"{id:N}.mp3");
    }
}

/// <summary>Validates complete consecutive MPEG Layer III frames and strips identifying ID3 tags, not an audio decoder.</summary>
public static class Mp3AudioValidation
{
    private static readonly int[] Mpeg1Bitrates = [0, 32, 40, 48, 56, 64, 80, 96, 112, 128, 160, 192, 224, 256, 320];
    private static readonly int[] Mpeg2Bitrates = [0, 8, 16, 24, 32, 40, 48, 56, 64, 80, 96, 112, 128, 144, 160];
    private static readonly int[] SampleRates = [44100, 48000, 32000];

    public static byte[] Sanitize(byte[] bytes)
    {
        if (bytes.Length < 8 || bytes.LongLength > InvitationAudioStorage.MaxFileBytes) throw Invalid();
        var start = 0; var end = bytes.Length;
        if (bytes.AsSpan(0, 3).SequenceEqual("ID3"u8))
        {
            if (bytes.Length < 10 || bytes[3] is < 2 or > 4 || bytes[4] == 0xff ||
                bytes.AsSpan(6, 4).ContainsAnyExceptInRange((byte)0, (byte)127)) throw Invalid();
            var tagSize = (bytes[6] << 21) | (bytes[7] << 14) | (bytes[8] << 7) | bytes[9];
            var footer = bytes[3] == 4 && (bytes[5] & 0x10) != 0 ? 10 : 0;
            start = 10 + tagSize + footer;
            if (start > end - 8) throw Invalid();
        }
        if (end - start >= 128 && bytes.AsSpan(end - 128, 3).SequenceEqual("TAG"u8)) end -= 128;
        var position = start; var frames = 0; int? expectedVersion = null; int? expectedRate = null;
        while (position + 4 <= end)
        {
            var header = BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(position, 4));
            var version = (int)((header >> 19) & 3); var layer = (header >> 17) & 3;
            var bitrateIndex = (int)((header >> 12) & 15); var sampleIndex = (int)((header >> 10) & 3);
            if ((header & 0xffe00000) != 0xffe00000 || version == 1 || layer != 1 ||
                bitrateIndex is 0 or 15 || sampleIndex == 3 || (header & 3) == 2) throw Invalid();
            var sampleRate = SampleRates[sampleIndex] / (version == 3 ? 1 : version == 2 ? 2 : 4);
            if ((expectedVersion.HasValue && expectedVersion != version) || (expectedRate.HasValue && expectedRate != sampleRate)) throw Invalid();
            expectedVersion = version; expectedRate = sampleRate;
            var bitrate = (version == 3 ? Mpeg1Bitrates : Mpeg2Bitrates)[bitrateIndex];
            var frameLength = (version == 3 ? 144000 : 72000) * bitrate / sampleRate + (int)((header >> 9) & 1);
            if (frameLength < 4 || frameLength > end - position) throw Invalid();
            position += frameLength; frames++;
        }
        if (position != end || frames < 2) throw Invalid();
        return bytes[start..end];
    }

    private static DomainException Invalid() => new("File không chứa chuỗi khung âm thanh MP3 hợp lệ, bị cắt ngắn hoặc có dữ liệu không được hỗ trợ. Hãy xuất lại MP3 rồi tải lên.");
}
