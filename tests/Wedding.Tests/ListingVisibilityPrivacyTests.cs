using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Wedding.Application.Common.Interfaces;
using Wedding.Application.Features.Listings;
using Wedding.Application.Features.Listings.Queries.GetListingDetail;
using Wedding.Application.Features.Listings.Queries.GetPublicListings;
using Wedding.Application.Features.Reviews.Queries.GetListingReviews;
using Wedding.Domain.Entities;
using Wedding.Domain.Enums;
using Wedding.Domain.Exceptions;
using Wedding.Infrastructure.Persistence;
using Wedding.WebApi.Controllers;

namespace Wedding.Tests;

public sealed class ListingVisibilityPrivacyTests
{
    [Theory]
    [InlineData(ListingStatus.Draft)]
    [InlineData(ListingStatus.PendingApproval)]
    [InlineData(ListingStatus.Rejected)]
    [InlineData(ListingStatus.Hidden)]
    public async Task AnonymousCannotReadAnyUnpublishedListing(ListingStatus status)
    {
        await using var fixture = await Fixture.Create();
        fixture.Listing.SetStatus(status); await fixture.Db.SaveChangesAsync();
        var handler = new GetListingDetailQueryHandler(fixture.Db, new Current(null, null));
        var hidden = await Assert.ThrowsAsync<NotFoundException>(() => handler.Handle(new(fixture.Listing.Id), default));
        var missing = await Assert.ThrowsAsync<NotFoundException>(() => handler.Handle(new(Guid.NewGuid()), default));
        Assert.Equal(missing.Message, hidden.Message);
    }

    [Theory]
    [InlineData("VendorOwner", true, true)]
    [InlineData("VendorOwner", false, false)]
    [InlineData("Customer", false, false)]
    [InlineData("Customer", true, false)]
    [InlineData("Finance", false, false)]
    [InlineData("CustomerCare", false, false)]
    [InlineData("Moderator", false, true)]
    [InlineData("SuperAdmin", false, true)]
    public async Task OnlyOwningVendorOrListingModeratorCanPreviewPrivateListing(string role, bool owningUser, bool allowed)
    {
        await using var fixture = await Fixture.Create();
        fixture.Listing.Reject("Ghi chú kiểm duyệt riêng"); await fixture.Db.SaveChangesAsync();
        var current = new Current(owningUser ? fixture.Owner.Id : fixture.OtherOwner.Id, role);
        var handler = new GetListingDetailQueryHandler(fixture.Db, current);
        if (!allowed)
            await Assert.ThrowsAsync<NotFoundException>(() => handler.Handle(new(fixture.Listing.Id), default));
        else
        {
            var result = await handler.Handle(new(fixture.Listing.Id), default);
            Assert.Equal("Rejected", result.Status);
            Assert.Equal("Ghi chú kiểm duyệt riêng", result.RejectionReason);
        }
    }

    [Fact]
    public async Task RoleWithoutAuthenticatedUserIdDoesNotGrantPreview()
    {
        await using var fixture = await Fixture.Create();
        fixture.Listing.SetStatus(ListingStatus.Draft); await fixture.Db.SaveChangesAsync();
        await Assert.ThrowsAsync<NotFoundException>(() => new GetListingDetailQueryHandler(fixture.Db,
            new Current(null, "SuperAdmin")).Handle(new(fixture.Listing.Id), default));
    }

