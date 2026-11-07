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

public sealed class VerificationDocumentTests
{
    [Theory]
    [InlineData(".pdf", "application/pdf")]
    [InlineData(".doc", "application/msword")]
    [InlineData(".docx", "application/vnd.openxmlformats-officedocument.wordprocessingml.document")]
    public async Task PrivateOwnerScopedDocumentsPersistAcrossDeployments(string extension, string mime)
    {
        using var folder = new TestFolder(); var owner = Guid.NewGuid();
        var listingRoot = Path.Combine(folder.Path, "private", "uploads", "listings");
        var first = new VerificationDocumentStorage(Path.Combine(folder.Path, "old-site"), configuredListingRoot: listingRoot);
        var bytes = Bytes(extension);
        var saved = await first.SaveAsync(owner, "../../private-name" + extension, mime, new MemoryStream(bytes), bytes.Length, default);
        var next = new VerificationDocumentStorage(Path.Combine(folder.Path, "new-site"), configuredListingRoot: listingRoot);
        Assert.Equal(Path.Combine(folder.Path, "private", "uploads", "verification-documents"), next.Root);
        Assert.True(next.IsOwnedExistingReference(owner, saved.Url));
        Assert.False(next.IsOwnedExistingReference(Guid.NewGuid(), saved.Url));
        Assert.DoesNotContain("private-name", saved.Url);
        Assert.Equal(bytes, System.IO.File.ReadAllBytes(next.Resolve(owner, saved.Url.Split('/').Last())!));
        Assert.False(Directory.Exists(Path.Combine(folder.Path, "old-site")));
        Assert.Throws<InvalidOperationException>(() => new VerificationDocumentStorage(folder.Path, "wwwroot/private"));
    }

    [Theory]
    [InlineData("../../web.config")]
    [InlineData("0123456789abcdef0123456789abcdef.pdf?token=abc")]
    [InlineData("0123456789abcdef0123456789abcdef.docm")]
    [InlineData("0123456789abcdef0123456789abcdef.pdf/../x")]
    [InlineData("0123456789abcdef0123456789abcdef.PDF")]
    [InlineData("")]
    public void ResolveRejectsPathsQueriesAndNonCanonicalExtensions(string name)
    { using var folder = new TestFolder(); Assert.Null(new VerificationDocumentStorage(folder.Path).Resolve(Guid.NewGuid(), name)); }

    [Fact]
    public async Task InvalidSignaturesMimesSizesMalformedAndActiveDocumentsNeverWrite()
    {
        using var folder = new TestFolder(); var storage = new VerificationDocumentStorage(folder.Path); var owner = Guid.NewGuid();
        foreach (var extension in new[] { ".pdf", ".doc", ".docx" })
        {
            var mime = VerificationDocumentStorage.ContentType(extension);
            await Assert.ThrowsAsync<DomainException>(() => storage.SaveAsync(owner, "a" + extension, mime, new MemoryStream(Encoding.UTF8.GetBytes("<html>not a document</html>")), 27, default));
            var bytes = Bytes(extension);
            await Assert.ThrowsAsync<DomainException>(() => storage.SaveAsync(owner, "a" + extension, "text/html", new MemoryStream(bytes), bytes.Length, default));
            await Assert.ThrowsAsync<DomainException>(() => storage.SaveAsync(owner, "a" + extension, mime, new MemoryStream(bytes), bytes.Length - 1, default));
            await Assert.ThrowsAsync<DomainException>(() => storage.SaveAsync(owner, "a" + extension, mime, new MemoryStream(bytes), VerificationDocumentStorage.MaxFileBytes + 1, default));
        }
        var activePdf = Encoding.ASCII.GetBytes("%PDF-1.4\n/Catalog /JavaScript /JS (bad)\nstartxref\n0\n%%EOF");
        await Assert.ThrowsAsync<DomainException>(() => storage.SaveAsync(owner, "a.pdf", "application/pdf", new MemoryStream(activePdf), activePdf.Length, default));
        var macro = Docx(extraName: "word/vbaProject.bin");
        await Assert.ThrowsAsync<DomainException>(() => storage.SaveAsync(owner, "a.docx", VerificationDocumentStorage.ContentType(".docx"), new MemoryStream(macro), macro.Length, default));
        var external = Docx(external: true);
        await Assert.ThrowsAsync<DomainException>(() => storage.SaveAsync(owner, "a.docx", VerificationDocumentStorage.ContentType(".docx"), new MemoryStream(external), external.Length, default));
        Assert.False(Directory.Exists(storage.Root));
    }

