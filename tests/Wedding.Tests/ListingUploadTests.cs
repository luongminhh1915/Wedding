using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;
using Wedding.Domain.Exceptions;
using Wedding.Infrastructure.Services;

namespace Wedding.Tests;

public sealed class ListingUploadTests
{
    [Fact]
    public async Task NewInstancesKeepImagesInConfiguredFolderOutsidePublishDirectory()
    {
        using var directory = new TempFolder();
        var contentRoot = Path.Combine(directory.Path, "site", "wwwroot");
        var configuredRoot = Path.Combine("..", "private", "uploads", "listings");
        var beforeDeploy = new ListingImageStorage(contentRoot, configuredRoot);
        var bytes = Png(metadata: true);
        var stored = await beforeDeploy.SaveAsync("../../holiday.png", "image/png", new MemoryStream(bytes), bytes.Length, default);

        var afterDeploy = new ListingImageStorage(contentRoot, configuredRoot);
        var path = afterDeploy.Resolve(stored.Url.Split('/').Last());
        Assert.Equal(Path.GetFullPath(Path.Combine(contentRoot, configuredRoot)), afterDeploy.Root);
        Assert.NotNull(path);
        Assert.StartsWith(afterDeploy.Root + Path.DirectorySeparatorChar, path);
        Assert.True(File.Exists(path));
        Assert.StartsWith("/uploads/listings/", stored.Url);
        Assert.DoesNotContain("holiday", stored.Url);
        Assert.DoesNotContain("private-location", Encoding.ASCII.GetString(await File.ReadAllBytesAsync(path!)));
        Assert.Equal(1, RasterImageValidation.Sanitize(await File.ReadAllBytesAsync(path!)).Width);
        Assert.False(Directory.Exists(Path.Combine(contentRoot, "uploads")));
    }

    [Fact]
    public async Task DefaultWritesOutsideWebRootAndOldGeneratedNamesRemainReadable()
    {
        using var directory = new TempFolder();
        var webRoot = Path.Combine(directory.Path, "wwwroot");
        var legacyRoot = Path.Combine(webRoot, "uploads", "listings");
        Directory.CreateDirectory(legacyRoot);
        var legacyName = $"{Guid.NewGuid():N}.jpeg";
        var legacyPath = Path.Combine(legacyRoot, legacyName);
        await File.WriteAllBytesAsync(legacyPath, Png());
        var storage = new ListingImageStorage(directory.Path, legacyWebRoot: webRoot);

        Assert.Equal(Path.Combine(directory.Path, "App_Data", "listing-uploads"), storage.Root);
        Assert.Equal(legacyPath, storage.Resolve(legacyName));
        storage.RemoveNewUpload($"/uploads/listings/{legacyName}");
        Assert.True(File.Exists(legacyPath)); // Rollback can never delete an existing legacy upload.
        var bytes = Png();
        var stored = await storage.SaveAsync("new.png", "image/png", new MemoryStream(bytes), bytes.Length, default);
        Assert.StartsWith(storage.Root, storage.Resolve(stored.Url.Split('/').Last()));
        Assert.Single(Directory.EnumerateFiles(legacyRoot));
    }

    [Theory]
    [InlineData("../../appsettings.json")]
    [InlineData("..\\..\\web.config")]
    [InlineData("%2e%2e%2fappsettings.json")]
    [InlineData("0123456789abcdef0123456789abcdef.svg")]
    [InlineData("0123456789abcdef0123456789abcdef.gif")]
    [InlineData("0123456789abcdef0123456789abcdef.JPG")]
    [InlineData("0123456789abcdef0123456789abcdef.png/../web.config")]
    [InlineData("/0123456789abcdef0123456789abcdef.png")]
    [InlineData("")]
    public void OnlyGeneratedRasterNamesCanResolve(string name)
    {
        using var directory = new TempFolder();
        Assert.Null(new ListingImageStorage(directory.Path).Resolve(name));
    }

