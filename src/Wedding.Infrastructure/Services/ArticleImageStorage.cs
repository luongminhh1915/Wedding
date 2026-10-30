using Wedding.Domain.Exceptions;

namespace Wedding.Infrastructure.Services;

public record StoredArticleImage(string Url, int Width, int Height, long Size);

/// <summary>Durable public CMS raster images; no client filename is used as a filesystem path.</summary>
public sealed class ArticleImageStorage
{
    public const long MaxFileBytes = InvitationImageStorage.MaxFileBytes;
    private const long QuotaBytes = 1024L * 1024 * 1024;
    private const int MaxFileCount = 5000;
    private static readonly SemaphoreSlim WriteGate = new(1, 1);
    public string Root { get; }

    public ArticleImageStorage(string contentRoot, string? configuredRoot = null, string? configuredListingRoot = null)
    {
        Root = !string.IsNullOrWhiteSpace(configuredRoot)
            ? Path.GetFullPath(Path.IsPathRooted(configuredRoot) ? configuredRoot : Path.Combine(contentRoot, configuredRoot))
            : !string.IsNullOrWhiteSpace(configuredListingRoot)
                ? Path.Combine(Path.GetDirectoryName(new ListingImageStorage(contentRoot, configuredListingRoot).Root)!, "articles")
                : Path.GetFullPath(Path.Combine(contentRoot, "App_Data", "article-uploads"));
    }

    public async Task<StoredArticleImage> SaveAsync(string originalName, string contentType, Stream input, long declaredLength, CancellationToken ct)
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
        if (memory.Length != declaredLength) throw new DomainException("Kích thước ảnh không khớp dữ liệu nhận được.");
        var image = RasterImageValidation.Sanitize(memory.ToArray());
        if (image.Extension != (extension == ".jpeg" ? ".jpg" : extension) ||
            !image.ContentType.Equals(contentType, StringComparison.OrdinalIgnoreCase))
            throw new DomainException("Nội dung ảnh không khớp định dạng khai báo. Hãy xuất lại ảnh JPG, PNG hoặc WebP.");
        var fileName = $"{Guid.NewGuid():N}{image.Extension}";
        await WriteGate.WaitAsync(ct);
        try
        {
            Directory.CreateDirectory(Root);
            var existing = new DirectoryInfo(Root).EnumerateFiles().Take(MaxFileCount + 1).ToArray();
            if (existing.Length >= MaxFileCount || existing.Sum(file => file.Length) + image.Bytes.Length > QuotaBytes)
                throw new DomainException("Bộ nhớ ảnh cẩm nang đã đầy (5.000 ảnh / 1 GB). Liên hệ quản trị để dọn ảnh không dùng.");
            var path = Path.Combine(Root, fileName);
            var created = false;
            try
            {
                await using var output = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, FileOptions.Asynchronous);
                created = true;
                await output.WriteAsync(image.Bytes, ct);
            }
            catch { if (created && File.Exists(path)) File.Delete(path); throw; }
        }
        finally { WriteGate.Release(); }
        return new($"/uploads/articles/{fileName}", image.Width, image.Height, image.Bytes.Length);
    }

    public string? Resolve(string fileName)
    {
        if (string.IsNullOrEmpty(fileName) || fileName.Length is < 36 or > 37 ||
            !Guid.TryParseExact(fileName[..32], "N", out var id)) return null;
        var extension = fileName[32..];
        return extension is ".jpg" or ".png" or ".webp" ? Path.Combine(Root, $"{id:N}{extension}") : null;
    }
}
