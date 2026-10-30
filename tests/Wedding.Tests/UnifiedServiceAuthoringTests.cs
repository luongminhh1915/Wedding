using System.Text.Json;
using Microsoft.Data.SqlClient;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Wedding.Application.Common.Interfaces;
using Wedding.Application.Features.Combos;
using Wedding.Application.Features.Listings;
using Wedding.Application.Features.Listings.Commands.ApproveListing;
using Wedding.Application.Features.Listings.Commands.CreateListing;
using Wedding.Application.Features.Listings.Commands.RejectListing;
using Wedding.Application.Features.Listings.Commands.SubmitForApproval;
using Wedding.Application.Features.Listings.Commands.UpdateListing;
using Wedding.Application.Features.Listings.DTOs;
using Wedding.Application.Features.Listings.Queries.GetListingDetail;
using Wedding.Application.Features.Listings.Queries.GetPublicListings;
using Wedding.Domain.Entities;
using Wedding.Domain.Enums;
using Wedding.Domain.Exceptions;
using Wedding.Infrastructure.Persistence;
using Wedding.Infrastructure.Services;

namespace Wedding.Tests;

public class UnifiedServiceAuthoringTests
{
    [Fact]
    public async Task OneCreateAndOneListingApprovalPublishTheSameSourceAndPricingFacet()
    {
        await using var f = await Fixture.Create();
        var id = await f.CreateService();
        var source = await f.Db.Listings.AsNoTracking().SingleAsync();
        var facet = await f.Db.ServiceItems.AsNoTracking().SingleAsync();
        Assert.Equal(id, facet.ListingId); Assert.Equal(source.PricingServiceItemId, facet.Id);
        Assert.Equal(ListingStatus.Draft, source.Status); Assert.Equal("Draft", facet.ModerationStatus);
        await f.Submit(id);
        Assert.Equal("Pending", (await f.Db.ServiceItems.AsNoTracking().SingleAsync()).ModerationStatus);
        Assert.Empty(await new GetServiceItemsQueryHandler(f.Db, f.Staff).Handle(new(true), default));
        Assert.Empty(await ComboPublicCatalog.Eligible(f.Db).ToListAsync());
        await f.Approve(id);
        source = await f.Db.Listings.AsNoTracking().SingleAsync(); facet = await f.Db.ServiceItems.AsNoTracking().SingleAsync();
        Assert.Equal(ListingStatus.Active, source.Status); Assert.Equal("Approved", facet.ModerationStatus);
        Assert.Equal(facet.Id, Assert.Single(await ComboPublicCatalog.Eligible(f.Db).ToListAsync()).Id);
        var editor = await f.Editor(id);
        Assert.Equal(facet.Id, editor.Pricing?.ServiceItemId); Assert.False(editor.PricingSelectionRequired);
        Assert.Single(editor.PricingOptions!); Assert.Single(await f.Db.ServiceItems.ToListAsync());
        await Assert.ThrowsAsync<DomainException>(() => new ReviewServiceItemCommandHandler(f.Db, new AuditLogger(f.Db, f.Staff), f.Staff)
            .Handle(new(facet.Id, true, null, editor.PricingOptions![0].ReviewVersion), default));
    }

    [Fact]
    public async Task PublicRetailDetailsAndFiltersUseTheSameApprovedCanonicalPriceAndNeverExposePrivatePricingOptions()
    {
        await using var f = await Fixture.Create(); var id = await f.CreateService();
        var listing = await f.Db.Listings.SingleAsync();
        listing.Update(listing.Title, 1000000, 9000000, listing.Description, listing.Location); await f.Db.SaveChangesAsync();
        await f.Submit(id); await f.Approve(id);
        var detail = await new GetListingDetailQueryHandler(f.Db, new Current(null, null)).Handle(new(id), default);
        Assert.Equal(5000000, detail.Pricing!.BasePrice); Assert.Equal("Package", detail.Pricing.Unit);
        Assert.Null(detail.Pricing.SourceVersion); Assert.Null(detail.PricingOptions); Assert.Null(detail.RevisionHistory);
        var browse = new GetPublicListingsQueryHandler(f.Db);
        var card = Assert.Single((await browse.Handle(new(null, null, 4000000, 6000000, null), default)).Items);
        Assert.Equal(detail.Pricing.BasePrice, card.BasePrice); Assert.Equal(detail.Pricing.Unit, card.PriceUnit);
        Assert.Empty((await browse.Handle(new(null, null, 6000000, null, null), default)).Items);
        Assert.Empty((await browse.Handle(new(null, null, null, 4000000, null), default)).Items);
        Assert.Equal(1000000, detail.MinPrice); Assert.Equal(9000000, detail.MaxPrice);
    }

