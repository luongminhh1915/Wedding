using Wedding.Domain.Exceptions;

namespace Wedding.Infrastructure.Services;

public record StoredListingImage(string Url, int Width, int Height, long Size);

/// <summary>
/// Stores validated listing raster files outside the deployed web root. Only generated raster
/// names are resolved; the optional old web root is a read-only migration fallback.
/// </summary>
public sealed class ListingImageStorage
{
    public const long MaxFileBytes = InvitationImageStorage.MaxFileBytes;
    private static readonly SemaphoreSlim WriteGate = new(1, 1);
    private const long QuotaBytes = 1024L * 1024 * 1024;
    private const int MaxFileCount = 5000;
    private readonly string? _legacyRoot;
    public string Root { get; }

    public ListingImageStorage(string contentRoot, string? configuredRoot = null, string? legacyWebRoot = null)
    {
        Root = Path.GetFullPath(string.IsNullOrWhiteSpace(configuredRoot)
            ? Path.Combine(contentRoot, "App_Data", "listing-uploads")
            : Path.IsPathRooted(configuredRoot) ? configuredRoot : Path.Combine(contentRoot, configuredRoot));
        _legacyRoot = legacyWebRoot == null ? null : Path.GetFullPath(Path.Combine(legacyWebRoot, "uploads", "listings"));
    }

    public async Task<StoredListingImage> SaveAsync(string originalName, string contentType, Stream input, long declaredLength, CancellationToken ct)
    {
        if (declaredLength is <= 0 or > MaxFileBytes) throw new DomainException("Mỗi ảnh cần có dữ liệu và không vượt quá 8 MB.");
        var extension = Path.GetExtension(originalName).ToLowerInvariant();
        if (extension is not (".jpg" or ".jpeg" or ".png" or ".webp")) throw new DomainException("Chỉ hỗ trợ ảnh JPG, PNG hoặc WebP; không nhận SVG/GIF/HEIC.");

        using var memory = new MemoryStream();
        var buffer = new byte[81920];
        int read;
        while ((read = await input.ReadAsync(buffer, ct)) > 0)
        {
            if (memory.Length + read > MaxFileBytes) throw new DomainException("Ảnh vượt quá giới hạn 8 MB.");
            memory.Write(buffer, 0, read);
        }
        var image = RasterImageValidation.Sanitize(memory.ToArray());
        if (image.Extension != (extension == ".jpeg" ? ".jpg" : extension)
            || !image.ContentType.Equals(contentType, StringComparison.OrdinalIgnoreCase))
            throw new DomainException("Nội dung ảnh không khớp định dạng khai báo. Hãy xuất lại ảnh JPG, PNG hoặc WebP.");

        var fileName = $"{Guid.NewGuid():N}{image.Extension}";
        await WriteGate.WaitAsync(ct);
        try
        {
            Directory.CreateDirectory(Root);
            var existing = new DirectoryInfo(Root).EnumerateFiles().Take(MaxFileCount + 1).ToArray();
            if (existing.Length >= MaxFileCount || existing.Sum(file => file.Length) + image.Bytes.Length > QuotaBytes)
                throw new DomainException("Bộ nhớ ảnh dịch vụ đã đầy (5.000 ảnh / 1 GB). Liên hệ quản trị để dọn các ảnh không dùng.");
            var path = Path.Combine(Root, fileName);
            try
            {
                await using var output = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, FileOptions.Asynchronous);
                await output.WriteAsync(image.Bytes, ct);
            }
            catch { if (File.Exists(path)) File.Delete(path); throw; }
        }
        finally { WriteGate.Release(); }
        return new($"/uploads/listings/{fileName}", image.Width, image.Height, image.Bytes.Length);
    }

    public string? Resolve(string fileName)
    {
        var name = CanonicalName(fileName);
        if (name == null) return null;
        var path = Path.Combine(Root, name);
        if (File.Exists(path) || _legacyRoot == null) return path;
        var legacyPath = Path.Combine(_legacyRoot, name);
        return File.Exists(legacyPath) ? legacyPath : path;
    }

    // Used only for rolling back files created during an unsuccessful multi-file upload.
    // This is deliberately not exposed as a public deletion endpoint.
    public void RemoveNewUpload(string url)
    {
        const string prefix = "/uploads/listings/";
        if (!url.StartsWith(prefix, StringComparison.Ordinal)) return;
        var name = CanonicalName(url[prefix.Length..]);
        if (name == null) return;
        var path = Path.Combine(Root, name);
        if (File.Exists(path)) File.Delete(path);
    }

    private static string? CanonicalName(string fileName)
    {
        if (string.IsNullOrEmpty(fileName) || fileName.Length is < 36 or > 37
            || !Guid.TryParseExact(fileName[..32], "N", out var id)) return null;
        var extension = fileName[32..];
        return extension is ".jpg" or ".jpeg" or ".png" or ".webp" ? $"{id:N}{extension}" : null;
    }
}
