using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Wedding.Application.Common.Interfaces;
using Wedding.Application.Features.Governance;
using Wedding.Domain.Entities;
using Wedding.Domain.Enums;
using Wedding.Domain.Exceptions;
using Wedding.Infrastructure.Services;
using Wedding.WebApi.Controllers;

namespace Wedding.Tests;

public sealed class ArticleImageUploadTests
{
    private const string ManagedImage = "/uploads/articles/0123456789abcdef0123456789abcdef.png";

    [Fact]
    public async Task RasterPersistsAcrossSiteInstancesInSiblingPrivateFolderWithMetadataRemoved()
    {
        using var folder = new TestFolder();
        var listingRoot = Path.Combine(folder.Path, "private", "uploads", "listings");
        var storage = new ArticleImageStorage(Path.Combine(folder.Path, "old-site"), configuredListingRoot: listingRoot);
        var png = Png(true);
        var image = await storage.SaveAsync("../../private-name.PNG", "image/png", new MemoryStream(png), png.Length, default);
        var next = new ArticleImageStorage(Path.Combine(folder.Path, "new-site"), configuredListingRoot: listingRoot);
        Assert.Equal(Path.Combine(folder.Path, "private", "uploads", "articles"), next.Root);
        Assert.Equal(storage.Root, next.Root);
        Assert.StartsWith("/uploads/articles/", image.Url);
        Assert.DoesNotContain("private-name", image.Url);
        var path = next.Resolve(image.Url.Split('/').Last())!;
        Assert.True(File.Exists(path));
        var saved = await File.ReadAllBytesAsync(path);
        Assert.DoesNotContain("secret-location", Encoding.ASCII.GetString(saved));
        Assert.Equal(1, RasterImageValidation.Sanitize(saved).Width);
        Assert.False(Directory.Exists(Path.Combine(folder.Path, "old-site")));
        Assert.Equal(Path.GetFullPath(Path.Combine(folder.Path, "App_Data", "article-uploads")), new ArticleImageStorage(folder.Path).Root);
        Assert.Equal(Path.Combine(folder.Path, "override"), new ArticleImageStorage(folder.Path, "override", listingRoot).Root);
    }

    [Theory]
    [InlineData("../../appsettings.json")]
    [InlineData("0123456789abcdef0123456789abcdef.svg")]
    [InlineData("0123456789abcdef0123456789abcdef.jpeg")]
    [InlineData("0123456789abcdef0123456789abcdef.PNG")]
    [InlineData("0123456789abcdef0123456789abcdef.png?x=1")]
    [InlineData("0123456789abcdef0123456789abcdef.png/../a")]
    [InlineData("/0123456789abcdef0123456789abcdef.png")]
    [InlineData("")]
    public void ResolveRejectsClientPathsAndNonCanonicalNames(string name)
    {
        using var folder = new TestFolder();
        Assert.Null(new ArticleImageStorage(folder.Path).Resolve(name));
    }

    [Fact]
    public async Task InvalidRasterTypeSizeLengthAndDamageDoNotWriteFiles()
    {
        using var folder = new TestFolder();
        var storage = new ArticleImageStorage(folder.Path);
        var png = Png();
        foreach (var (name, mime) in new[] { ("a.svg", "image/svg+xml"), ("a.jpg", "image/jpeg"), ("a.png", "text/html") })
            await Assert.ThrowsAsync<DomainException>(() => storage.SaveAsync(name, mime, new MemoryStream(png), png.Length, default));
        await Assert.ThrowsAsync<DomainException>(() => storage.SaveAsync("a.png", "image/png", new MemoryStream(png), ArticleImageStorage.MaxFileBytes + 1, default));
        await Assert.ThrowsAsync<DomainException>(() => storage.SaveAsync("a.png", "image/png", new MemoryStream(new byte[ArticleImageStorage.MaxFileBytes + 1]), 1, default));
        await Assert.ThrowsAsync<DomainException>(() => storage.SaveAsync("a.png", "image/png", new MemoryStream(png), 1, default));
        png[^1] ^= 1;
        await Assert.ThrowsAsync<DomainException>(() => storage.SaveAsync("a.png", "image/png", new MemoryStream(png), png.Length, default));
        Assert.False(Directory.Exists(storage.Root));
    }

    [Fact]
    public async Task QuotaRejectsUploadWithoutOverwritingExistingFiles()
    {
        using var folder = new TestFolder();
        var storage = new ArticleImageStorage(folder.Path);
        Directory.CreateDirectory(storage.Root);
        for (var i = 0; i < 5000; i++) File.WriteAllBytes(Path.Combine(storage.Root, $"owned-{i}"), []);
        var bytes = Png();
        await Assert.ThrowsAsync<DomainException>(() => storage.SaveAsync("a.png", "image/png", new MemoryStream(bytes), bytes.Length, default));
        Assert.Equal(5000, Directory.EnumerateFiles(storage.Root).Count());
    }

