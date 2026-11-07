using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using Wedding.Application.Common.Interfaces;
using Wedding.Domain.Exceptions;

namespace Wedding.Infrastructure.Services;

public record StoredVerificationDocument(string Url, string OriginalName, long Size, string Extension);

/// <summary>Private owner-scoped evidence. Structural checks are not an antivirus guarantee.</summary>
public sealed class VerificationDocumentStorage : IVerificationDocumentStore
{
    public const long MaxFileBytes = 20 * 1024 * 1024;
    public const long MaxBatchBytes = 32 * 1024 * 1024;
    private static readonly SemaphoreSlim WriteGate = new(1, 1);
    private static readonly string[] Extensions = [".pdf", ".doc", ".docx", ".jpg", ".png"];
    public string Root { get; }

    public VerificationDocumentStorage(string contentRoot, string? configuredRoot = null, string? configuredListingRoot = null)
    {
        Root = !string.IsNullOrWhiteSpace(configuredRoot)
            ? Path.GetFullPath(Path.IsPathRooted(configuredRoot) ? configuredRoot : Path.Combine(contentRoot, configuredRoot))
            : !string.IsNullOrWhiteSpace(configuredListingRoot)
                ? Path.Combine(Path.GetDirectoryName(new ListingImageStorage(contentRoot, configuredListingRoot).Root)!, "verification-documents")
                : Path.GetFullPath(Path.Combine(contentRoot, "App_Data", "verification-documents"));
        var publicRoot = Path.GetFullPath(Path.Combine(contentRoot, "wwwroot"));
        if (Root.Equals(publicRoot, StringComparison.OrdinalIgnoreCase) || Root.StartsWith(publicRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Verification documents must be outside the public web root.");
    }

    public async Task<StoredVerificationDocument> SaveAsync(Guid vendorId, string originalName, string contentType, Stream input, long declaredLength, CancellationToken ct)
    {
        if (vendorId == Guid.Empty || declaredLength is <= 0 or > MaxFileBytes)
            throw new DomainException("Mỗi tài liệu cần có dữ liệu và không vượt quá 20 MB.");
        var extension = Path.GetExtension(originalName).ToLowerInvariant();
        if (extension == ".jpeg") extension = ".jpg";
        if (!Extensions.Contains(extension)) throw new DomainException("Chỉ nhận PDF, Word DOC/DOCX hoặc ảnh JPG/PNG.");
        var expectedMime = ContentType(extension);
        if (!string.Equals(contentType, expectedMime, StringComparison.OrdinalIgnoreCase))
            throw new DomainException("Định dạng tệp không khớp nội dung khai báo. Vui lòng xuất lại PDF, Word hoặc ảnh.");
        using var memory = new MemoryStream();
        var buffer = new byte[81920]; int read;
        while ((read = await input.ReadAsync(buffer, ct)) > 0)
        {
            if (memory.Length + read > MaxFileBytes) throw new DomainException("Tài liệu vượt quá 20 MB.");
            memory.Write(buffer, 0, read);
        }
        if (memory.Length != declaredLength) throw new DomainException("Kích thước tài liệu không khớp dữ liệu nhận được.");
        var bytes = Validate(extension, memory.ToArray());
        var fileName = $"{Guid.NewGuid():N}{extension}";
        var folder = Path.Combine(Root, vendorId.ToString("D"));
        await WriteGate.WaitAsync(ct);
        try
        {
            var existing = Directory.Exists(folder) ? new DirectoryInfo(folder).EnumerateFiles().Take(101).ToArray() : [];
            if (existing.Length >= 100 || existing.Sum(f => f.Length) + bytes.Length > 200L * 1024 * 1024)
                throw new DomainException("Kho giấy tờ đã đạt giới hạn kỹ thuật. Liên hệ quản trị để kiểm tra tệp không còn dùng.");
            Directory.CreateDirectory(folder);
            var path = Path.Combine(folder, fileName); var created = false;
            try
            {
                await using var output = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, FileOptions.Asynchronous);
                created = true; await output.WriteAsync(bytes, ct);
            }
            catch { if (created && File.Exists(path)) File.Delete(path); throw; }
        }
        finally { WriteGate.Release(); }
        var displayName = Path.GetFileName(originalName.Replace('\\', '/'));
        return new($"/uploads/documents/{vendorId:D}/{fileName}", displayName.Length > 150 ? displayName[..150] : displayName, bytes.Length, extension);
    }

    public string? Resolve(Guid vendorId, string fileName)
    {
        if (vendorId == Guid.Empty || string.IsNullOrEmpty(fileName) || fileName.Length < 36 ||
            !Guid.TryParseExact(fileName[..32], "N", out var id) || fileName[..32] != id.ToString("N")) return null;
        var extension = fileName[32..];
        return Extensions.Contains(extension) ? Path.Combine(Root, vendorId.ToString("D"), fileName) : null;
    }
    public bool IsOwnedExistingReference(Guid vendorId, string reference)
    {
        var prefix = $"/uploads/documents/{vendorId:D}/";
        if (!reference.StartsWith(prefix, StringComparison.Ordinal)) return false;
        var path = Resolve(vendorId, reference[prefix.Length..]);
        return path != null && File.Exists(path);
    }
    public void RemoveNewUpload(Guid vendorId, string reference)
    {
        var prefix = $"/uploads/documents/{vendorId:D}/";
        if (!reference.StartsWith(prefix, StringComparison.Ordinal)) return;
        var path = Resolve(vendorId, reference[prefix.Length..]);
        if (path != null && File.Exists(path)) File.Delete(path);
    }
    public static string ContentType(string extension) => extension switch
    {
        ".pdf" => "application/pdf", ".doc" => "application/msword",
        ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        ".png" => "image/png", ".jpg" => "image/jpeg", _ => throw new DomainException("Định dạng không hợp lệ.")
    };

    private static byte[] Validate(string extension, byte[] bytes)
    {
        try
        {
            if (extension is ".jpg" or ".png")
            {
                var image = RasterImageValidation.Sanitize(bytes);
                if (image.Extension != extension) throw new InvalidDataException();
                return image.Bytes;
            }
            if (extension == ".pdf")
            {
                var text = Encoding.Latin1.GetString(bytes);
                if (bytes.Length < 32 || !(text.StartsWith("%PDF-1.", StringComparison.Ordinal) && text[7] is >= '0' and <= '7' || text.StartsWith("%PDF-2.0", StringComparison.Ordinal)) ||
                    !text.TrimEnd().EndsWith("%%EOF", StringComparison.Ordinal) || !text.Contains("startxref", StringComparison.Ordinal) || !text.Contains("/Catalog", StringComparison.Ordinal)) throw new InvalidDataException();
                foreach (var action in new[] { "/JavaScript", "/JS", "/Launch", "/EmbeddedFile", "/RichMedia", "/OpenAction", "/AA" })
                    if (text.Contains(action, StringComparison.Ordinal)) throw new InvalidDataException();
            }
            else if (extension == ".doc") ValidateDoc(bytes);
            else ValidateDocx(bytes);
            return bytes;
        }
        catch (Exception ex) when (ex is InvalidDataException or XmlException or ArgumentException or OverflowException or EndOfStreamException)
        { throw new DomainException("Nội dung tài liệu bị hỏng, không đúng định dạng hoặc chứa nội dung nhúng không được hỗ trợ. Hãy xuất lại PDF/Word thông thường."); }
    }

    private static void ValidateDocx(byte[] bytes)
    {
        using var zip = new ZipArchive(new MemoryStream(bytes), ZipArchiveMode.Read);
        if (zip.Entries.Count is < 3 or > 512) throw new InvalidDataException();
        long expanded = 0; var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in zip.Entries)
        {
            var name = entry.FullName;
            if (!names.Add(name) || name.StartsWith('/') || name.Contains('\\') || name.Contains(':') || name.Split('/').Any(p => p is "." or "..") ||
                name.Contains("vba", StringComparison.OrdinalIgnoreCase) || name.Contains("activeX", StringComparison.OrdinalIgnoreCase) || name.Contains("embeddings/", StringComparison.OrdinalIgnoreCase) ||
                entry.Length > 50L * 1024 * 1024 || (expanded += entry.Length) > 64L * 1024 * 1024 || entry.Length > Math.Max(1024 * 1024, entry.CompressedLength * 200)) throw new InvalidDataException();
            if (name.EndsWith(".rels", StringComparison.OrdinalIgnoreCase))
            {
                var relationships = ReadXml(entry);
                if (relationships.Descendants().Any(x => string.Equals((string?)x.Attribute("TargetMode"), "External", StringComparison.OrdinalIgnoreCase))) throw new InvalidDataException();
            }
        }
        var contentTypes = ReadXml(zip.GetEntry("[Content_Types].xml") ?? throw new InvalidDataException());
        if (!contentTypes.Descendants().Any(x => (string?)x.Attribute("PartName") == "/word/document.xml" &&
            (string?)x.Attribute("ContentType") == "application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml")) throw new InvalidDataException();
        _ = zip.GetEntry("_rels/.rels") ?? throw new InvalidDataException();
        var doc = ReadXml(zip.GetEntry("word/document.xml") ?? throw new InvalidDataException());
        if (doc.Root?.Name.LocalName != "document" || doc.Root.Name.NamespaceName is not ("http://schemas.openxmlformats.org/wordprocessingml/2006/main" or "http://purl.oclc.org/ooxml/wordprocessingml/main")) throw new InvalidDataException();
    }
    private static XDocument ReadXml(ZipArchiveEntry entry)
    {
        if (entry.Length > 4 * 1024 * 1024) throw new InvalidDataException();
        using var stream = entry.Open();
        using var reader = XmlReader.Create(stream, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 4 * 1024 * 1024 });
        return XDocument.Load(reader);
    }
    private static void ValidateDoc(byte[] bytes)
    {
        ReadOnlySpan<byte> signature = [0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1];
        if (bytes.Length < 1024 || !bytes.AsSpan(0, 8).SequenceEqual(signature) || BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(28)) != 0xFFFE) throw new InvalidDataException();
        var shift = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(30));
        var major = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(26));
        if (!(major == 3 && shift == 9 || major == 4 && shift == 12)) throw new InvalidDataException();
        var sectorSize = 1 << shift;
        if (bytes.Length % sectorSize != 0) throw new InvalidDataException();
        var count = bytes.Length / sectorSize - 1;
        var fatCount = BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(44));
        if (fatCount is <= 0 or > 109) throw new InvalidDataException();
        var fat = new List<int>();
        for (var i = 0; i < fatCount; i++)
        {
            var sector = BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(76 + 4 * i));
            if (sector < 0 || sector >= count) throw new InvalidDataException();
            for (var j = 0; j < sectorSize; j += 4) fat.Add(BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan((sector + 1) * sectorSize + j)));
        }
        var next = BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(48)); var seen = new HashSet<int>(); var found = false;
        while (next != -2)
        {
            if (next < 0 || next >= count || next >= fat.Count || !seen.Add(next) || seen.Count > 128) throw new InvalidDataException();
            for (var p = (next + 1) * sectorSize; p < (next + 2) * sectorSize; p += 128)
            {
                var length = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(p + 64));
                if (length == 0) continue;
                if (length is < 2 or > 64 || length % 2 != 0) throw new InvalidDataException();
                var name = Encoding.Unicode.GetString(bytes, p, length - 2);
                if (name.Contains("VBA", StringComparison.OrdinalIgnoreCase) || name.Equals("Macros", StringComparison.OrdinalIgnoreCase) || name.Equals("ObjectPool", StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException();
                if (name == "WordDocument" && bytes[p + 66] == 2) found = true;
            }
            next = fat[next];
        }
        if (!found) throw new InvalidDataException();
    }
}
