using System.Buffers.Binary;
using System.Text;
using Wedding.Domain.Exceptions;

namespace Wedding.Infrastructure.Services;

public record StoredInvitationImage(string Url, int Width, int Height, long Size);

/// <summary>Local durable raster storage. Client filenames never become paths; no executable/SVG content is served.</summary>
public class InvitationImageStorage
{
    public const long MaxFileBytes = 8 * 1024 * 1024;
    private const long OwnerQuotaBytes = 200 * 1024 * 1024;
    private static readonly SemaphoreSlim WriteGate = new(1, 1);
    public string Root { get; }
    public InvitationImageStorage(string contentRoot, string? configuredRoot = null)
        => Root = Path.GetFullPath(string.IsNullOrWhiteSpace(configuredRoot) ? Path.Combine(contentRoot, "App_Data", "invitation-uploads") : Path.IsPathRooted(configuredRoot) ? configuredRoot : Path.Combine(contentRoot, configuredRoot));

    public async Task<StoredInvitationImage> SaveAsync(Guid ownerId, string originalName, string contentType, Stream input, long declaredLength, CancellationToken ct)
    {
        if (ownerId == Guid.Empty || declaredLength is <= 0 or > MaxFileBytes) throw new DomainException("Mỗi ảnh cần có dữ liệu và không vượt quá 8 MB.");
        var ext = Path.GetExtension(originalName).ToLowerInvariant();
        if (ext is not (".jpg" or ".jpeg" or ".png" or ".webp")) throw new DomainException("Chỉ hỗ trợ ảnh JPG, PNG hoặc WebP; không nhận SVG/GIF/HEIC.");
        using var memory = new MemoryStream(); var buffer = new byte[81920]; int read;
        while ((read = await input.ReadAsync(buffer, ct)) > 0)
        {
            if (memory.Length + read > MaxFileBytes) throw new DomainException("Ảnh vượt quá giới hạn 8 MB.");
            memory.Write(buffer, 0, read);
        }
        var image = RasterImageValidation.Sanitize(memory.ToArray());
        var canonicalExt = ext == ".jpeg" ? ".jpg" : ext;
        if (image.Extension != canonicalExt || !contentType.Equals(image.ContentType, StringComparison.OrdinalIgnoreCase)) throw new DomainException("Nội dung ảnh không khớp định dạng khai báo. Hãy xuất lại ảnh JPG, PNG hoặc WebP.");
        var directory = Path.Combine(Root, ownerId.ToString("N"));
        var fileName = $"{Guid.NewGuid():N}{image.Extension}";
        await WriteGate.WaitAsync(ct);
        try
        {
            Directory.CreateDirectory(directory);
            var existing = new DirectoryInfo(directory).EnumerateFiles().Take(201).ToArray();
            if (existing.Length >= 200 || existing.Sum(f => f.Length) + image.Bytes.Length > OwnerQuotaBytes) throw new DomainException("Bộ nhớ ảnh của hồ sơ đã đầy (200 ảnh / 200 MB). Liên hệ quản trị để dọn ảnh không dùng.");
            var path = Path.Combine(directory, fileName);
            try { await using var output = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, FileOptions.Asynchronous); await output.WriteAsync(image.Bytes, ct); }
            catch { if (File.Exists(path)) File.Delete(path); throw; }
        }
        finally { WriteGate.Release(); }
        return new($"/uploads/invitations/{ownerId:N}/{fileName}", image.Width, image.Height, image.Bytes.Length);
    }
    public string? Resolve(Guid ownerId, string fileName)
    {
        if (ownerId == Guid.Empty || fileName.Length is < 36 or > 37 || !Guid.TryParseExact(fileName[..32], "N", out var id)) return null;
        var ext = fileName[32..]; if (ext is not (".jpg" or ".png" or ".webp")) return null;
        return Path.Combine(Root, ownerId.ToString("N"), $"{id:N}{ext}");
    }
}