    [Fact]
    public async Task RejectsSpoofedTypesOversizeAndDamagedFilesWithoutWriting()
    {
        using var directory = new TempFolder();
        var storage = new ListingImageStorage(directory.Path);
        var bytes = Png();
        await Assert.ThrowsAsync<DomainException>(() => storage.SaveAsync("image.svg", "image/svg+xml", new MemoryStream(bytes), bytes.Length, default));
        await Assert.ThrowsAsync<DomainException>(() => storage.SaveAsync("image.jpg", "image/jpeg", new MemoryStream(bytes), bytes.Length, default));
        await Assert.ThrowsAsync<DomainException>(() => storage.SaveAsync("image.png", "text/html", new MemoryStream(bytes), bytes.Length, default));
        await Assert.ThrowsAsync<DomainException>(() => storage.SaveAsync("image.png", "image/png", new MemoryStream(bytes), ListingImageStorage.MaxFileBytes + 1, default));
        await Assert.ThrowsAsync<DomainException>(() => storage.SaveAsync("image.png", "image/png", new MemoryStream(new byte[ListingImageStorage.MaxFileBytes + 1]), 1, default));
        await Assert.ThrowsAsync<DomainException>(() => storage.SaveAsync("image.png", "image/png", new MemoryStream(), 0, default));
        var corrupted = bytes.ToArray(); corrupted[^1] ^= 1;
        await Assert.ThrowsAsync<DomainException>(() => storage.SaveAsync("image.png", "image/png", new MemoryStream(corrupted), corrupted.Length, default));
        Assert.False(Directory.Exists(storage.Root));
    }

    [Fact]
    public async Task RollbackRemovesOnlyExplicitNewUploadAndNamesDoNotCollide()
    {
        using var directory = new TempFolder();
        var storage = new ListingImageStorage(directory.Path);
        var bytes = Png();
        var results = await Task.WhenAll(Enumerable.Range(0, 5).Select(_ => storage.SaveAsync("same-name.png", "image/png", new MemoryStream(bytes), bytes.Length, default)));
        Assert.Equal(5, results.Select(result => result.Url).Distinct().Count());
        storage.RemoveNewUpload("/uploads/listings/../../appsettings.json");
        storage.RemoveNewUpload(results[0].Url);
        Assert.Equal(4, Directory.EnumerateFiles(storage.Root).Count());
        foreach (var result in results.Skip(1)) Assert.True(File.Exists(new ListingImageStorage(directory.Path).Resolve(result.Url.Split('/').Last())));
    }

    private static byte[] Png(bool metadata = false)
    {
        using var stream = new MemoryStream(); stream.Write([137, 80, 78, 71, 13, 10, 26, 10]);
        var header = new byte[13]; BinaryPrimitives.WriteUInt32BigEndian(header, 1); BinaryPrimitives.WriteUInt32BigEndian(header.AsSpan(4), 1); header[8] = 8; header[9] = 6;
        Chunk(stream, "IHDR", header);
        if (metadata) Chunk(stream, "tEXt", Encoding.ASCII.GetBytes("Location\0private-location"));
        using var compressed = new MemoryStream();
        using (var zlib = new ZLibStream(compressed, CompressionLevel.Fastest, true)) zlib.Write([0, 37, 51, 76, 255]);
        Chunk(stream, "IDAT", compressed.ToArray()); Chunk(stream, "IEND", []);
        return stream.ToArray();
    }

    private static void Chunk(Stream stream, string name, byte[] bytes)
    {
        var length = new byte[4]; BinaryPrimitives.WriteUInt32BigEndian(length, (uint)bytes.Length); stream.Write(length);
        var type = Encoding.ASCII.GetBytes(name); stream.Write(type); stream.Write(bytes);
        var crc = new byte[4]; BinaryPrimitives.WriteUInt32BigEndian(crc, RasterImageValidation.Crc32([.. type, .. bytes])); stream.Write(crc);
    }

    private sealed class TempFolder : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "WeddingListingUploadTest_" + Guid.NewGuid().ToString("N"));
        public TempFolder() => Directory.CreateDirectory(Path);
        public void Dispose()
        {
            var resolved = System.IO.Path.GetFullPath(Path);
            var expectedPrefix = System.IO.Path.GetFullPath(System.IO.Path.GetTempPath()) + "WeddingListingUploadTest_";
            if (!resolved.StartsWith(expectedPrefix, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Invalid test cleanup path.");
            Directory.Delete(resolved, true);
        }
    }
}