    [Fact]
    public async Task PublishedPriceEditsRemainProposalsAndPreserveAllUnchangedChildrenAndMediaIds()
    {
        await using var f = await Fixture.Create(); var id = await f.CreateService(); await f.Submit(id); await f.Approve(id);
        var before = await f.Editor(id); var pricing = before.Pricing!;
        var variantIds = pricing.Variants!.Select(x => x.Id).ToList(); var addOnIds = pricing.AddOns!.Select(x => x.Id).ToList();
        var mediaIds = before.Media.Select(x => x.Id).ToList();
        await f.Edit(id, pricing with { BasePrice = 6000000, Variants = null, AddOns = null });
        var proposed = await f.Editor(id); Assert.Equal(6000000, proposed.Pricing!.BasePrice);
        Assert.Equal(5000000, (await f.Db.ServiceItems.AsNoTracking().SingleAsync()).BasePrice);
        Assert.Equal("Approved", (await f.Db.ServiceItems.AsNoTracking().SingleAsync()).ModerationStatus);
        await f.Submit(id); var pending = (await f.Editor(id)).LatestRevision!.Id;
        await Assert.ThrowsAsync<DomainException>(() => f.Approve(id, Guid.NewGuid()));
        await f.Approve(id, pending);
        var after = await f.Editor(id); Assert.Equal(6000000, after.Pricing!.BasePrice);
        Assert.Equal(pricing.ServiceItemId, after.Pricing.ServiceItemId);
        Assert.Equal(variantIds, after.Pricing.Variants!.Select(x => x.Id)); Assert.Equal(addOnIds, after.Pricing.AddOns!.Select(x => x.Id));
        Assert.Equal(mediaIds, after.Media.Select(x => x.Id)); Assert.Single(await f.Db.ServiceItems.ToListAsync());
        Assert.Equal(1, await f.Db.ItemVariants.CountAsync()); Assert.Equal(1, await f.Db.ItemAddOns.CountAsync());
    }

    [Fact]
    public async Task LegacySiblingsRequireExplicitSelectionAndNeverMergeDeleteOrSilentlyReplaceTheSource()
    {
        await using var f = await Fixture.Create(); var id = await f.CreateService(null);
        var source = await f.Db.Listings.SingleAsync(); source.Approve();
        var first = f.Legacy(source, "First old option", 2000000); var second = f.Legacy(source, "Second old option", 3000000);
        await f.Db.SaveChangesAsync(); var before = await f.Editor(id);
        Assert.Null(before.Pricing); Assert.Null(before.PricingServiceItemId); Assert.True(before.PricingSelectionRequired); Assert.Equal(2, before.PricingOptions!.Count);
        await Assert.ThrowsAsync<DomainException>(() => f.Edit(id, f.DefaultPricing));
        await f.Edit(id, f.DefaultPricing with { ServiceItemId = second.Id, BasePrice = second.BasePrice });
        Assert.Null((await f.Db.Listings.AsNoTracking().SingleAsync()).PricingServiceItemId);
        await f.Submit(id); await f.Approve(id);
        Assert.Equal(second.Id, (await f.Db.Listings.AsNoTracking().SingleAsync()).PricingServiceItemId);
        Assert.Equal(2, await f.Db.ServiceItems.CountAsync());
        Assert.Equal("First old option", (await f.Db.ServiceItems.AsNoTracking().SingleAsync(x => x.Id == first.Id)).Name);
        Assert.Equal("Draft", (await f.Db.ServiceItems.AsNoTracking().SingleAsync(x => x.Id == first.Id)).ModerationStatus);
        Assert.Equal("Approved", (await f.Db.ServiceItems.AsNoTracking().SingleAsync(x => x.Id == second.Id)).ModerationStatus);
        await Assert.ThrowsAsync<DomainException>(() => f.Edit(id, f.DefaultPricing with { ServiceItemId = first.Id }));
        Assert.Equal(2, await f.Db.ServiceItems.CountAsync());
    }