    [Fact]
    public void RoutesRequireAuthenticationAndHaveNoAnonymousDocumentFallback()
    {
        var get = typeof(UploadController).GetMethod(nameof(UploadController.GetDocument))!;
        Assert.Empty(get.GetCustomAttributes(typeof(AllowAnonymousAttribute), false));
        Assert.Equal("VendorOwner,Moderator,SuperAdmin", Assert.Single(get.GetCustomAttributes(typeof(AuthorizeAttribute), false).Cast<AuthorizeAttribute>()).Roles);
        Assert.Equal("/uploads/documents/{vendorId:guid}/{fileName}", Assert.Single(get.GetCustomAttributes(typeof(HttpGetAttribute), false).Cast<HttpGetAttribute>()).Template);
        Assert.Equal("VendorOwner", Assert.Single(typeof(UploadController).GetMethod(nameof(UploadController.UploadDocuments))!.GetCustomAttributes(typeof(AuthorizeAttribute), false).Cast<AuthorizeAttribute>()).Roles);
    }

    [Theory]
    [InlineData("http://example.invalid/private.pdf")]
    [InlineData("https://user:password@example.invalid/private.pdf")]
    [InlineData("https://external.example.invalid/new-unowned.pdf")]
    [InlineData("/uploads/documents/0123456789abcdef0123456789abcdef.pdf")]
    [InlineData("/uploads/listings/0123456789abcdef0123456789abcdef.png")]
    [InlineData("javascript:alert(1)")]
    public void LegacyCompatibilityDoesNotAcceptHttpUnownedFlatPathsOrOtherUploads(string url)
    { using var folder = new TestFolder(); Assert.Throws<DomainException>(() => VerificationDocumentAccess.NormalizeReference(url, Guid.NewGuid(), new VerificationDocumentStorage(folder.Path))); }

    [SqlServerFact]
    public async Task OwnedSqlChecksActiveOwnerStaffCrossShopAndBatchRollback()
    {
        await using var owned = await CommissionSqlTestDatabase.CreateAsync(migrate: false);
        await owned.Db.Database.EnsureCreatedAsync(); // Fresh owned SQL test only; no application schema/migration.
        using var folder = new TestFolder();
        var owner = User.Create("Document owner", "owner@documents.example.invalid", "0900000881", "qa-hash", UserRole.VendorOwner);
        var other = User.Create("Other owner", "other@documents.example.invalid", "0900000882", "qa-hash", UserRole.VendorOwner);
        var vendor = Vendor.Create(owner.Id, "QA legal owner", "doc-owner", "Owner", "0900000881", "Hà Nội", .08m);
        var foreign = Vendor.Create(other.Id, "QA foreign", "doc-foreign", "Other", "0900000882", "Hà Nội", .08m);
        owned.Db.Users.AddRange(owner, other); owned.Db.Vendors.AddRange(vendor, foreign); await owned.Db.SaveChangesAsync();
        var uploader = Controller(folder.Path, owned.Db, new Current(owner.Id, "VendorOwner"));
        var bytes = Bytes(".pdf");
        var uploaded = Assert.IsType<OkObjectResult>(await uploader.UploadDocuments([File(bytes, "private.pdf", "application/pdf")], default));
        var url = JsonSerializer.SerializeToElement(uploaded.Value).GetProperty("urls")[0].GetString()!;
        var fileName = url.Split('/').Last();
        var result = Assert.IsType<PhysicalFileResult>(await uploader.GetDocument(vendor.Id, fileName, default));
        Assert.Equal(fileName, result.FileDownloadName); Assert.False(result.EnableRangeProcessing);
        Assert.Contains("no-store", uploader.Response.Headers.CacheControl.ToString());
        Assert.Equal("nosniff", uploader.Response.Headers["X-Content-Type-Options"].ToString());
        Assert.IsType<NotFoundResult>(await Controller(folder.Path, owned.Db, new Current(other.Id, "VendorOwner")).GetDocument(vendor.Id, fileName, default));
        Assert.IsType<NotFoundResult>(await Controller(folder.Path, owned.Db, new Current(null, null)).GetDocument(vendor.Id, fileName, default));
        foreach (var role in new[] { UserRole.Moderator, UserRole.SuperAdmin, UserRole.Customer, UserRole.Finance, UserRole.CustomerCare })
        {
            var staff = User.Create("QA " + role, role + "@documents.example.invalid", "0900000883", "qa-hash", role);
            owned.Db.Users.Add(staff); await owned.Db.SaveChangesAsync();
            var controller = Controller(folder.Path, owned.Db, new Current(staff.Id, role.ToString()));
            var read = await controller.GetDocument(vendor.Id, fileName, default);
            if (role is UserRole.SuperAdmin or UserRole.Moderator) Assert.IsType<PhysicalFileResult>(read); else Assert.IsType<NotFoundResult>(read);
            await Assert.ThrowsAsync<DomainException>(() => controller.UploadDocuments([File(bytes, "private.pdf", "application/pdf")], default));
            staff.SetStatus(false); await owned.Db.SaveChangesAsync();
            Assert.IsType<NotFoundResult>(await controller.GetDocument(vendor.Id, fileName, default));
        }
        var invalid = Encoding.UTF8.GetBytes("wrong content");
        await Assert.ThrowsAsync<DomainException>(() => uploader.UploadDocuments([File(bytes, "first.pdf", "application/pdf"), File(invalid, "second.pdf", "application/pdf")], default));
        Assert.Single(Directory.EnumerateFiles(Path.GetDirectoryName(result.FileName)!));
        Assert.True(System.IO.File.Exists(result.FileName));
        owned.Db.Entry(vendor).Property(v => v.IsActive).CurrentValue = false; await owned.Db.SaveChangesAsync();
        await Assert.ThrowsAsync<DomainException>(() => uploader.UploadDocuments([File(bytes, "a.pdf", "application/pdf")], default));
        Assert.IsType<NotFoundResult>(await uploader.GetDocument(vendor.Id, fileName, default));
    }