    [Fact]
    public async Task PublicDetailsAndBrowseNeverDiscloseModerationNotes()
    {
        await using var fixture = await Fixture.Create();
        fixture.Listing.Reject("Private moderation evidence");
        fixture.Listing.SetStatus(ListingStatus.Active); await fixture.Db.SaveChangesAsync();
        foreach (var current in new[] { new Current(null, null), new Current(fixture.Customer.Id, "Customer"), new Current(fixture.OtherOwner.Id, "VendorOwner") })
        {
            var detail = await new GetListingDetailQueryHandler(fixture.Db, current).Handle(new(fixture.Listing.Id), default);
            Assert.Null(detail.RejectionReason);
            Assert.DoesNotContain("Private moderation evidence", JsonSerializer.Serialize(detail));
        }
        var browse = await Browse(fixture.Db);
        Assert.Null(Assert.Single(browse.Items).RejectionReason);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task DisabledVendorOrOwnerIsHiddenFromDetailsBrowseAndReviews(bool disableVendor)
    {
        await using var fixture = await Fixture.Create();
        if (disableVendor) fixture.Db.Entry(fixture.Vendor).Property(vendor => vendor.IsActive).CurrentValue = false;
        else fixture.Owner.SetStatus(false);
        await fixture.Db.SaveChangesAsync();
        await Assert.ThrowsAsync<NotFoundException>(() => new GetListingDetailQueryHandler(fixture.Db, new Current(null, null))
            .Handle(new(fixture.Listing.Id), default));
        Assert.Empty((await Browse(fixture.Db)).Items);
        var reviews = new GetListingReviewsQueryHandler(fixture.Db);
        await Assert.ThrowsAsync<NotFoundException>(() => reviews.Handle(new(ListingId: fixture.Listing.Id), default));
        await Assert.ThrowsAsync<NotFoundException>(() => reviews.Handle(new(VendorId: fixture.Vendor.Id), default));
    }

    [Fact]
    public async Task PublicReviewsKeepApprovedContentWithoutAccountContractOrModerationIdentifiers()
    {
        await using var fixture = await Fixture.Create();
        var lead = Lead.Create(fixture.Customer.Id, fixture.Vendor.Id, fixture.Listing.Id, "0900000000", DateTime.UtcNow.AddDays(30), 100, 1000000, null);
        var contract = BookingContract.Create(lead.Id, null, fixture.Vendor.Id, fixture.Customer.Id, 1000000, 100000, null, lead.WeddingDate);
        fixture.Db.Leads.Add(lead); fixture.Db.BookingContracts.Add(contract);
        var approved = fixture.Review(fixture.Listing, fixture.Customer, contract.Id); approved.Approve();
        fixture.Db.Reviews.Add(approved); await fixture.Db.SaveChangesAsync();

        var publicReviews = await new GetListingReviewsQueryHandler(fixture.Db).Handle(new(ListingId: fixture.Listing.Id), default);
        var item = Assert.Single(publicReviews);
        Assert.True(item.IsVerifiedBuyer); Assert.Equal(fixture.Customer.FullName, item.CustomerName);
        Assert.Equal("Approved", item.Status); Assert.Equal(approved.Content, item.Content);
        var json = JsonSerializer.Serialize(item, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        using var document = JsonDocument.Parse(json);
        foreach (var property in new[] { "bookingContractId", "contractCode", "customerId", "moderationFlag", "moderationDeadline", "rejectionReason" })
            Assert.False(document.RootElement.TryGetProperty(property, out _), property);
        Assert.DoesNotContain(contract.Id.ToString(), json);
        Assert.DoesNotContain(contract.ContractCode, json);
        Assert.DoesNotContain(fixture.Customer.Id.ToString(), json);
    }

    [Fact]
    public async Task PublicVendorReviewsExcludePendingRejectedAndUnpublishedListingContent()
    {
        await using var fixture = await Fixture.Create();
        var visible = fixture.Review(fixture.Listing, fixture.Customer); visible.Approve(); fixture.Db.Reviews.Add(visible);
        foreach (var status in new[] { ReviewStatus.Pending, ReviewStatus.Rejected })
        {
            var customer = fixture.AddCustomer();
            var review = fixture.Review(fixture.Listing, customer);
            if (status == ReviewStatus.Rejected) review.Reject("Private rejection reason");
            fixture.Db.Reviews.Add(review);
        }
        foreach (var status in new[] { ListingStatus.Draft, ListingStatus.PendingApproval, ListingStatus.Hidden, ListingStatus.Rejected })
        {
            var listing = fixture.AddListing(status);
            var review = fixture.Review(listing, fixture.Customer); review.Approve(); fixture.Db.Reviews.Add(review);
        }
        await fixture.Db.SaveChangesAsync();
        var result = await new GetListingReviewsQueryHandler(fixture.Db).Handle(new(VendorId: fixture.Vendor.Id), default);
        Assert.Equal(visible.Id, Assert.Single(result).Id);
    }

    [Theory]
    [InlineData(ListingStatus.Draft)]
    [InlineData(ListingStatus.PendingApproval)]
    [InlineData(ListingStatus.Hidden)]
    [InlineData(ListingStatus.Rejected)]
    public async Task PublicListingReviewsDoNotDiscloseUnpublishedListingExistence(ListingStatus status)
    {
        await using var fixture = await Fixture.Create();
        fixture.Listing.SetStatus(status); await fixture.Db.SaveChangesAsync();
        var handler = new GetListingReviewsQueryHandler(fixture.Db);
        var hidden = await Assert.ThrowsAsync<NotFoundException>(() => handler.Handle(new(ListingId: fixture.Listing.Id), default));
        var missing = await Assert.ThrowsAsync<NotFoundException>(() => handler.Handle(new(ListingId: Guid.NewGuid()), default));
        Assert.Equal(missing.Message, hidden.Message);
    }

    [Theory]
    [InlineData("Customer")]
    [InlineData("Finance")]
    [InlineData("CustomerCare")]
    [InlineData("Moderator")]
    [InlineData("SuperAdmin")]
    public async Task ListingUploadsRequireVendorRoleEvenForAnExistingVendorOwnerId(string role)
    {
        await using var fixture = await Fixture.Create();
        await Assert.ThrowsAsync<DomainException>(() => ListingAccess.RequireImageUploadAccessAsync(fixture.Db, new Current(fixture.Owner.Id, role), default));
    }

    [Fact]
    public async Task ListingUploadsRequireOwnActiveVendorAndOwnerAccount()
    {
        await using var fixture = await Fixture.Create();
        await ListingAccess.RequireImageUploadAccessAsync(fixture.Db, new Current(fixture.Owner.Id, "VendorOwner"), default);
        await Assert.ThrowsAsync<DomainException>(() => ListingAccess.RequireImageUploadAccessAsync(fixture.Db, new Current(null, "VendorOwner"), default));
        await Assert.ThrowsAsync<DomainException>(() => ListingAccess.RequireImageUploadAccessAsync(fixture.Db, new Current(fixture.Customer.Id, "VendorOwner"), default));
        fixture.Db.Entry(fixture.Vendor).Property(vendor => vendor.IsActive).CurrentValue = false; await fixture.Db.SaveChangesAsync();
        await Assert.ThrowsAsync<DomainException>(() => ListingAccess.RequireImageUploadAccessAsync(fixture.Db, new Current(fixture.Owner.Id, "VendorOwner"), default));
        fixture.Db.Entry(fixture.Vendor).Property(vendor => vendor.IsActive).CurrentValue = true; fixture.Owner.SetStatus(false); await fixture.Db.SaveChangesAsync();
        await Assert.ThrowsAsync<DomainException>(() => ListingAccess.RequireImageUploadAccessAsync(fixture.Db, new Current(fixture.Owner.Id, "VendorOwner"), default));
    }

    [Fact]
    public async Task UploadControllerRejectsCustomerBeforeOpeningFilesOrCreatingStorage()
    {
        await using var fixture = await Fixture.Create();
        var environment = new TestEnvironment();
        var file = new TrackingFile();
        var controller = new UploadController(environment, new ConfigurationBuilder().Build(), fixture.Db, new Current(fixture.Customer.Id, "Customer"));
        await Assert.ThrowsAsync<DomainException>(() => controller.UploadImages([file], default));
        Assert.False(file.WasOpened);
        Assert.False(Directory.Exists(environment.ContentRootPath));
        var authorization = typeof(UploadController).GetMethod(nameof(UploadController.UploadImages))!
            .GetCustomAttributes(typeof(AuthorizeAttribute), false).Cast<AuthorizeAttribute>();
        Assert.Contains(authorization, attribute => attribute.Roles == "VendorOwner");
    }

    private static Task<PublicListingsResult> Browse(ApplicationDbContext db) => new GetPublicListingsQueryHandler(db)
        .Handle(new(null, null, null, null, null), default);

    private sealed record Current(Guid? UserId, string? Role) : ICurrentUserService { public string? Email => null; }

    private sealed class TestEnvironment : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "WeddingPrivacyTests";
        public string EnvironmentName { get; set; } = "Testing";
        public string ContentRootPath { get; set; } = Path.Combine(Path.GetTempPath(), "WeddingPrivacyNoWrite_" + Guid.NewGuid().ToString("N"));
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
        public string WebRootPath { get; set; } = "";
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
    }

    private sealed class TrackingFile : IFormFile
    {
        public bool WasOpened { get; private set; }
        public string ContentType => "image/png";
        public string ContentDisposition => "";
        public IHeaderDictionary Headers => new HeaderDictionary();
        public long Length => 8;
        public string Name => "files";
        public string FileName => "photo.png";
        public Stream OpenReadStream() { WasOpened = true; return new MemoryStream(new byte[8]); }
        public void CopyTo(Stream target) => throw new NotSupportedException();
        public Task CopyToAsync(Stream target, CancellationToken ct = default) => throw new NotSupportedException();
    }

    private sealed class Fixture(SqliteConnection connection, ApplicationDbContext db) : IAsyncDisposable
    {
        public ApplicationDbContext Db { get; } = db;
        public User Owner { get; private set; } = null!;
        public User OtherOwner { get; private set; } = null!;
        public User Customer { get; private set; } = null!;
        public Vendor Vendor { get; private set; } = null!;
        public Category Category { get; private set; } = null!;
        public Listing Listing { get; private set; } = null!;
        public static async Task<Fixture> Create()
        {
            var connection = new SqliteConnection("Data Source=:memory:"); await connection.OpenAsync();
            var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options);
            await db.Database.EnsureCreatedAsync();
            var fixture = new Fixture(connection, db);
            fixture.Owner = User.Create("Vendor owner", "owner@test.vn", "0900000001", "hash", UserRole.VendorOwner);
            fixture.OtherOwner = User.Create("Other vendor owner", "other@test.vn", "0900000002", "hash", UserRole.VendorOwner);
            fixture.Customer = User.Create("Public customer name", "customer@test.vn", "0900000000", "hash", UserRole.Customer);
            db.Users.AddRange(fixture.Owner, fixture.OtherOwner, fixture.Customer);
            fixture.Vendor = Vendor.Create(fixture.Owner.Id, "Vendor", "vendor", "Owner", "0900000001", "HCM", .08m);
            db.Vendors.Add(fixture.Vendor);
            db.Vendors.Add(Vendor.Create(fixture.OtherOwner.Id, "Other vendor", "other-vendor", "Other", "0900000002", "HCM", .08m));
            fixture.Category = Category.Create("Photography", "privacy-photo", null, null, .08m); db.Categories.Add(fixture.Category);
            fixture.Listing = fixture.AddListing(ListingStatus.Active);
            await db.SaveChangesAsync(); return fixture;
        }
        public User AddCustomer()
        {
            var customer = User.Create("Another public name", $"{Guid.NewGuid():N}@test.vn", "0900000003", "hash", UserRole.Customer);
            Db.Users.Add(customer); return customer;
        }
        public Listing AddListing(ListingStatus status)
        {
            var listing = Listing.Create(Vendor.Id, Category.Id, "Photo", Guid.NewGuid().ToString("N"), 1000, 10000, "Photo description", "HCM");
            listing.SetStatus(status); Db.Listings.Add(listing); return listing;
        }
        public Review Review(Listing listing, User customer, Guid? contractId = null) => Wedding.Domain.Entities.Review.Create(contractId,
            listing.Id, customer.Id, Vendor.Id, 5, "Dịch vụ chu đáo và đúng giờ, chúng tôi rất hài lòng với ekip.", "[\"https://images.example.com/review.jpg\"]", contractId.HasValue);
        public async ValueTask DisposeAsync() { await Db.DisposeAsync(); await connection.DisposeAsync(); }
    }
}