    [Fact]
    public async Task OldCreateWithoutPricingCanBeConfiguredInPlaceAndCannotCreateDuplicateFacetsOnRepeatedEdits()
    {
        await using var f = await Fixture.Create(); var id = await f.CreateService(null);
        Assert.Empty(await f.Db.ServiceItems.ToListAsync());
        await f.Edit(id, f.DefaultPricing); var first = (await f.Editor(id)).Pricing!;
        await f.Edit(id, first with { Variants = null, AddOns = null });
        Assert.Single(await f.Db.Listings.ToListAsync()); Assert.Single(await f.Db.ServiceItems.ToListAsync());
        Assert.Equal(first.ServiceItemId, (await f.Editor(id)).Pricing!.ServiceItemId);
        Assert.Equal(first.Variants![0].Id, (await f.Editor(id)).Pricing!.Variants![0].Id);
        await f.Submit(id); await f.Reject(id, "Clarify policy");
        Assert.Equal("Rejected", (await f.Db.ServiceItems.AsNoTracking().SingleAsync()).ModerationStatus);
        await f.Edit(id, (await f.Editor(id)).Pricing! with { CancellationPolicy = "Clarified reviewed terms" });
        await f.Submit(id); await f.Approve(id);
        Assert.Equal("Approved", (await f.Db.ServiceItems.AsNoTracking().SingleAsync()).ModerationStatus);
        Assert.Equal("Clarified reviewed terms", (await f.Db.Listings.AsNoTracking().SingleAsync()).CancellationPolicy);
    }

    [Fact]
    public async Task LegacySaveOfCanonicalFacetUsesListingReviewAndReusesItsIdEvenWhenOldClientOmitsId()
    {
        await using var f = await Fixture.Create(); var id = await f.CreateService(); await f.Submit(id); await f.Approve(id);
        var before = await f.Editor(id); var p = before.Pricing!;
        var request = new SaveServiceItemCommand(null, id, "Legacy requested source edit", 7000000, p.Unit, p.MinGuests, p.MaxGuests,
            p.Regions, p.CancellationPolicy!, true, p.Variants!, p.AddOns!, true, p.MaxBookingsPerDay);
        await new SaveServiceItemCommandHandler(f.Db, f.Owner, new AuditLogger(f.Db, f.Owner)).Handle(request, default);
        Assert.Single(await f.Db.ServiceItems.ToListAsync());
        Assert.Equal(5000000, (await f.Db.ServiceItems.AsNoTracking().SingleAsync()).BasePrice);
        var editor = await f.Editor(id); Assert.Equal("Pending", editor.LatestRevision!.Status); Assert.Equal(7000000, editor.Pricing!.BasePrice);
        await f.Approve(id); Assert.Equal(7000000, (await f.Db.ServiceItems.AsNoTracking().SingleAsync()).BasePrice);
        Assert.Equal(p.ServiceItemId, (await f.Db.Listings.AsNoTracking().SingleAsync()).PricingServiceItemId);
    }