    [SqlServerFact]
    public async Task SubmissionRequiresRealTaxProfileAndOwnExistingEvidenceWithoutManufacturingConsent()
    {
        await using var owned = await CommissionSqlTestDatabase.CreateAsync(migrate: false); await owned.Db.Database.EnsureCreatedAsync();
        using var folder = new TestFolder();
        var owner = User.Create("QA owner", "submit@documents.example.invalid", "0900000881", "qa-hash", UserRole.VendorOwner);
        var vendor = Vendor.Create(owner.Id, "QA own", "submission-owner", "Owner", "0900000881", "Hà Nội", .08m);
        owned.Db.Users.Add(owner); owned.Db.Vendors.Add(vendor); await owned.Db.SaveChangesAsync();
        var store = new VerificationDocumentStorage(folder.Path); var pdf = Bytes(".pdf");
        var saved = await store.SaveAsync(vendor.Id, "legal.pdf", "application/pdf", new MemoryStream(pdf), pdf.Length, default);
        var handler = new SubmitVerificationCommandHandler(owned.Db, new Current(owner.Id, "VendorOwner"), store);
        await Assert.ThrowsAsync<DomainException>(() => handler.Handle(new("1234567890", [saved.Url], null), default));
        Assert.Empty(await owned.Db.VendorTaxProfiles.ToListAsync()); Assert.Empty(await owned.Db.VendorVerifications.ToListAsync());
        var profile = VendorTaxProfile.Create(vendor.Id); profile.Update("Actual test legal name", "1234567890", "VCB", "123456789", "Actual test holder", false);
        owned.Db.VendorTaxProfiles.Add(profile); await owned.Db.SaveChangesAsync();
        await Assert.ThrowsAsync<DomainException>(() => handler.Handle(new("1234567890", [saved.Url], null), default));
        Assert.False(profile.AgreementAccepted);
        profile.Update("Actual test legal name", "1234567890", "VCB", "123456789", "Actual test holder", true); await owned.Db.SaveChangesAsync();
        var other = await store.SaveAsync(Guid.NewGuid(), "other.pdf", "application/pdf", new MemoryStream(pdf), pdf.Length, default);
        await Assert.ThrowsAsync<DomainException>(() => handler.Handle(new("1234567890", [other.Url], null), default));
        Assert.Equal(VerificationStatus.Draft, profile.Status);
        await Assert.ThrowsAsync<DomainException>(() => handler.Handle(new("1234567890", [$"https://weddingmatch.website{other.Url}"], null), default));
        var legacy = VendorVerification.Create(vendor.Id, "OLD", "[\"https://legacy.example.invalid/legal.pdf\"]", null);
        legacy.Review(Guid.NewGuid(), false, "Re-upload current evidence"); owned.Db.VendorVerifications.Add(legacy); await owned.Db.SaveChangesAsync();
        var result = await handler.Handle(new("1234567890", [saved.Url], null), default);
        Assert.Equal("Submitted", result.Status); Assert.False(vendor.IsVerified);
        Assert.Equal("VCB", profile.BankCode); Assert.Equal("123456789", profile.BankAccountNumber); Assert.True(profile.AgreementAccepted);
        Assert.Equal(.08m, vendor.CommissionRate);
        Assert.Contains("https://legacy.example.invalid/legal.pdf", (await new GetVerificationsQueryHandler(owned.Db, new Current(owner.Id, "VendorOwner")).Handle(new(true), default)).Single(v => v.Id == legacy.Id).DocumentUrls);
        await Assert.ThrowsAsync<DomainException>(() => handler.Handle(new("1234567890", [saved.Url], null), default));
    }

