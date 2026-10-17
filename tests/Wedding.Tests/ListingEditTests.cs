using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Wedding.Application.Common.Interfaces;
using Wedding.Application.Features.Listings;
using Wedding.Application.Features.Listings.Commands.UpdateListing;
using Wedding.Application.Features.Listings.Commands.CreateListing;
using Wedding.Application.Features.Listings.Queries.GetListingDetail;
using Wedding.Domain.Entities;
using Wedding.Domain.Enums;
using Wedding.Domain.Exceptions;
using Wedding.Infrastructure.Persistence;

namespace Wedding.Tests;

public sealed class ListingEditTests
{
    private const string Description = "Mô tả đầy đủ đã lưu cùng toàn bộ album của dịch vụ cưới.";
    private const string LocalImage = "/uploads/listings/0123456789abcdef0123456789abcdef.png";

    [Fact]
    public async Task PriceOnlyUpdateWithOmittedMediaPreservesDescriptionEveryPhotoAndExistingCover()
    {
        await using var fixture = await Fixture.Create();
        var before = await fixture.Db.ListingMedias.AsNoTracking().OrderBy(media => media.DisplayOrder).ToListAsync();
        await fixture.Handler().Handle(fixture.Command() with { MinPrice = 1200 }, default);
        fixture.Db.ChangeTracker.Clear();
        var saved = await fixture.Db.Listings.SingleAsync();
        var after = await fixture.Db.ListingMedias.OrderBy(media => media.DisplayOrder).ToListAsync();
        Assert.Equal(1200, saved.MinPrice); Assert.Equal(Description, saved.Description);
        Assert.Equal(before.Select(media => media.Id), after.Select(media => media.Id));
        Assert.Equal(before.Select(media => media.MediaUrl), after.Select(media => media.MediaUrl));
        Assert.True(after[0].IsFeatured); Assert.Equal(3, after.Count);
    }