    [Fact]
    public async Task PendingAndForeignOrRoleSpoofedWritesCannotChangeSourcesOrChildren()
    {
        await using var f = await Fixture.Create(); var id = await f.CreateService(); var before = await f.Editor(id);
        await Assert.ThrowsAsync<DomainException>(() => f.Edit(id, before.Pricing! with { Variants = [new(Guid.NewGuid(), "Foreign variant", 0)] }));
        await Assert.ThrowsAsync<DomainException>(() => new UpdateListingCommandHandler(f.Db, new Current(f.Owner.UserId, "Moderator"))
            .Handle(f.Update(id, before.Pricing), default));
        await Assert.ThrowsAsync<NotFoundException>(() => new UpdateListingCommandHandler(f.Db, f.Other)
            .Handle(f.Update(id, before.Pricing), default));
        await f.Submit(id); var snapshot = await f.Snapshot();
        await Assert.ThrowsAsync<DomainException>(() => f.Edit(id, before.Pricing! with { BasePrice = 1 }));
        await Assert.ThrowsAsync<DomainException>(() => new ApproveListingCommandHandler(f.Db, new Current(f.Owner.UserId, "Moderator")).Handle(new(id), default));
        Assert.Equal(snapshot, await f.Snapshot());
    }

    [Fact]
    public async Task ChangingAnUnlinkedLegacyFacetAfterSubmissionInvalidatesItsSourceProposal()
    {
        await using var f = await Fixture.Create(); var id = await f.CreateService(null);
        var source = await f.Db.Listings.SingleAsync(); source.Approve(); var legacy = f.Legacy(source, "Selected legacy", 2000000); await f.Db.SaveChangesAsync();
        await f.Edit(id, f.DefaultPricing with { ServiceItemId = legacy.Id }); await f.Submit(id);
        f.Db.ChangeTracker.Clear(); var changed = await f.Db.ServiceItems.SingleAsync();
        changed.Update("Concurrent legacy edit", 9000000, "Package", 1, 2000, "HCM", "Changed policy", true); await f.Db.SaveChangesAsync();
        var before = await f.Snapshot(); await Assert.ThrowsAsync<DomainException>(() => f.Approve(id));
        Assert.Equal(before, await f.Snapshot()); Assert.Null((await f.Db.Listings.AsNoTracking().SingleAsync()).PricingServiceItemId);
        Assert.Equal(9000000, (await f.Db.ServiceItems.AsNoTracking().SingleAsync()).BasePrice);
    }

    [Fact]
    public async Task ListingActivityAndReviewNeverReenableAnIndependentlyInactiveFacet()
    {
        await using var f = await Fixture.Create(); var id = await f.CreateService(); await f.Submit(id); await f.Approve(id);
        var item = await f.Db.ServiceItems.SingleAsync(); f.Db.Entry(item).Property(x => x.IsActive).CurrentValue = false; await f.Db.SaveChangesAsync();
        await new SetListingActivityCommandHandler(f.Db, f.Owner).Handle(new(id, false), default);
        await new SetListingActivityCommandHandler(f.Db, f.Owner).Handle(new(id, true), default);
        await f.Edit(id, (await f.Editor(id)).Pricing! with { BasePrice = 6000000 }); await f.Submit(id); await f.Approve(id);
        Assert.False((await f.Db.ServiceItems.AsNoTracking().SingleAsync()).IsActive);
        Assert.Empty(await ComboPublicCatalog.Eligible(f.Db).ToListAsync());
    }

    [Fact]
    public async Task FailureBeforeCommitRollsBackListingApprovalAndFacetApprovalTogether()
    {
        await using var f = await Fixture.Create(); var id = await f.CreateService(); await f.Submit(id); var before = await f.Snapshot();
        await using var failure = new FailAfterSaveContext(f.Options);
        await Assert.ThrowsAsync<InvalidOperationException>(() => new ApproveListingCommandHandler(failure, f.Staff).Handle(new(id), default));
        Assert.Equal(before, await f.Snapshot());
        Assert.Equal("Pending", (await f.Db.ServiceItems.AsNoTracking().SingleAsync()).ModerationStatus);
    }