    [Theory]
    [InlineData("/uploads/articles/0123456789abcdef0123456789abcdef.png")]
    [InlineData("/uploads/articles/0123456789abcdef0123456789abcdef.jpg")]
    [InlineData("/uploads/articles/0123456789abcdef0123456789abcdef.webp")]
    [InlineData("https://images.example.invalid/old-cover.jpg")]
    public void ManagedRasterAndExistingHttpsReferencesAreAccepted(string url) => Assert.Equal(url, ArticleImageReferences.Normalize(url));

    [Theory]
    [InlineData("/uploads/articles/../../web.config")]
    [InlineData("/uploads/listings/0123456789abcdef0123456789abcdef.png")]
    [InlineData("/uploads/articles/0123456789abcdef0123456789abcdef.png?x=1")]
    [InlineData("//evil.example.invalid/image.png")]
    [InlineData("http://images.example.invalid/cover.png")]
    [InlineData("javascript:alert(1)")]
    [InlineData("https://user:secret@example.invalid/image.png")]
    public void InvalidImageReferencesAreRejected(string url) => Assert.Throws<DomainException>(() => ArticleImageReferences.Normalize(url));

    [Fact]
    public void RouteRequiresCmsRolesAndPublicGetDoesNotGrantUpload()
    {
        var post = typeof(ArticleImagesController).GetMethod(nameof(ArticleImagesController.Upload))!;
        Assert.Equal("SuperAdmin,Moderator", post.GetCustomAttributes(typeof(AuthorizeAttribute), false).Cast<AuthorizeAttribute>().Single().Roles);
        Assert.Equal("api/admin/articles/images", post.GetCustomAttributes(typeof(HttpPostAttribute), false).Cast<HttpPostAttribute>().Single().Template);
        var get = typeof(ArticleImagesController).GetMethod(nameof(ArticleImagesController.Get))!;
        Assert.Single(get.GetCustomAttributes(typeof(AllowAnonymousAttribute), false));
        Assert.Equal("/uploads/articles/{fileName}", get.GetCustomAttributes(typeof(HttpGetAttribute), false).Cast<HttpGetAttribute>().Single().Template);
        Assert.Null(ArticleImageReferences.Normalize("  "));
    }

    [SqlServerFact]
    public async Task ActiveCmsActorsUploadCreateEditPublishUnpublishAndDeleteUsingOwnedSql()
    {
        await using var owned = await CommissionSqlTestDatabase.CreateAsync();
        using var folder = new TestFolder();
        foreach (var role in new[] { UserRole.SuperAdmin, UserRole.Moderator })
        {
            var admin = User.Create("QA CMS " + role, role.ToString().ToLowerInvariant() + "@article-qa.example.invalid", "0900000881", "not-a-real-password-hash", role);
            owned.Db.Users.Add(admin); await owned.Db.SaveChangesAsync();
            var actor = new Current(admin.Id, role.ToString());
            var controller = Controller(folder.Path, owned.Db, actor);
            var bytes = Png(true);
            var upload = Assert.IsType<OkObjectResult>(await controller.Upload(new FormFile(new MemoryStream(bytes), 0, bytes.Length, "file", "cover.png") { Headers = new HeaderDictionary(), ContentType = "image/png" }, default));
            var url = JsonSerializer.SerializeToElement(upload.Value).GetProperty("url").GetString()!;
            var result = Assert.IsType<PhysicalFileResult>(controller.Get(url.Split('/').Last()));
            Assert.Equal("image/png", result.ContentType);
            Assert.Equal("nosniff", controller.Response.Headers["X-Content-Type-Options"].ToString());
            var audit = new AuditLogger(owned.Db, actor);
            var draft = await new SaveArticleCommandHandler(owned.Db, actor, audit).Handle(Input(role.ToString(), url), default);
            Assert.False(draft.IsPublished); Assert.Equal(url, draft.ThumbnailUrl);
            var publicQuery = new GetArticlesQueryHandler(owned.Db, new Current(null, null));
            Assert.Empty(await publicQuery.Handle(new(false, draft.Slug), default));
            Assert.Single(await new GetArticlesQueryHandler(owned.Db, actor).Handle(new(true, draft.Slug), default));
            var update = new UpdateArticleHandler(owned.Db, audit, actor);
            await update.Handle(new(draft.Id, "Published cover", draft.Slug, "Preview", "Actual article content", url, "Cẩm nang cưới", true, []), default);
            Assert.Equal(url, (await publicQuery.Handle(new(false, draft.Slug), default)).Single().ThumbnailUrl);
            await update.Handle(new(draft.Id, "Draft again", draft.Slug, "Preview", "Actual article content", url, "Cẩm nang cưới", false, []), default);
            Assert.Empty(await publicQuery.Handle(new(false, draft.Slug), default));
            await new DeleteArticleHandler(owned.Db, audit, actor).Handle(new(draft.Id), default);
            Assert.False(await owned.Db.Articles.AnyAsync(a => a.Id == draft.Id));
            Assert.True(File.Exists(result.FileName)); // An article edit/delete never removes a saved image.
        }
    }

