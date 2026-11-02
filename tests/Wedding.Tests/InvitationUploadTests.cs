using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Wedding.Application.Common.Interfaces;
using Wedding.Application.Features.Invitations;
using Wedding.Application.Features.Invitations.Commands.SaveInvitation;
using Wedding.Application.Features.Invitations.Queries.GetPublicInvitation;
using Wedding.Application.Features.Planner;
using Wedding.Domain.Entities;
using Wedding.Domain.Enums;
using Wedding.Domain.Exceptions;
using Wedding.Infrastructure.Persistence;
using Wedding.Infrastructure.Services;

namespace Wedding.Tests;

public class InvitationUploadTests
{
    [Fact]
    public async Task UploadProducesGeneratedSafePublicPathAndStripsPrivateMetadata()
    {
        using var folder = new TempFolder(); var storage = new InvitationImageStorage(folder.Path); var owner = Guid.NewGuid(); var png = Png(metadata: true);
        await using var stream = new MemoryStream(png);
        var image = await storage.SaveAsync(owner, "../../private-location.png", "image/png", stream, png.Length, default);
        Assert.StartsWith($"/uploads/invitations/{owner:N}/", image.Url); Assert.DoesNotContain("private", image.Url);
        var file = storage.Resolve(owner, image.Url.Split('/').Last()); Assert.NotNull(file); Assert.True(File.Exists(file));
        var saved = await File.ReadAllBytesAsync(file!); Assert.DoesNotContain("private-location", Encoding.ASCII.GetString(saved));
        var rechecked = RasterImageValidation.Sanitize(saved); Assert.Equal(320, rechecked.Width); Assert.Equal(180, rechecked.Height);
        Assert.Null(storage.Resolve(owner, "../../appsettings.json")); Assert.Null(storage.Resolve(owner, $"{Guid.NewGuid():N}.svg"));
        Assert.False(File.Exists(storage.Resolve(Guid.NewGuid(), image.Url.Split('/').Last())));
    }
    [Fact]
    public async Task UploadRejectsSvgMimeMismatchOversizeAndMalformedRaster()
    {
        using var folder = new TempFolder(); var storage = new InvitationImageStorage(folder.Path); var owner = Guid.NewGuid(); var png = Png();
        await Assert.ThrowsAsync<DomainException>(() => storage.SaveAsync(owner, "fake.svg", "image/svg+xml", new MemoryStream(Encoding.UTF8.GetBytes("<svg xmlns='http://www.w3.org/2000/svg'><script>alert(1)</script></svg>")), 90, default));
        await Assert.ThrowsAsync<DomainException>(() => storage.SaveAsync(owner, "fake.jpg", "image/jpeg", new MemoryStream(png), png.Length, default));
        await Assert.ThrowsAsync<DomainException>(() => storage.SaveAsync(owner, "fake.png", "text/html", new MemoryStream(png), png.Length, default));
        await Assert.ThrowsAsync<DomainException>(() => storage.SaveAsync(owner, "large.png", "image/png", new MemoryStream(), InvitationImageStorage.MaxFileBytes + 1, default));
        await Assert.ThrowsAsync<DomainException>(() => storage.SaveAsync(owner, "large.png", "image/png", new MemoryStream(new byte[InvitationImageStorage.MaxFileBytes + 1]), 1, default));
        var corrupted = png.ToArray(); corrupted[^1] ^= 1; Assert.Throws<DomainException>(() => RasterImageValidation.Sanitize(corrupted));
        Assert.Throws<DomainException>(() => RasterImageValidation.Sanitize([.. png, .. Encoding.UTF8.GetBytes("<script>not an image</script>")]));
        Assert.Throws<DomainException>(() => RasterImageValidation.Sanitize(Png(9000, 1)));
        Assert.Throws<DomainException>(() => RasterImageValidation.Sanitize(Png(-1, 1))); // PNG uint32 0xffffffff must be a validation error, never overflow/500.
        Assert.Throws<DomainException>(() => RasterImageValidation.Sanitize(Png(8192, 8192)));
        Assert.Empty(Directory.EnumerateFiles(folder.Path, "*", SearchOption.AllDirectories));
    }
    [Fact]
    public async Task StoredImagesRoundTripThroughSharedInvitationAndPublicReload()
    {
        using var folder = new TempFolder(); var storage = new InvitationImageStorage(folder.Path);
        await using var connection = new SqliteConnection("Data Source=:memory:"); await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options;
        var owner = User.Create("Owner", "owner@upload.test", "0900000000", "hash", UserRole.Customer);
        var editor = User.Create("Editor", "editor@upload.test", "0900000001", "hash", UserRole.Customer);
        var profile = WeddingProfile.Create(owner.Id); profile.Update("Minh", "Anh", DateTime.UtcNow.AddDays(100), "HCM", 100, 10, "Classic", null);
        await using (var db = new ApplicationDbContext(options))
        {
            await db.Database.EnsureCreatedAsync(); db.Users.AddRange(owner, editor); db.WeddingProfiles.Add(profile); db.WeddingMembers.Add(WeddingMember.Create(profile.Id, editor.Id, "Editor")); await db.SaveChangesAsync();
            var scope = await WeddingAccess.ResolveAsync(db, new Current(editor.Id), default, write: true); Assert.Equal(owner.Id, scope.OwnerId);
            var png = Png(); var upload = await storage.SaveAsync(scope.OwnerId, "photo.png", "image/png", new MemoryStream(png), png.Length, default);
            var json = JsonSerializer.Serialize(new { album = new[] { upload.Url, "https://example.com/legacy-photo.jpg" } });
            var command = new SaveInvitationCommand("upload-roundtrip", "Minh", "Anh", DateTime.UtcNow.AddDays(100), "Venue", "Address", null, "Our wedding", upload.Url, null, "navy-cream", null, ContentJson: json, IsPublished: true);
            var saved = await new SaveInvitationCommandHandler(db, new Current(editor.Id)).Handle(command, default); Assert.Equal(owner.Id, saved.CustomerId);
            await Assert.ThrowsAsync<DomainException>(() => new SaveInvitationCommandHandler(db, new Current(editor.Id)).Handle(command with { CoverImageUrl = $"/uploads/invitations/{Guid.NewGuid():N}/{Guid.NewGuid():N}.png" }, default));
        }
        await using (var reloaded = new ApplicationDbContext(options))
        {
            var invitation = await new GetPublicInvitationQueryHandler(reloaded).Handle(new("upload-roundtrip"), default);
            Assert.StartsWith($"/uploads/invitations/{owner.Id:N}/", invitation.CoverImageUrl);
            using var json = JsonDocument.Parse(invitation.ContentJson!); Assert.Equal(invitation.CoverImageUrl, json.RootElement.GetProperty("album")[0].GetString()); Assert.Equal("https://example.com/legacy-photo.jpg", json.RootElement.GetProperty("album")[1].GetString());
            Assert.True(File.Exists(storage.Resolve(owner.Id, invitation.CoverImageUrl!.Split('/').Last())));
        }
    }
    [Fact]
    public async Task ViewerCannotUploadIntoSharedWeddingAndLegacyUrlsRemainValid()
    {
        await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite("Data Source=:memory:").Options); await db.Database.OpenConnectionAsync(); await db.Database.EnsureCreatedAsync();
        var owner = User.Create("Owner", "owner@upload.test", "0900000000", "hash", UserRole.Customer); var viewer = User.Create("Viewer", "viewer@upload.test", "0900000001", "hash", UserRole.Customer); db.Users.AddRange(owner, viewer);
        var profile = WeddingProfile.Create(owner.Id); db.WeddingProfiles.Add(profile); db.WeddingMembers.Add(WeddingMember.Create(profile.Id, viewer.Id, "Viewer")); await db.SaveChangesAsync();
        await Assert.ThrowsAsync<DomainException>(() => WeddingAccess.ResolveAsync(db, new Current(viewer.Id), default, write: true));
        Assert.True(InvitationImageUrls.IsValid("https://example.com/already-saved.jpg", owner.Id)); Assert.True(InvitationImageUrls.IsValid("http://example.com/legacy.png", owner.Id));
        Assert.False(InvitationImageUrls.IsValid("javascript:alert(1)", owner.Id)); Assert.False(InvitationImageUrls.IsValid("/uploads/invitations/../../appsettings.json", owner.Id));
        Assert.False(InvitationImageUrls.IsValid($"/uploads/invitations/{Guid.NewGuid():N}/{Guid.NewGuid():N}.png", owner.Id));
    }
    [Fact]
    public void WebpFrameCannotBypassDimensionLimitUsingSmallExtendedCanvas()
    {
        using var payload = new MemoryStream(); payload.Write(Encoding.ASCII.GetBytes("WEBP"));
        payload.Write(Encoding.ASCII.GetBytes("VP8X")); var length = new byte[4]; BinaryPrimitives.WriteUInt32LittleEndian(length, 10); payload.Write(length); payload.Write(new byte[10]);
        payload.Write(Encoding.ASCII.GetBytes("VP8 ")); payload.Write(length); var frame = new byte[10]; frame[3] = 0x9d; frame[4] = 1; frame[5] = 0x2a;
        BinaryPrimitives.WriteUInt16LittleEndian(frame.AsSpan(6), 8192); BinaryPrimitives.WriteUInt16LittleEndian(frame.AsSpan(8), 8192); payload.Write(frame);
        using var webp = new MemoryStream(); webp.Write(Encoding.ASCII.GetBytes("RIFF")); BinaryPrimitives.WriteUInt32LittleEndian(length, (uint)payload.Length); webp.Write(length); payload.Position = 0; payload.CopyTo(webp);
        Assert.Throws<DomainException>(() => RasterImageValidation.Sanitize(webp.ToArray()));
    }
    [Fact]
    public async Task CreatesNonPersonalSmallPngFixtureForBrowserQa()
    {
        var directory = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "WeddingUploadQa", Guid.NewGuid().ToString("N")); Directory.CreateDirectory(directory);
        var fixture = System.IO.Path.Combine(directory, "wedding-upload-sample.png"); await File.WriteAllBytesAsync(fixture, Png()); Assert.True(File.Exists(fixture));
    }
    private static byte[] Png(int width = 320, int height = 180, bool metadata = false)
    {
        using var png = new MemoryStream(); png.Write([137, 80, 78, 71, 13, 10, 26, 10]);
        var header = new byte[13]; BinaryPrimitives.WriteUInt32BigEndian(header, (uint)width); BinaryPrimitives.WriteUInt32BigEndian(header.AsSpan(4), (uint)height); header[8] = 8; header[9] = 6; Chunk(png, "IHDR", header);
        if (metadata) Chunk(png, "tEXt", Encoding.ASCII.GetBytes("Location\0private-location"));
        using var pixels = new MemoryStream();
        var rows = width * (long)height <= 1000000 ? height : 1;
        for (var y = 0; y < rows; y++) { pixels.WriteByte(0); for (var x = 0; x < width; x++) pixels.Write(y < height / 2 ? [37, 51, 76, 255] : x < width / 3 ? [210, 172, 43, 255] : [242, 239, 228, 255]); }
        using var compressed = new MemoryStream(); using (var zlib = new ZLibStream(compressed, CompressionLevel.Fastest, true)) { pixels.Position = 0; pixels.CopyTo(zlib); }
        Chunk(png, "IDAT", compressed.ToArray()); Chunk(png, "IEND", []); return png.ToArray();
    }
    private static void Chunk(Stream stream, string name, byte[] data)
    {
        var length = new byte[4]; BinaryPrimitives.WriteUInt32BigEndian(length, (uint)data.Length); stream.Write(length); var type = Encoding.ASCII.GetBytes(name); stream.Write(type); stream.Write(data);
        var crc = new byte[4]; BinaryPrimitives.WriteUInt32BigEndian(crc, RasterImageValidation.Crc32([.. type, .. data])); stream.Write(crc);
    }
    private sealed record Current(Guid Id) : ICurrentUserService { public Guid? UserId => Id; public string? Email => "owner@upload.test"; public string? Role => "Customer"; }
    private sealed class TempFolder : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "WeddingUploadTest_" + Guid.NewGuid().ToString("N"));
        public TempFolder() => Directory.CreateDirectory(Path);
        public void Dispose() { if (!System.IO.Path.GetFullPath(Path).StartsWith(System.IO.Path.GetFullPath(System.IO.Path.GetTempPath()) + "WeddingUploadTest_", StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Invalid test cleanup path."); Directory.Delete(Path, true); }
    }
}