    [SqlServerFact]
    public async Task SqlReloadPreservesOneApprovalSourceLinkChildrenAndContentVersionForScaledWholeMoney()
    {
        var database = $"WeddingPlanTest_UnifiedService_{Guid.NewGuid():N}";
        var source = Environment.GetEnvironmentVariable("WEDDING_TEST_SQLSERVER");
        var connection = new SqlConnectionStringBuilder(string.IsNullOrWhiteSpace(source) || source == "1"
            ? "Server=localhost;Integrated Security=true;TrustServerCertificate=true;Connection Timeout=5" : source) { InitialCatalog = database };
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlServer(connection.ConnectionString).Options;
        await using var db = new ApplicationDbContext(options);
        try
        {
            await db.Database.MigrateAsync(); await using var f = await Fixture.Seed(db, options);
            var id = await f.CreateService(f.DefaultPricing with { BasePrice = 5000000.000m }); await f.Submit(id);
            db.ChangeTracker.Clear(); await using var reviewDb = new ApplicationDbContext(options);
            await new ApproveListingCommandHandler(reviewDb, f.Staff).Handle(new(id), default);
            await using var read = new ApplicationDbContext(options);
            var editor = await new GetListingEditorQueryHandler(read, f.Owner).Handle(new(id), default);
            Assert.Equal("Approved", Assert.Single(await read.ServiceItems.AsNoTracking().ToListAsync()).ModerationStatus);
            Assert.Equal(editor.PricingServiceItemId, editor.Pricing!.ServiceItemId);
            var variantId = editor.Pricing.Variants![0].Id; var addOnId = editor.Pricing.AddOns![0].Id;
            await new UpdateListingCommandHandler(read, f.Owner).Handle(f.Update(id, editor.Pricing with { BasePrice = 6500000.00m }), default);
            await new SubmitForApprovalCommandHandler(read, f.Owner).Handle(new(id), default);
            read.ChangeTracker.Clear(); await using var approval = new ApplicationDbContext(options);
            await new ApproveListingCommandHandler(approval, f.Staff).Handle(new(id), default);
            var final = await new GetListingEditorQueryHandler(approval, f.Owner).Handle(new(id), default);
            Assert.Equal(6500000, final.Pricing!.BasePrice); Assert.Equal(variantId, final.Pricing.Variants![0].Id); Assert.Equal(addOnId, final.Pricing.AddOns![0].Id);
            Assert.Single(await approval.ServiceItems.AsNoTracking().ToListAsync());
            Assert.Empty(await approval.Database.GetPendingMigrationsAsync()); Assert.False(approval.Database.HasPendingModelChanges());
        }
        finally { Assert.StartsWith("WeddingPlanTest_UnifiedService_", database); await db.Database.EnsureDeletedAsync(); }
    }