    private static FormFile File(byte[] bytes, string name, string mime) => new(new MemoryStream(bytes), 0, bytes.Length, "files", name) { Headers = new HeaderDictionary(), ContentType = mime };
    private static UploadController Controller(string root, IApplicationDbContext db, ICurrentUserService actor) => new(new Host(root),
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Uploads:VerificationRoot"] = Path.Combine(root, "private-documents") }).Build(), db, actor)
        { ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() } };
    private sealed record Current(Guid? UserId, string? Role) : ICurrentUserService { public string? Email => null; }
    private sealed class Host(string root) : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "QA"; public string EnvironmentName { get; set; } = "Testing";
        public string ContentRootPath { get; set; } = root; public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
        public string WebRootPath { get; set; } = Path.Combine(root, "wwwroot"); public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
    }
    private static byte[] Bytes(string extension) => extension switch { ".docx" => Docx(), ".doc" => Doc(), _ => Encoding.ASCII.GetBytes("%PDF-1.4\n1 0 obj\n<< /Type /Catalog >>\nendobj\nstartxref\n9\n%%EOF") };
    private static byte[] Docx(string? extraName = null, bool external = false)
    {
        using var memory = new MemoryStream();
        using (var zip = new ZipArchive(memory, ZipArchiveMode.Create, true))
        {
            Write("[Content_Types].xml", "<Types xmlns='http://schemas.openxmlformats.org/package/2006/content-types'><Override PartName='/word/document.xml' ContentType='application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml'/></Types>");
            Write("word/document.xml", "<w:document xmlns:w='http://schemas.openxmlformats.org/wordprocessingml/2006/main'><w:body/></w:document>");
            Write("_rels/.rels", external ? "<Relationships><Relationship TargetMode='External' Target='https://remote.example.invalid'/></Relationships>" : "<Relationships/>");
            if (extraName != null) Write(extraName, "unsupported macro");
            void Write(string name, string value) { using var writer = new StreamWriter(zip.CreateEntry(name).Open()); writer.Write(value); }
        }
        return memory.ToArray();
    }
    private static byte[] Doc()
    {
        var data = new byte[2048]; new byte[] { 0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1 }.CopyTo(data, 0);
        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(26), 3); BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(28), 0xFFFE); BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(30), 9);
        BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(44), 1); BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(48), 1); BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(76), 0);
        BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(512), -3); BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(516), -2); BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(520), -2);
        var name = Encoding.Unicode.GetBytes("WordDocument\0"); name.CopyTo(data, 1024); BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(1088), (ushort)name.Length); data[1090] = 2;
        return data;
    }
    private sealed class TestFolder : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "WeddingVerificationTest_" + Guid.NewGuid().ToString("N"));
        public void Dispose()
        {
            var full = System.IO.Path.GetFullPath(Path); var prefix = System.IO.Path.Combine(System.IO.Path.GetFullPath(System.IO.Path.GetTempPath()), "WeddingVerificationTest_");
            if (!full.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) || !Guid.TryParseExact(full[prefix.Length..], "N", out _)) throw new InvalidOperationException();
            if (Directory.Exists(full)) Directory.Delete(full, true);
        }
    }
}