    [Fact]
    public async Task ReplacementAlbumSavesEveryPhotoInOrderWithExactlyOneFirstCoverAndCanBeReadBack()
    {
        await using var fixture = await Fixture.Create();
        var urls = new List<string> { "https://cdn.example.com/new-cover.jpg", LocalImage, "http://legacy.example.com/last.jpg" };
        await fixture.Handler().Handle(fixture.Command(urls) with { Title = "Updated service" }, default);
        fixture.Db.ChangeTracker.Clear();
        var detail = await new GetListingDetailQueryHandler(fixture.Db, new Current(fixture.Owner.Id)).Handle(new(fixture.Listing.Id), default);
        Assert.Equal("Updated service", detail.Title); Assert.Equal(Description, detail.Description);
        Assert.Equal(urls, detail.Media.Select(media => media.MediaUrl));
        Assert.Equal(new[] { 0, 1, 2 }, detail.Media.Select(media => media.DisplayOrder));
        Assert.True(detail.Media[0].IsFeatured); Assert.Single(detail.Media, media => media.IsFeatured);
        Assert.Equal(3, await fixture.Db.ListingMedias.CountAsync());
    }

    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("data:image/png;base64,AA")]
    [InlineData("blob:https://site.example/temp")]
    [InlineData("//cdn.example.com/image.png")]
    [InlineData("/uploads/listings/../../web.config")]
    [InlineData("/uploads/listings/%252e%252e/web.config")]
    [InlineData("https://cdn.example.com/../secret.png")]
    [InlineData("https://cdn.example.com/a/%2e%2e/secret.png")]
    [InlineData("https://name:pass@cdn.example.com/photo.jpg")]
    [InlineData("https://cdn.example.com/photo\n.jpg")]
    [InlineData("/uploads/invitations/0123456789abcdef0123456789abcdef/photo.png")]
    [InlineData("/uploads/listings/0123456789abcdef0123456789abcdef.svg")]
    public async Task InvalidReferencesCannotPartiallyChangeListingOrAlbum(string url)
    {
        await using var fixture = await Fixture.Create();
        var beforeIds = await fixture.Db.ListingMedias.Select(media => media.Id).ToListAsync();
        await Assert.ThrowsAsync<DomainException>(() => fixture.Handler().Handle(fixture.Command([url]) with { Title = "Must not persist" }, default));
        Assert.Equal("Original service", (await fixture.Db.Listings.AsNoTracking().SingleAsync()).Title);
        Assert.Equal(Description, (await fixture.Db.Listings.AsNoTracking().SingleAsync()).Description);
        Assert.Equal(beforeIds.Order(), (await fixture.Db.ListingMedias.Select(media => media.Id).ToListAsync()).Order());
    }

    [Fact]
    public async Task EmptyDuplicateOversizeAlbumsAndInvalidTextNeverMutateRows()
    {
        await using var fixture = await Fixture.Create();
        var invalid = new[]
        {
            fixture.Command([]), fixture.Command([LocalImage, LocalImage]),
            fixture.Command(Enumerable.Range(0, 11).Select(index => $"https://cdn.example.com/{index}.jpg").ToList()),
            fixture.Command([$"https://cdn.example.com/{new string('a', 500)}.jpg"]),
            fixture.Command() with { Description = "" }, fixture.Command() with { Description = new string('a', 4001) },
            fixture.Command() with { Location = new string('a', 201) }, fixture.Command() with { MinPrice = 9000, MaxPrice = 1000 }
        };
        foreach (var command in invalid)
            await Assert.ThrowsAsync<DomainException>(() => fixture.Handler().Handle(command with { Title = "Must not persist" }, default));
        Assert.Equal("Original service", (await fixture.Db.Listings.AsNoTracking().SingleAsync()).Title);
        Assert.Equal(3, await fixture.Db.ListingMedias.CountAsync());
    }

    [Theory]
    [InlineData(ListingStatus.Active)]
    [InlineData(ListingStatus.PendingApproval)]
    [InlineData(ListingStatus.Hidden)]
    public async Task NonEditableStatesCannotReplaceContentOrAlbum(ListingStatus status)
    {
        await using var fixture = await Fixture.Create();
        fixture.Listing.SetStatus(status); await fixture.Db.SaveChangesAsync();
        await Assert.ThrowsAsync<DomainException>(() => fixture.Handler().Handle(fixture.Command([LocalImage]) with { Title = "Must not persist" }, default));
        Assert.Equal("Original service", (await fixture.Db.Listings.AsNoTracking().SingleAsync()).Title);
        Assert.Equal(3, await fixture.Db.ListingMedias.CountAsync());
    }

    [Fact]
    public async Task AnotherVendorCannotEditOrReplaceTheOwnersAlbum()
    {
        await using var fixture = await Fixture.Create();
        await Assert.ThrowsAsync<NotFoundException>(() => fixture.Handler(fixture.OtherOwner.Id).Handle(fixture.Command([LocalImage]), default));
        Assert.Equal(3, await fixture.Db.ListingMedias.CountAsync());
        Assert.Equal(Description, (await fixture.Db.Listings.AsNoTracking().SingleAsync()).Description);
    }

    [Fact]
    public async Task RejectedOwnerCanEditAndExplicitNullMediaRemainsBackwardCompatible()
    {
        await using var fixture = await Fixture.Create();
        fixture.Listing.Reject("Bổ sung nội dung"); await fixture.Db.SaveChangesAsync();
        await fixture.Handler().Handle(fixture.Command(null) with { Description = "Mô tả đã được bổ sung theo yêu cầu kiểm duyệt." }, default);
        Assert.Equal(ListingStatus.Rejected, (await fixture.Db.Listings.AsNoTracking().SingleAsync()).Status);
        Assert.Equal(3, await fixture.Db.ListingMedias.CountAsync());
    }

    [Fact]
    public async Task FailureAfterDatabaseSaveRollsBackTextMediaAndAuditAsOneTransaction()
    {
        await using var fixture = await Fixture.Create();
        var oldMedia = await fixture.Db.ListingMedias.AsNoTracking().OrderBy(media => media.DisplayOrder).Select(media => media.Id).ToListAsync();
        var auditCount = await fixture.Db.AuditLogs.CountAsync();
        await using var failing = new FailingAfterSaveContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(fixture.Db.Database.GetDbConnection()).Options);
        var handler = new UpdateListingCommandHandler(failing, new Current(fixture.Owner.Id));
        await Assert.ThrowsAsync<InvalidOperationException>(() => handler.Handle(fixture.Command(["https://cdn.example.com/new.jpg"]) with { Title = "Must be rolled back" }, default));
        Assert.Equal("Original service", (await fixture.Db.Listings.AsNoTracking().SingleAsync()).Title);
        Assert.Equal(oldMedia, await fixture.Db.ListingMedias.AsNoTracking().OrderBy(media => media.DisplayOrder).Select(media => media.Id).ToListAsync());
        Assert.Equal(auditCount, await fixture.Db.AuditLogs.CountAsync());
    }

    [Theory]
    [InlineData("https://images.example.com/photo.jpg?w=1200")]
    [InlineData("http://legacy.example.com/photo.jpg")]
    [InlineData(LocalImage)]
    public void ExistingHttpAndGeneratedUploadsAreValidForCreateAndUpdate(string url)
    {
        Assert.True(ListingImageReferences.IsValid(url));
        Assert.True(new CreateListingCommandValidator().Validate(new CreateListingCommand(Guid.NewGuid(), "Photo", 1000, 2000, Description, "HCM", [url])).IsValid);
        Assert.True(new UpdateListingCommandValidator().Validate(new UpdateListingCommand(Guid.NewGuid(), "Photo", 1000, 2000, Description, "HCM", [url])).IsValid);
    }

    private sealed record Current(Guid? UserId) : ICurrentUserService { public string? Email => null; public string? Role => "VendorOwner"; }
    private sealed class FailingAfterSaveContext(DbContextOptions<ApplicationDbContext> options) : ApplicationDbContext(options)
    {
        public override async Task<int> SaveChangesAsync(CancellationToken ct = default)
        {
            await base.SaveChangesAsync(ct);
            throw new InvalidOperationException("Simulated failure before the listing transaction commits.");
        }
    }
    private sealed class Fixture(SqliteConnection connection, ApplicationDbContext db) : IAsyncDisposable
    {
        public ApplicationDbContext Db { get; } = db;
        public User Owner { get; private set; } = null!;
        public User OtherOwner { get; private set; } = null!;
        public Listing Listing { get; private set; } = null!;
        public UpdateListingCommandHandler Handler(Guid? userId = null) => new(Db, new Current(userId ?? Owner.Id));
        public UpdateListingCommand Command(List<string>? images = null) => new(Listing.Id, "Original service", 1000, 2000, Description, "HCM", images);
        public static async Task<Fixture> Create()
        {
            var connection = new SqliteConnection("Data Source=:memory:"); await connection.OpenAsync();
            var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options);
            await db.Database.EnsureCreatedAsync(); var fixture = new Fixture(connection, db);
            fixture.Owner = User.Create("Owner", "owner@test.vn", "0900000001", "hash", UserRole.VendorOwner);
            fixture.OtherOwner = User.Create("Other owner", "other@test.vn", "0900000002", "hash", UserRole.VendorOwner);
            db.Users.AddRange(fixture.Owner, fixture.OtherOwner);
            var vendor = Vendor.Create(fixture.Owner.Id, "Vendor", "vendor", "Owner", "0900000001", "HCM", .08m);
            var otherVendor = Vendor.Create(fixture.OtherOwner.Id, "Other vendor", "other-vendor", "Other", "0900000002", "HCM", .08m);
            db.Vendors.AddRange(vendor, otherVendor);
            var category = Category.Create("Photography", "listing-edit-photo", null, null, .08m); db.Categories.Add(category);
            fixture.Listing = Listing.Create(vendor.Id, category.Id, "Original service", "original-service", 1000, 2000, Description, "HCM");
            db.Listings.Add(fixture.Listing);
            var urls = new[] { LocalImage, "https://cdn.example.com/second.jpg", "http://legacy.example.com/third.jpg" };
            for (var index = 0; index < urls.Length; index++) db.ListingMedias.Add(ListingMedia.Create(fixture.Listing.Id, urls[index], null, displayOrder: index, isFeatured: index == 0));
            await db.SaveChangesAsync(); return fixture;
        }
        public async ValueTask DisposeAsync() { await Db.DisposeAsync(); await connection.DisposeAsync(); }
    }
}