public record ValidatedRaster(byte[] Bytes, string Extension, string ContentType, int Width, int Height);
public static class RasterImageValidation
{
    private static readonly byte[] PngSignature = [137, 80, 78, 71, 13, 10, 26, 10];
    public static ValidatedRaster Sanitize(byte[] bytes)
    {
        if (bytes.Length < 24 || bytes.Length > InvitationImageStorage.MaxFileBytes) throw Invalid();
        if (bytes.AsSpan(0, 8).SequenceEqual(PngSignature)) return Png(bytes);
        if (bytes[0] == 0xff && bytes[1] == 0xd8) return Jpeg(bytes);
        if (Encoding.ASCII.GetString(bytes, 0, 4) == "RIFF" && Encoding.ASCII.GetString(bytes, 8, 4) == "WEBP") return Webp(bytes);
        throw Invalid();
    }
    private static void Dimensions(int width, int height)
    { if (width < 1 || height < 1 || width > 8192 || height > 8192 || (long)width * height > 25000000) throw new DomainException("Ảnh tối đa 8192 px mỗi cạnh và 25 triệu điểm ảnh. Hãy thu nhỏ ảnh rồi tải lại."); }
    private static DomainException Invalid() => new("Tệp không phải ảnh JPG, PNG hoặc WebP hợp lệ, bị hỏng hoặc có nội dung không được hỗ trợ.");
    private static ValidatedRaster Png(byte[] b)
    {
        var width = 0; var height = 0; var offset = 8; var hasPixels = false; var ended = false;
        using var output = new MemoryStream(); output.Write(PngSignature);
        while (offset + 12 <= b.Length)
        {
            var length = BinaryPrimitives.ReadUInt32BigEndian(b.AsSpan(offset, 4));
            if (length > b.Length - offset - 12) throw Invalid();
            var size = checked((int)length); var type = Encoding.ASCII.GetString(b, offset + 4, 4);
            var expectedCrc = BinaryPrimitives.ReadUInt32BigEndian(b.AsSpan(offset + 8 + size, 4));
            if (Crc32(b.AsSpan(offset + 4, size + 4)) != expectedCrc) throw Invalid();
            if (offset == 8)
            {
                if (type != "IHDR" || size != 13) throw Invalid();
                var pngWidth = BinaryPrimitives.ReadUInt32BigEndian(b.AsSpan(offset + 8, 4)); var pngHeight = BinaryPrimitives.ReadUInt32BigEndian(b.AsSpan(offset + 12, 4));
                if (pngWidth > 8192 || pngHeight > 8192) throw new DomainException("Ảnh tối đa 8192 px mỗi cạnh. Hãy thu nhỏ ảnh rồi tải lại.");
                width = (int)pngWidth; height = (int)pngHeight; Dimensions(width, height);
            }
            else if (type == "IHDR") throw Invalid();
            if (type == "acTL") throw new DomainException("Vui lòng dùng ảnh tĩnh thay cho ảnh động.");
            if (type == "IDAT") hasPixels = true;
            // Strip textual/EXIF/location metadata, preserve only raster/colour transparency chunks.
            if (type is "IHDR" or "PLTE" or "IDAT" or "IEND" or "tRNS") output.Write(b, offset, size + 12);
            offset += size + 12;
            if (type == "IEND") { if (size != 0 || offset != b.Length) throw Invalid(); ended = true; break; }
        }
        if (!ended || !hasPixels) throw Invalid();
        return new(output.ToArray(), ".png", "image/png", width, height);
    }
    private static ValidatedRaster Jpeg(byte[] b)
    {
        if (b[^2] != 0xff || b[^1] != 0xd9) throw Invalid();
        var pos = 2; var width = 0; var height = 0; var hasScan = false;
        using var output = new MemoryStream(); output.Write(b, 0, 2);
        while (pos + 4 <= b.Length)
        {
            var start = pos; if (b[pos++] != 0xff) throw Invalid();
            while (pos < b.Length && b[pos] == 0xff) pos++;
            if (pos >= b.Length) throw Invalid(); var marker = b[pos++];
            if (marker is 0x00 or 0xd8 or 0xd9 || pos + 2 > b.Length) throw Invalid();
            var length = BinaryPrimitives.ReadUInt16BigEndian(b.AsSpan(pos, 2));
            if (length < 2 || pos + length > b.Length) throw Invalid();
            if (marker is 0xc0 or 0xc1 or 0xc2)
            { if (length < 8) throw Invalid(); height = BinaryPrimitives.ReadUInt16BigEndian(b.AsSpan(pos + 3, 2)); width = BinaryPrimitives.ReadUInt16BigEndian(b.AsSpan(pos + 5, 2)); Dimensions(width, height); }
            if (marker == 0xda) { if (width == 0 || height == 0) throw Invalid(); output.Write(b, start, b.Length - start); hasScan = true; break; }
            if (marker != 0xfe && (marker < 0xe1 || marker > 0xed) && marker != 0xef) output.Write(b, start, pos + length - start);
            pos += length;
        }
        if (!hasScan) throw Invalid(); return new(output.ToArray(), ".jpg", "image/jpeg", width, height);
    }
    private static ValidatedRaster Webp(byte[] b)
    {
        if (BinaryPrimitives.ReadUInt32LittleEndian(b.AsSpan(4, 4)) != b.Length - 8) throw Invalid();
        using var payload = new MemoryStream(); payload.Write(Encoding.ASCII.GetBytes("WEBP"));
        var pos = 12; var width = 0; var height = 0; var hasPixels = false;
        while (pos + 8 <= b.Length)
        {
            var type = Encoding.ASCII.GetString(b, pos, 4); var size = BinaryPrimitives.ReadUInt32LittleEndian(b.AsSpan(pos + 4, 4));
            if (size > b.Length - pos - 8) throw Invalid(); var length = checked((int)size); var chunk = b.AsSpan(pos + 8, length);
            if (type is "ANIM" or "ANMF") throw new DomainException("Vui lòng dùng ảnh tĩnh thay cho ảnh động.");
            if (type == "VP8X") { if (pos != 12 || width != 0 || length != 10 || (chunk[0] & 2) != 0) throw Invalid(); width = 1 + chunk[4] + (chunk[5] << 8) + (chunk[6] << 16); height = 1 + chunk[7] + (chunk[8] << 8) + (chunk[9] << 16); Dimensions(width, height); b[pos + 8] &= 0xd3; }
            if (type is "VP8 " or "VP8L")
            {
                if (hasPixels) throw Invalid();
                int frameWidth, frameHeight;
                if (type == "VP8 ") { if (length < 10 || chunk[3] != 0x9d || chunk[4] != 0x01 || chunk[5] != 0x2a) throw Invalid(); frameWidth = BinaryPrimitives.ReadUInt16LittleEndian(chunk[6..8]) & 0x3fff; frameHeight = BinaryPrimitives.ReadUInt16LittleEndian(chunk[8..10]) & 0x3fff; }
                else { if (length < 5 || chunk[0] != 0x2f) throw Invalid(); var bits = BinaryPrimitives.ReadUInt32LittleEndian(chunk[1..5]); frameWidth = 1 + (int)(bits & 0x3fff); frameHeight = 1 + (int)((bits >> 14) & 0x3fff); }
                Dimensions(frameWidth, frameHeight);
                if (width != 0 && (width != frameWidth || height != frameHeight)) throw Invalid();
                width = frameWidth; height = frameHeight; hasPixels = true;
            }
            if (type is "VP8X" or "VP8 " or "VP8L" or "ALPH") { payload.Write(b, pos, length + 8); if ((length & 1) != 0) payload.WriteByte(0); }
            pos += length + 8 + (length & 1);
        }
        if (!hasPixels || pos != b.Length) throw Invalid(); Dimensions(width, height);
        using var output = new MemoryStream(); output.Write(Encoding.ASCII.GetBytes("RIFF")); var lengthBytes = new byte[4]; BinaryPrimitives.WriteUInt32LittleEndian(lengthBytes, (uint)payload.Length); output.Write(lengthBytes); payload.Position = 0; payload.CopyTo(output);
        return new(output.ToArray(), ".webp", "image/webp", width, height);
    }
    public static uint Crc32(ReadOnlySpan<byte> bytes)
    {
        uint crc = 0xffffffff;
        foreach (var value in bytes) { crc ^= value; for (var bit = 0; bit < 8; bit++) crc = (crc >> 1) ^ ((crc & 1) == 1 ? 0xedb88320u : 0); }
        return ~crc;
    }
}