    [SqlServerFact]
    public async Task NonCmsStaleAndInactiveActorsCannotReadDraftsWriteArticlesOrUpload()
    {
        await using var owned = await CommissionSqlTestDatabase.CreateAsync();
        using var folder = new TestFolder();
        foreach (var role in new[] { UserRole.Customer, UserRole.VendorOwner, UserRole.Finance, UserRole.CustomerCare })
        {
            var user = User.Create("QA denied " + role, role.ToString().ToLowerInvariant() + "@article-qa.example.invalid", "0900000882", "test-hash", role);
            owned.Db.Users.Add(user); await owned.Db.SaveChangesAsync();
            await AssertDenied(new Current(user.Id, role.ToString()));
            await AssertDenied(new Current(user.Id, "SuperAdmin"));
        }
        var inactive = User.Create("QA inactive", "inactive@article-qa.example.invalid", "0900000883", "test-hash", UserRole.SuperAdmin);
        inactive.SetStatus(false); owned.Db.Users.Add(inactive); await owned.Db.SaveChangesAsync();
        await AssertDenied(new Current(inactive.Id, "SuperAdmin"));
        await AssertDenied(new Current(Guid.NewGuid(), "SuperAdmin"));
        await AssertDenied(new Current(null, null));
        Assert.Empty(await owned.Db.Articles.ToListAsync());
        Assert.False(Directory.Exists(Path.Combine(folder.Path, "images")));

        async Task AssertDenied(Current actor)
        {
            var audit = new AuditLogger(owned.Db, actor);
            await Assert.ThrowsAsync<DomainException>(() => ArticleCmsAccess.RequireAsync(owned.Db, actor, default));
            await Assert.ThrowsAsync<DomainException>(() => new SaveArticleCommandHandler(owned.Db, actor, audit).Handle(Input("denied", ManagedImage), default));
            await Assert.ThrowsAsync<DomainException>(() => new UpdateArticleHandler(owned.Db, audit, actor).Handle(new(Guid.NewGuid(), "No", "denied", "", "No", null, "Cẩm nang", true, []), default));
            await Assert.ThrowsAsync<DomainException>(() => new DeleteArticleHandler(owned.Db, audit, actor).Handle(new(Guid.NewGuid()), default));
            await Assert.ThrowsAsync<DomainException>(() => new GetArticlesQueryHandler(owned.Db, actor).Handle(new(true, null), default));
            var bytes = Png();
            await Assert.ThrowsAsync<DomainException>(() => Controller(folder.Path, owned.Db, actor).Upload(new FormFile(new MemoryStream(bytes), 0, bytes.Length, "file", "cover.png") { Headers = new HeaderDictionary(), ContentType = "image/png" }, default));
        }
    }

    private static SaveArticleCommand Input(string slug, string? image) => new("QA article", "qa-" + slug.ToLowerInvariant(), "Preview", "Actual article content", image, "Cẩm nang cưới", false);
    private static ArticleImagesController Controller(string root, IApplicationDbContext db, ICurrentUserService actor) =>
        new(new HostEnvironment(root), new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Uploads:ArticleRoot"] = Path.Combine(root, "images") }).Build(), db, actor)
        { ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() } };
    private sealed record Current(Guid? UserId, string? Role) : ICurrentUserService { public string? Email => null; }
    private sealed class HostEnvironment(string root) : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "QA";
        public string EnvironmentName { get; set; } = "Testing";
        public string ContentRootPath { get; set; } = root;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
        public string WebRootPath { get; set; } = Path.Combine(root, "wwwroot");
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
    }
    private static byte[] Png(bool metadata = false)
    {
        using var stream = new MemoryStream(); stream.Write([137, 80, 78, 71, 13, 10, 26, 10]);
        var header = new byte[13]; BinaryPrimitives.WriteUInt32BigEndian(header, 1); BinaryPrimitives.WriteUInt32BigEndian(header.AsSpan(4), 1); header[8] = 8; header[9] = 6;
        Chunk(stream, "IHDR", header);
        if (metadata) Chunk(stream, "tEXt", Encoding.ASCII.GetBytes("Location\0secret-location"));
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
    private sealed class TestFolder : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "WeddingArticleUploadTest_" + Guid.NewGuid().ToString("N"));
        public TestFolder() => Directory.CreateDirectory(Path);
        public void Dispose()
        {
            var full = System.IO.Path.GetFullPath(Path);
            var prefix = System.IO.Path.Combine(System.IO.Path.GetFullPath(System.IO.Path.GetTempPath()), "WeddingArticleUploadTest_");
            if (!full.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) || !Guid.TryParseExact(full[prefix.Length..], "N", out _)) throw new InvalidOperationException("Refuse unsafe cleanup path.");
            Directory.Delete(full, true);
        }
    }
}