    private sealed record Current(Guid? UserId, string? Role) : ICurrentUserService { public string? Email => null; }
    private sealed class FailAfterSaveContext(DbContextOptions<ApplicationDbContext> options) : ApplicationDbContext(options)
    {
        public override async Task<int> SaveChangesAsync(CancellationToken ct = default)
        { await base.SaveChangesAsync(ct); throw new InvalidOperationException("Synthetic unified service precommit failure."); }
    }
    private sealed class Fixture(ApplicationDbContext db, DbContextOptions<ApplicationDbContext> options, SqliteConnection? connection = null) : IAsyncDisposable
    {
        public ApplicationDbContext Db { get; } = db;
        public DbContextOptions<ApplicationDbContext> Options { get; } = options;
        public Current Owner { get; private set; } = null!; public Current Staff { get; private set; } = null!; public Current Other { get; private set; } = null!;
        public Vendor Vendor { get; private set; } = null!; public Category Category { get; private set; } = null!;
        public ServicePricingDto DefaultPricing => new(5000000, "Package", 1, 2000, "HCM", "Reviewed cancellation policy", 2,
            Variants: [new(null, "Preserved variant", 100000.00m)], AddOns: [new(null, "Preserved add-on", 50000.000m, false)]);
        public Task<Guid> CreateService() => CreateService(DefaultPricing);
        public Task<Guid> CreateService(ServicePricingDto? pricing) => new CreateListingCommandHandler(Db, Owner)
            .Handle(new(Category.Id, "Unified service", 5000000, 5000000, "Unified source description", "HCM", ["https://images.example.invalid/unified.jpg"], Pricing: pricing), default);
        public UpdateListingCommand Update(Guid id, ServicePricingDto? pricing) => new(id, "Unified service", 5000000, 5000000, "Unified source description", "HCM", Pricing: pricing);
        public Task Edit(Guid id, ServicePricingDto? pricing) => new UpdateListingCommandHandler(Db, Owner).Handle(Update(id, pricing), default);
        public Task Submit(Guid id) => new SubmitForApprovalCommandHandler(Db, Owner).Handle(new(id), default);
        public Task Approve(Guid id, Guid? revision = null) => new ApproveListingCommandHandler(Db, Staff).Handle(new(id, revision), default);
        public Task Reject(Guid id, string reason) => new RejectListingCommandHandler(Db, Staff).Handle(new(id, reason), default);
        public Task<ListingDto> Editor(Guid id) => new GetListingEditorQueryHandler(Db, Owner).Handle(new(id), default);
        public ServiceItem Legacy(Listing listing, string name, decimal price)
        { var item = ServiceItem.Create(Vendor.Id, listing.Id, Category.Id, name, price, "Package", 1, 2000, "HCM", "Legacy preserved policy"); Db.ServiceItems.Add(item); return item; }
        public async Task<string> Snapshot()
        {
            await using var read = new ApplicationDbContext(Options);
            return JsonSerializer.Serialize(new { Listings = await read.Listings.AsNoTracking().Select(x => new { x.Id, x.Title, x.Status, x.PricingServiceItemId, x.CancellationPolicy, x.UpdatedAt }).ToListAsync(),
                Items = await read.ServiceItems.AsNoTracking().Select(x => new { x.Id, x.Name, x.BasePrice, x.ModerationStatus, x.ReviewNote, x.UpdatedAt }).ToListAsync(),
                Revisions = await read.ListingRevisions.AsNoTracking().Select(x => new { x.Id, x.Status, x.ContentJson }).ToListAsync(),
                Audits = await read.AuditLogs.CountAsync(), Notifications = await read.Notifications.CountAsync() });
        }
        public static async Task<Fixture> Create()
        {
            var connection = new SqliteConnection("Data Source=:memory:"); await connection.OpenAsync();
            var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options; var db = new ApplicationDbContext(options);
            await db.Database.EnsureCreatedAsync(); return await Seed(db, options, connection);
        }
        public static async Task<Fixture> Seed(ApplicationDbContext db, DbContextOptions<ApplicationDbContext> options, SqliteConnection? connection = null)
        {
            var f = new Fixture(db, options, connection);
            User User(string name, UserRole role) { var user = Wedding.Domain.Entities.User.Create(name, $"{name}@unified-service.example.invalid", "0901234567", "synthetic-hash", role); db.Users.Add(user); return user; }
            var owner = User("owner", UserRole.VendorOwner); var moderator = User("moderator", UserRole.Moderator); var other = User("other", UserRole.VendorOwner);
            f.Owner = new(owner.Id, "VendorOwner"); f.Staff = new(moderator.Id, "Moderator"); f.Other = new(other.Id, "VendorOwner");
            f.Vendor = Vendor.Create(owner.Id, "Unified service shop", "unified-service-shop", "Owner", "0901234567", "HCM", .08m); f.Vendor.SetVerified(true); f.Vendor.ConfigureComboParticipation(true, .15m); db.Vendors.Add(f.Vendor);
            db.Vendors.Add(Vendor.Create(other.Id, "Other shop", "unified-other-shop", "Other", "0901234567", "HCM", .08m));
            var tax = VendorTaxProfile.Create(f.Vendor.Id); tax.Update("Synthetic business", "1234567890", "VCB", "123456789", "Synthetic holder", true); tax.Submit(); tax.Review(true); db.VendorTaxProfiles.Add(tax);
            f.Category = await db.Categories.SingleAsync(x => x.Slug == "quay-chup-photo-video");
            db.VendorBusinessGroups.Add(VendorBusinessGroup.Create(f.Vendor.Id, f.Category.BusinessGroupId!.Value)); await db.SaveChangesAsync(); return f;
        }
        public async ValueTask DisposeAsync() { if (connection != null) { await Db.DisposeAsync(); await connection.DisposeAsync(); } }
    }
}
