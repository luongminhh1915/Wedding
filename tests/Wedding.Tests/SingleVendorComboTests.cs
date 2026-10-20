using Microsoft.Data.Sqlite;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Wedding.Application.Common.Interfaces;
using Wedding.Application.Features.Combos;
using Wedding.Domain.Entities;
using Wedding.Domain.Enums;
using Wedding.Domain.Exceptions;
using Wedding.Infrastructure.Persistence;

namespace Wedding.Tests;

public class SingleVendorComboTests
{
    [Fact]
    public async Task PublicCatalogExposesStoredShopDetailsAndActualImageGalleryButNoPrivateVendorData()
    {
        await using var f = await Fixture.Create();
        f.Vendor.UpdateProfile("Public shop", "PRIVATE CONTACT PERSON", "0909876543", "123 Public Street", "HCM", "Public shop biography",
            "https://public.example.invalid/logo.png", "https://public.example.invalid/cover.jpg");
        f.Vendor.UpdateRating(4.9m, 3);
        var bank = VendorTaxProfile.Create(f.Vendor.Id); bank.Update("PRIVATE LEGAL NAME", "0123456789", "TEST", "112233445566", "PRIVATE BANK HOLDER", true); bank.Submit(); bank.Review(true); f.Db.VendorTaxProfiles.Add(bank);
        f.Db.VendorVerifications.Add(VendorVerification.Create(f.Vendor.Id, "PRIVATE LICENSE", "[\"https://private.example.invalid/id-document.png\"]", "PRIVATE MODERATION NOTE"));
        f.Db.ListingMedias.AddRange(
            ListingMedia.Create(f.Venue.ListingId, "https://public.example.invalid/gallery.jpg", "https://public.example.invalid/gallery-thumb.jpg", displayOrder: 0),
            ListingMedia.Create(f.Venue.ListingId, "https://public.example.invalid/featured.jpg", "https://public.example.invalid/featured-thumb.jpg", displayOrder: 9, isFeatured: true),
            ListingMedia.Create(f.Venue.ListingId, "https://public.example.invalid/gallery.jpg", null, displayOrder: 10),
            ListingMedia.Create(f.Venue.ListingId, "https://private.example.invalid/internal-document.pdf", null, mediaType: "document"),
            ListingMedia.Create(f.Venue.ListingId, "https://public.example.invalid/video.mp4", null, mediaType: "video"));
        await f.Db.SaveChangesAsync();
        var builder = await new GetComboBuilderQueryHandler(f.Db).Handle(new(null), default);
        var vendor = Assert.Single(builder.Vendors!, v => v.Id == f.Vendor.Id);
        Assert.Equal("Public shop", vendor.Name); Assert.Equal(f.Vendor.Slug, vendor.Slug); Assert.Equal("123 Public Street", vendor.Address); Assert.Equal("Public shop biography", vendor.Bio);
        Assert.Equal("https://public.example.invalid/logo.png", vendor.LogoUrl); Assert.Equal("https://public.example.invalid/cover.jpg", vendor.CoverImageUrl);
        Assert.Equal(4.9m, vendor.RatingAvg); Assert.Equal(3, vendor.TotalReviews); Assert.True(vendor.IsVerified);
        var option = builder.Categories.SelectMany(c => c.Options).Single(o => o.ServiceItemId == f.Venue.Id);
        Assert.Equal(new[] { "https://public.example.invalid/featured.jpg", "https://public.example.invalid/gallery.jpg" }, option.GalleryUrls);
        Assert.Equal("https://public.example.invalid/featured-thumb.jpg", option.ImageUrl); Assert.Equal("QA unit test", option.Description);
        var json = JsonSerializer.Serialize(builder, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        foreach (var secret in new[] { "PRIVATE CONTACT PERSON", "0909876543", "PRIVATE LEGAL NAME", "112233445566", "PRIVATE BANK HOLDER", "PRIVATE LICENSE", "PRIVATE MODERATION NOTE", "private.example.invalid", f.Owner.Email }) Assert.DoesNotContain(secret, json);
        using var document = JsonDocument.Parse(JsonSerializer.Serialize(vendor, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
        foreach (var key in new[] { "userId", "email", "contactPerson", "hotline", "bankAccountNumber", "documents", "commissionRate" }) Assert.False(document.RootElement.TryGetProperty(key, out _));
    }
    [Fact]
    public async Task PublicShopMetadataHasNoFabricatedImagesOrRatingsAndFollowsCatalogEligibility()
    {
        await using var f = await Fixture.Create(); f.OtherVendor.SetVerified(false); await f.Db.SaveChangesAsync();
        var builder = await new GetComboBuilderQueryHandler(f.Db).Handle(new(null), default);
        var vendor = Assert.Single(builder.Vendors!); Assert.Equal(f.Vendor.Id, vendor.Id); Assert.Null(vendor.LogoUrl); Assert.Null(vendor.CoverImageUrl); Assert.Null(vendor.RatingAvg); Assert.Equal(0, vendor.TotalReviews);
        Assert.All(builder.Categories.SelectMany(c => c.Options), option => { Assert.Null(option.ImageUrl); Assert.Empty(option.GalleryUrls!); Assert.Equal(f.Vendor.Id, option.VendorId); });
        f.Vendor.ConfigureComboParticipation(false, .08m); await f.Db.SaveChangesAsync();
        var hidden = await new GetComboBuilderQueryHandler(f.Db).Handle(new(null), default); Assert.Empty(hidden.Vendors!); Assert.All(hidden.Categories, category => Assert.Empty(category.Options));
    }
    [Fact]
    public async Task GalleryIsBoundedAndUsesOnlyExistingListingImages()
    {
        await using var f = await Fixture.Create();
        for (var i = 0; i < 15; i++) f.Db.ListingMedias.Add(ListingMedia.Create(f.Venue.ListingId, $"https://public.example.invalid/image-{i}.jpg", null, displayOrder: i));
        await f.Db.SaveChangesAsync();
        var builder = await new GetComboBuilderQueryHandler(f.Db).Handle(new(null), default);
        var gallery = builder.Categories.SelectMany(c => c.Options).Single(o => o.ServiceItemId == f.Venue.Id).GalleryUrls!;
        Assert.Equal(12, gallery.Count); Assert.Equal("https://public.example.invalid/image-0.jpg", gallery[0]); Assert.Equal("https://public.example.invalid/image-11.jpg", gallery[^1]);
    }
    [Fact]
    public async Task PriceDraftAndSubmitIndependentlyRejectMultipleVendorsWithoutCreatingSideEffects()
    {
        await using var f = await Fixture.Create();
        var selected = new List<ComboSelection> { new(f.Venue.Id, null, null), new(f.ForeignPhoto.Id, null, null) };
        await Assert.ThrowsAsync<DomainException>(() => ComboPricing.CalculateAsync(f.Db, new(null, "HCM", f.Date, 100, selected), f.Customer.Id, default));
        var command = new SubmitComboOrderCommand(null, new(), f.Date, 100, f.Customer.PhoneNumber, null, "HCM", selected);
        var handler = new SubmitComboOrderCommandHandler(f.Db, f.CustomerUser, new Notify());
        await Assert.ThrowsAsync<DomainException>(() => handler.Handle(command with { SaveAsDraft = true }, default));
        await Assert.ThrowsAsync<DomainException>(() => handler.Handle(command, default));
        Assert.Equal(0, await f.Db.ComboOrders.CountAsync()); Assert.Equal(0, await f.Db.Leads.CountAsync()); Assert.Equal(0, await f.Db.BudgetItems.CountAsync());
    }
    [Fact]
    public async Task ApprovedOwnedPresetAllowsSameVendorReplacementButNeverCrossVendorEvenAfterDetachingPreset()
    {
        await using var f = await Fixture.Create(); var template = await f.SaveAsVendor();
        await new ReviewComboTemplateCommandHandler(f.Db, new Audit(), f.AdminUser).Handle(new(template.Id, true), default);
        var request = new ComboPriceCommand(template.Id, "HCM", f.Date, 100, new() { new(f.Venue.Id, null, null), new(f.OwnPhotoAlternative.Id, null, null) });
        var price = await ComboPricing.CalculateAsync(f.Db, request, null, default);
        Assert.All(price.Lines, line => Assert.Equal(f.Vendor.Id, line.VendorId));
        var bad = request with { Selections = new() { new(f.Venue.Id, null, null), new(f.ForeignPhoto.Id, null, null) } };
        await Assert.ThrowsAsync<DomainException>(() => ComboPricing.CalculateAsync(f.Db, bad, null, default));
        await Assert.ThrowsAsync<DomainException>(() => ComboPricing.CalculateAsync(f.Db, bad with { TemplateId = null }, null, default));
    }
    [Fact]
    public async Task VendorCannotUseAnotherVendorItemsOrForeignAllowedOptionsOrModifyAnotherCombo()
    {
        await using var f = await Fixture.Create(); var handler = new SaveComboTemplateCommandHandler(f.Db, new Audit(), f.VendorUser);
        await Assert.ThrowsAsync<DomainException>(() => handler.Handle(f.TemplateCommand with { Items = new() { f.Slot(f.ForeignPhoto) } }, default));
        var foreignAllowed = f.Slot(f.OwnPhoto) with { AllowedItemIds = new() { f.ForeignPhoto.Id } };
        await Assert.ThrowsAsync<DomainException>(() => handler.Handle(f.TemplateCommand with { Items = new() { f.Slot(f.Venue), foreignAllowed } }, default));
        var foreignLegacy = ComboTemplate.Create("Other vendor legacy combo", "Popular", "Modern", null, null, 0);
        foreignLegacy.StageUnpublished(f.OtherVendor.Id, "Draft"); f.Db.ComboTemplates.Add(foreignLegacy); await f.Db.SaveChangesAsync();
        var other = await new SaveComboTemplateCommandHandler(f.Db, new Audit(), new Current(f.OtherOwner.Id, "VendorOwner"))
            .Handle(f.TemplateCommand with { Id = foreignLegacy.Id, Name = "Other vendor combo", Items = new() { f.Slot(f.ForeignPhoto) } }, default);
        await Assert.ThrowsAsync<NotFoundException>(() => handler.Handle(f.TemplateCommand with { Id = other.Id }, default));
        await Assert.ThrowsAsync<NotFoundException>(() => new ArchiveComboTemplateCommandHandler(f.Db, f.VendorUser, new Audit()).Handle(new(other.Id), default));
        Assert.True(await f.Db.ComboTemplates.AnyAsync(t => t.Id == other.Id));
    }
    [Fact]
    public async Task AdminResolvesExactlyOneOwnerAndRejectsMixedCreationAndForeignSwapOptions()
    {
        await using var f = await Fixture.Create(); var admin = f.AdminUser;
        var handler = new SaveComboTemplateCommandHandler(f.Db, new Audit(), admin);
        var saved = await handler.Handle(f.TemplateCommand, default);
        Assert.Equal(f.Vendor.Id, saved.OwnerVendorId); Assert.Equal("Pending", saved.ModerationStatus);
        await Assert.ThrowsAsync<DomainException>(() => handler.Handle(f.TemplateCommand with { Name = "Mixed", Items = new() { f.Slot(f.Venue), f.Slot(f.ForeignPhoto) } }, default));
        await Assert.ThrowsAsync<DomainException>(() => handler.Handle(f.TemplateCommand with { Items = new() { f.Slot(f.OwnPhoto) with { AllowedItemIds = new() { f.ForeignPhoto.Id } } } }, default));
        await Assert.ThrowsAsync<DomainException>(() => handler.Handle(f.TemplateCommand with { Id = saved.Id, Items = new() { f.Slot(f.ForeignPhoto) } }, default));
    }
    [Fact]
    public async Task BuilderAndModeratorRejectLegacyUnownedOrCrossVendorTemplatesWithoutDeletingThem()
    {
        await using var f = await Fixture.Create(); var owned = await f.SaveAsVendor();
        await new ReviewComboTemplateCommandHandler(f.Db, new Audit(), f.AdminUser).Handle(new(owned.Id, true), default);
        var legacy = ComboTemplate.Create("Legacy mixed preset", "Popular", "Modern", null, null, 1);
        foreach (var item in new[] { f.Venue, f.ForeignPhoto }) { var slot = ComboTemplateItem.Create(legacy.Id, item.CategoryId, item.ListingId, false); slot.ConfigureItems(item.Id, "[]"); legacy.Items.Add(slot); }
        legacy.SetScope(null, "HCM", null, false); f.Db.ComboTemplates.Add(legacy); await f.Db.SaveChangesAsync();
        await Assert.ThrowsAsync<DomainException>(() => new ReviewComboTemplateCommandHandler(f.Db, new Audit(), f.AdminUser).Handle(new(legacy.Id, true), default));
        legacy.SetScope(f.Vendor.Id, "HCM", null, true); await f.Db.SaveChangesAsync(); // Simulates an old incorrectly owned preset.
        var builder = await new GetComboBuilderQueryHandler(f.Db).Handle(new(null), default);
        Assert.Contains(builder.Templates, t => t.Id == owned.Id); Assert.DoesNotContain(builder.Templates, t => t.Id == legacy.Id);
        Assert.True(await f.Db.ComboTemplates.AnyAsync(t => t.Id == legacy.Id));
        await Assert.ThrowsAsync<DomainException>(() => ComboPricing.CalculateAsync(f.Db, new(legacy.Id, "HCM", f.Date, 100, new() { new(f.Venue.Id, null, null) }), null, default));
    }
    [Fact]
    public async Task LegacyMixedDraftIsReadableButCannotBeResubmittedEvenWithSingleVendorPayload()
    {
        await using var f = await Fixture.Create();
        var order = ComboOrder.Create(f.Customer.Id, null, f.Date, 100, "Legacy fixture",
            new List<(Guid, Guid, Guid, string, decimal)> { (f.Venue.ListingId, f.Vendor.Id, f.Venue.CategoryId, "Venue", 1000000), (f.OwnPhoto.ListingId, f.Vendor.Id, f.OwnPhoto.CategoryId, "Photo", 2000000) });
        order.SetPricing(0, "HCM", true); f.Db.ComboOrders.Add(order); await f.Db.SaveChangesAsync();
        var oldItem = order.Items.Single(i => i.CategoryId == f.OwnPhoto.CategoryId);
        // A database-only historical fixture represents a row written before the new invariant.
        // It is never created through a production API/factory and never touches a real database.
        await f.Db.Database.ExecuteSqlInterpolatedAsync($"UPDATE ComboOrderItems SET VendorId = {f.OtherVendor.Id}, ListingId = {f.ForeignPhoto.ListingId}, ServiceItemId = {f.ForeignPhoto.Id} WHERE Id = {oldItem.Id}");
        f.Db.ChangeTracker.Clear();
        var history = await new GetMyComboOrdersQueryHandler(f.Db, f.CustomerUser).Handle(new(), default);
        Assert.Equal(2, history.Single().Items.Select(i => i.VendorId).Distinct().Count());
        var legacyEntity = await f.Db.ComboOrders.Include(o => o.Items).SingleAsync();
        Assert.Throws<DomainException>(() => legacyEntity.SubmitDraft()); Assert.Throws<DomainException>(() => legacyEntity.SetPricing(0, "HCM", false));
        var command = new SubmitComboOrderCommand(null, new(), f.Date, 100, f.Customer.PhoneNumber, null, "HCM", new() { new(f.Venue.Id, null, null) }, SaveAsDraft: false, DraftId: order.Id);
        await Assert.ThrowsAsync<DomainException>(() => new SubmitComboOrderCommandHandler(f.Db, f.CustomerUser, new Notify()).Handle(command, default));
        Assert.Equal(1, await f.Db.ComboOrders.CountAsync()); Assert.Equal(2, await f.Db.ComboOrderItems.CountAsync()); Assert.Equal(0, await f.Db.Leads.CountAsync());
    }
    [Fact]
    public async Task VendorCanReviseLegacyEditArchiveAndResubmitWithoutDeletingExistingOrderHistory()
    {
        await using var f = await Fixture.Create(); var created = await f.SaveAsVendor();
        Assert.Equal("Pending", created.ModerationStatus); Assert.Equal(f.Vendor.Id, created.OwnerVendorId);
        await new ReviewComboTemplateCommandHandler(f.Db, new Audit(), f.AdminUser).Handle(new(created.Id, true), default);
        var submitted = await new SubmitComboOrderCommandHandler(f.Db, f.CustomerUser, new Notify()).Handle(new(created.Id, new(), f.Date, 100, f.Customer.PhoneNumber, null, "HCM", new() { new(f.Venue.Id, null, null), new(f.OwnPhoto.Id, null, null) }), default);
        Assert.Equal(0, await f.Db.BookingContracts.CountAsync()); Assert.Equal(0, await f.Db.Commissions.CountAsync()); Assert.Equal(0, await f.Db.PaymentTransactions.CountAsync()); // RFQ only: no automatic charge/contract.
        await new ArchiveComboTemplateCommandHandler(f.Db, f.VendorUser, new Audit()).Handle(new(created.Id), default);
        var builder = await new GetComboBuilderQueryHandler(f.Db).Handle(new(null), default); Assert.DoesNotContain(builder.Templates, t => t.Id == created.Id);
        Assert.True(await f.Db.ComboOrders.AnyAsync(o => o.Id == submitted.Id));
        var edited = await new SaveComboTemplateCommandHandler(f.Db, new Audit(), f.VendorUser).Handle(f.TemplateCommand with { Id = created.Id, Name = "Updated owned combo" }, default);
        Assert.Equal("Approved", edited.ModerationStatus); Assert.Equal("Pending", edited.LatestRevision?.Status);
        Assert.Equal(f.Vendor.Id, edited.OwnerVendorId);
        Assert.True(await f.Db.ComboOrders.AnyAsync(o => o.Id == submitted.Id));
    }
    private sealed record Current(Guid Id, string CurrentRole) : ICurrentUserService { public Guid? UserId => Id; public string? Email => null; public string? Role => CurrentRole; }
    private sealed class Audit : IAuditLogger { public Task LogAsync(string action, string entityType, string? entityId, object? data = null) => Task.CompletedTask; }
    private sealed class Notify : INotificationSender { public Task SendAsync(string channel, string recipient, string message, CancellationToken cancellationToken = default) => Task.CompletedTask; }
    private sealed class Fixture : IAsyncDisposable
    {
        public SqliteConnection Connection = null!; public ApplicationDbContext Db = null!; public User Customer = null!; public User Owner = null!; public User OtherOwner = null!; public User Admin = null!;
        public Vendor Vendor = null!; public Vendor OtherVendor = null!; public ServiceItem Venue = null!; public ServiceItem OwnPhoto = null!; public ServiceItem OwnPhotoAlternative = null!; public ServiceItem ForeignPhoto = null!;
        public DateTime Date => DateTime.UtcNow.AddDays(20);
        public Current CustomerUser => new(Customer.Id, "Customer"); public Current VendorUser => new(Owner.Id, "VendorOwner");
        public Current AdminUser => new(Admin.Id, "SuperAdmin");
        public ComboTemplateItemDto Slot(ServiceItem item) => new(item.CategoryId, item.ListingId, false, item.Id, new());
        public SaveComboTemplateCommand TemplateCommand => new(null, "Owned combo", "Popular", "Modern", null, null, true, 0, new() { Slot(Venue), Slot(OwnPhoto) }, "HCM", .03m);
        public async Task<ComboTemplateDto> SaveAsVendor()
        {
            var legacy = ComboTemplate.Create("Legacy owned combo", "Popular", "Modern", null, null, 0);
            legacy.StageUnpublished(Vendor.Id, "Draft"); Db.ComboTemplates.Add(legacy); await Db.SaveChangesAsync();
            return await new SaveComboTemplateCommandHandler(Db, new Audit(), VendorUser).Handle(TemplateCommand with { Id = legacy.Id }, default);
        }
        public static async Task<Fixture> Create()
        {
            var f = new Fixture(); f.Connection = new SqliteConnection("Data Source=:memory:"); await f.Connection.OpenAsync(); f.Db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(f.Connection).Options); await f.Db.Database.EnsureCreatedAsync();
            f.Customer = User.Create("Customer", "c@single.test", "0900000000", "hash", UserRole.Customer); f.Owner = User.Create("Owner", "v@single.test", "0900000001", "hash", UserRole.VendorOwner); f.OtherOwner = User.Create("Other", "o@single.test", "0900000002", "hash", UserRole.VendorOwner);
            f.Admin = User.Create("Synthetic admin", "admin@single.test", "0900000003", "hash", UserRole.SuperAdmin);
            f.Db.Users.AddRange(f.Customer, f.Owner, f.OtherOwner, f.Admin);
            f.Vendor = Vendor.Create(f.Owner.Id, "One Vendor", "one-vendor", "Owner", "0900000001", "HCM", .08m); f.OtherVendor = Vendor.Create(f.OtherOwner.Id, "Other Vendor", "other-vendor", "Other", "0900000002", "HCM", .08m);
            foreach (var vendor in new[] { f.Vendor, f.OtherVendor }) { vendor.SetVerified(true); vendor.ConfigureComboParticipation(true, .08m); f.Db.Vendors.Add(vendor); }
            var venueCategory = await f.Db.Categories.SingleAsync(c => c.Slug == "tiem-cuoi-venues"); var photoCategory = await f.Db.Categories.SingleAsync(c => c.Slug == "quay-chup-photo-video");
            ServiceItem Add(Vendor vendor, Category category, string name, decimal price)
            {
                var listing = Listing.Create(vendor.Id, category.Id, name, name.ToLowerInvariant().Replace(' ', '-'), price, price, "QA unit test", "HCM"); listing.Approve(); f.Db.Listings.Add(listing);
                var item = ServiceItem.Create(vendor.Id, listing.Id, category.Id, name, price, "Package", 1, 1000, "HCM", "Policy"); item.Submit(); item.Review(true, null); f.Db.ServiceItems.Add(item); return item;
            }
            f.Venue = Add(f.Vendor, venueCategory, "Venue", 1000000); f.OwnPhoto = Add(f.Vendor, photoCategory, "Photo", 2000000); f.OwnPhotoAlternative = Add(f.Vendor, photoCategory, "Own Photo Alternative", 2500000); f.ForeignPhoto = Add(f.OtherVendor, photoCategory, "Foreign Photo", 3000000);
            await f.Db.SaveChangesAsync(); return f;
        }
        public async ValueTask DisposeAsync() { await Db.DisposeAsync(); await Connection.DisposeAsync(); }
    }
}
