using Microsoft.EntityFrameworkCore;
using Wedding.Application.Common.Interfaces;
using Wedding.Application.Features.Listings;
using Wedding.Application.Features.Listings.Commands.ApproveListing;
using Wedding.Application.Features.Listings.Commands.SubmitForApproval;
using Wedding.Application.Features.Listings.Commands.UpdateListing;
using Wedding.Domain.Entities;
using Wedding.Domain.Enums;
using Wedding.Infrastructure.Persistence;

namespace Wedding.Tests;

public class StaffListingEditorSqlTests
{
    [SqlServerFact]
    public async Task StaffSqlEditAndSubmitKeepPublicContentUntilCanonicalApproval()
    {
        var name = "WeddingPlanTest_StaffListingEdit_" + Guid.NewGuid().ToString("N");
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlServer($"Server=localhost;Database={name};Integrated Security=true;TrustServerCertificate=true;Connection Timeout=5").Options;
        await using var db = new ApplicationDbContext(options);
        var owned = false;
        try
        {
            Assert.False(await db.Database.CanConnectAsync()); owned = true;
            await db.Database.MigrateAsync();
            var owner = User.Create("Synthetic SQL Vendor", "vendor@staff-editor.example.invalid", "0901234567", "synthetic-hash", UserRole.VendorOwner);
            var admin = User.Create("Synthetic SQL Admin", "admin@staff-editor.example.invalid", "0901234568", "synthetic-hash", UserRole.SuperAdmin);
            var moderator = User.Create("Synthetic SQL Moderator", "mod@staff-editor.example.invalid", "0901234569", "synthetic-hash", UserRole.Moderator);
            db.Users.AddRange(owner, admin, moderator);
            var vendor = Vendor.Create(owner.Id, "Synthetic SQL Shop", "staff-editor-sql-shop", "Owner", "0901234567", "Hà Nội", .08m); db.Vendors.Add(vendor);
            var category = await db.Categories.SingleAsync(row => row.Slug == "quay-chup-photo-video");
            var listing = Listing.Create(vendor.Id, category.Id, "Original SQL published service", "staff-editor-sql-listing", 1000, 2000, "Original SQL public description", "Hà Nội");
            listing.Approve(); db.Listings.Add(listing);
            db.ListingMedias.Add(ListingMedia.Create(listing.Id, "https://images.example.invalid/original.jpg", null, isFeatured: true));
            await db.SaveChangesAsync(); db.ChangeTracker.Clear();
            var actor = new Current(admin.Id, "SuperAdmin");
            await new UpdateListingCommandHandler(db, actor).Handle(new(listing.Id, "Staff SQL proposal", 1200, 2400, "Staff SQL draft description", "Hà Nội", ["https://images.example.invalid/new.jpg"]), default);
            Assert.Equal("Original SQL published service", (await db.Listings.AsNoTracking().SingleAsync(row => row.Id == listing.Id)).Title);
            Assert.Equal("https://images.example.invalid/original.jpg", (await db.ListingMedias.AsNoTracking().SingleAsync(row => row.ListingId == listing.Id)).MediaUrl);
            var draft = await db.ListingRevisions.AsNoTracking().SingleAsync(row => row.ListingId == listing.Id && row.Status == "Draft");
            Assert.Equal(admin.Id, draft.CreatedById);
            Assert.True(await db.AuditLogs.AnyAsync(row => row.Action == "ListingRevision.SaveDraft" && row.ActorId == admin.Id && row.ActorRole == "SuperAdmin"));
            await new SubmitForApprovalCommandHandler(db, actor).Handle(new(listing.Id), default);
            var pending = await db.ListingRevisions.AsNoTracking().SingleAsync(row => row.ListingId == listing.Id && row.Status == "Pending");
            Assert.Equal("Original SQL published service", (await db.Listings.AsNoTracking().SingleAsync(row => row.Id == listing.Id)).Title);
            await new ApproveListingCommandHandler(db, new Current(moderator.Id, "Moderator")).Handle(new(listing.Id, pending.Id), default);
            Assert.Equal("Staff SQL proposal", (await db.Listings.AsNoTracking().SingleAsync(row => row.Id == listing.Id)).Title);
            Assert.Equal("https://images.example.invalid/new.jpg", (await db.ListingMedias.AsNoTracking().SingleAsync(row => row.ListingId == listing.Id)).MediaUrl);
            Assert.False((await db.Vendors.AsNoTracking().SingleAsync(row => row.Id == vendor.Id)).IsVerified);
            Assert.Equal(0, await db.BookingContracts.CountAsync());
        }
        finally
        {
            if (owned)
            {
                Assert.StartsWith("WeddingPlanTest_StaffListingEdit_", name);
                await using var cleanup = new ApplicationDbContext(options);
                await cleanup.Database.EnsureDeletedAsync(); Assert.False(await cleanup.Database.CanConnectAsync());
            }
        }
    }
    private sealed record Current(Guid? UserId, string? Role) : ICurrentUserService { public string? Email => null; }
}
