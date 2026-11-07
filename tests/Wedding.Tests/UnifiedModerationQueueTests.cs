using System.Text.Encodings.Web;
using System.Text.Json;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Data.SqlClient;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Wedding.Application.Common.Interfaces;
using Wedding.Application.Features.Combos;
using Wedding.Application.Features.Listings;
using Wedding.Application.Features.Listings.Commands.ApproveListing;
using Wedding.Application.Features.ModerationQueue;
using Wedding.Domain.Entities;
using Wedding.Domain.Enums;
using Wedding.Domain.Exceptions;
using Wedding.Infrastructure.Persistence;

namespace Wedding.Tests;

public class UnifiedModerationQueueTests
{
    [Theory]
    [InlineData("unknown", 20)] [InlineData("all", 0)] [InlineData("all", 51)]
    public async Task BulkRejectsInvalidScopeOrLimitBeforeCallingAnyApproval(string type, int size)
    {
        await using var f = await Fixture.Create();
        var audits = await f.Db.AuditLogs.CountAsync();
        await Assert.ThrowsAsync<DomainException>(() => new AutoApproveModerationQueueCommandHandler(f.Db, f.Moderator, null!).Handle(new(type, size), default));
        Assert.Equal(audits, await f.Db.AuditLogs.CountAsync());
    }

    [Fact]
    public async Task BulkApprovesAllThreeKindsThroughCanonicalHandlersAndSkipsInvalidProposalIndependently()
    {
        await using var f = await Fixture.Create();
        await AssertSuccessfulBulk(f);
    }

    [Fact]
    public async Task BulkMayApproveUnverifiedVendorListingsWithoutMarkingVendorVerifiedOrWaivingComboEligibility()
    {
        await using var f = await Fixture.Create();
        var result = await new AutoApproveModerationQueueCommandHandler(f.Db, f.Moderator, f.Mediator()).Handle(new(), default);
        Assert.Equal(2, result.ApprovedListings); Assert.Equal(1, result.ApprovedServices);
        Assert.Equal(0, result.ApprovedCombos); Assert.Equal(1, result.Skipped);
        Assert.False((await f.Db.Vendors.AsNoTracking().SingleAsync()).IsVerified);
    }

    [Fact]
    public async Task BulkNeverApprovesCanonicalPricingFacetAsASeparateServiceItem()
    {
        await using var f = await Fixture.Create();
        f.Live.AttachPricingServiceItem(f.Service.Id); await f.Db.SaveChangesAsync();
        var result = await new AutoApproveModerationQueueCommandHandler(f.Db, f.Moderator, f.Mediator()).Handle(new("service-item"), default);
        Assert.Equal(0, result.Attempted); Assert.Equal(0, result.TotalApproved);
        Assert.Equal("Pending", (await f.Db.ServiceItems.AsNoTracking().SingleAsync(row => row.Id == f.Service.Id)).ModerationStatus);
    }

    [Fact]
    public async Task BulkLimitsEachKindAndReportsMoreWithoutReviewingDraftsOrCanonicalFacetsSeparately()
    {
        await using var f = await Fixture.Create();
        var beforeDraft = await f.Db.Listings.CountAsync(row => row.Status == ListingStatus.Draft);
        var result = await new AutoApproveModerationQueueCommandHandler(f.Db, f.Moderator, f.Mediator()).Handle(new("listing", 1), default);
        Assert.Equal(1, result.Attempted); Assert.Equal(1, result.TotalApproved); Assert.Equal(0, result.Skipped); Assert.True(result.HasMore);
        Assert.Equal(0, result.ApprovedServices); Assert.Equal(0, result.ApprovedCombos);
        Assert.Equal(beforeDraft, await f.Db.Listings.CountAsync(row => row.Status == ListingStatus.Draft));
        Assert.Equal("Pending", (await f.Db.ServiceItems.AsNoTracking().SingleAsync(row => row.Id == f.Service.Id)).ModerationStatus);
    }

    [Fact]
    public async Task BulkPropagatesCancellationAndUnexpectedFailureInsteadOfReportingFalseSuccess()
    {
        await using var f = await Fixture.Create();
        var audits = await f.Db.AuditLogs.CountAsync();
        var cancelled = new CancellationTokenSource(); cancelled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new AutoApproveModerationQueueCommandHandler(f.Db, f.Moderator, f.Mediator()).Handle(new(), cancelled.Token));
        var error = new InvalidOperationException("Synthetic infrastructure failure");
        var actual = await Assert.ThrowsAsync<InvalidOperationException>(() => new AutoApproveModerationQueueCommandHandler(f.Db, f.Moderator, f.Mediator(new FailingListingHandler(error))).Handle(new(), default));
        Assert.Same(error, actual); Assert.Equal(audits, await f.Db.AuditLogs.CountAsync());
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new AutoApproveModerationQueueCommandHandler(f.Db, f.Moderator, f.Mediator(new FailingListingHandler(new OperationCanceledException()))).Handle(new(), default));
    }

    private static async Task AssertSuccessfulBulk(Fixture f)
    {
        var vendor = await f.Db.Vendors.SingleAsync(); vendor.SetVerified(true); vendor.ConfigureComboParticipation(true, 0);
        var invalid = Listing.Create(vendor.Id, f.Live.CategoryId, "Malformed synthetic proposal", "malformed-bulk-qa", 1000, 2000, "Synthetic invalid draft content", "HCM");
        f.Db.Listings.Add(invalid);
        f.Db.ListingRevisions.Add(ListingRevision.Create(invalid.Id, 1, "{malformed", f.Owner.Id, true));
        await f.Db.SaveChangesAsync(); f.Db.ChangeTracker.Clear();
        var beforeContracts = await f.Db.BookingContracts.CountAsync();
        var result = await new AutoApproveModerationQueueCommandHandler(f.Db, f.Moderator, f.Mediator()).Handle(new(), default);
        Assert.Equal(2, result.ApprovedListings); Assert.Equal(1, result.ApprovedServices); Assert.Equal(1, result.ApprovedCombos);
        Assert.Equal(4, result.TotalApproved); Assert.Equal(5, result.Attempted); Assert.Equal(1, result.Skipped); Assert.False(result.HasMore);
        Assert.Equal("Pending", (await f.Db.ListingRevisions.AsNoTracking().SingleAsync(row => row.ListingId == invalid.Id)).Status);
        Assert.Equal("Approved", (await f.Db.ListingRevisions.AsNoTracking().SingleAsync(row => row.Id == f.ListingRevision.Id)).Status);
        Assert.Equal("Approved", (await f.Db.ComboTemplateRevisions.AsNoTracking().SingleAsync(row => row.Id == f.ComboRevision.Id)).Status);
        Assert.Equal(beforeContracts, await f.Db.BookingContracts.CountAsync());
        Assert.Equal(2, await f.Db.AuditLogs.CountAsync(row => row.Action == "Listing.Approve"));
    }

    [Theory]
    [InlineData("anonymous")] [InlineData("unknown")] [InlineData("inactive")]
    [InlineData("VendorOwner")] [InlineData("Customer")] [InlineData("Finance")] [InlineData("CustomerCare")]
    [InlineData("forged")] [InlineData("mismatch")]
    public async Task PageAndEveryDetailTypeRejectNonPersistedOrWrongStaffIdentity(string identity)
    {
        await using var f = await Fixture.Create();
        var actor = identity switch {
            "anonymous" => new Current(null, null), "unknown" => new Current(Guid.NewGuid(), "Moderator"),
            "inactive" => f.Moderator, "forged" => new Current(f.Owner.Id, "SuperAdmin"),
            "mismatch" => new Current(f.Moderator.UserId, "SuperAdmin"),
            _ => new Current(f.Users.Single(user => user.Role.ToString() == identity).Id, identity)
        };
        if (identity == "inactive") { f.Users.Single(user => user.Id == f.Moderator.UserId).SetStatus(false); await f.Db.SaveChangesAsync(); }
        var audits = await f.Db.AuditLogs.CountAsync();
        await Assert.ThrowsAsync<DomainException>(() => new GetModerationQueueQueryHandler(f.Db, actor).Handle(new(), default));
        await Assert.ThrowsAsync<DomainException>(() => new AutoApproveModerationQueueCommandHandler(f.Db, actor, null!).Handle(new(), default));
        foreach (var (kind, id) in new[] { ("listing", f.Live.Id), ("service-item", f.Service.Id), ("combo", f.Combo.Id) })
            await Assert.ThrowsAsync<DomainException>(() => new GetModerationQueueDetailQueryHandler(f.Db, actor).Handle(new(kind, id), default));
        Assert.Equal(audits, await f.Db.AuditLogs.CountAsync());
    }

    [Fact]
    public async Task DefaultPageUnifiesLatestPendingProposalsAndDoesNotHydratePublishedBaselineContent()
    {
        await using var f = await Fixture.Create();
        var result = await f.Page(new());
        Assert.Equal(4, result.TotalCount); Assert.Equal(4, result.PendingCount); Assert.Equal("Pending", result.Status);
        Assert.Equal(new[] { "combo", "listing", "service-item" }, result.Items.Select(item => item.Type).Distinct().Order());
        var listing = Assert.Single(result.Items, row => row.Id == f.Live.Id);
        Assert.Equal("Latest private listing proposal", listing.Title); Assert.Equal(f.ListingRevision.Id, listing.RevisionId);
        var combo = Assert.Single(result.Items, row => row.Id == f.Combo.Id);
        Assert.Equal("Latest private combo proposal", combo.Title); Assert.Equal(f.ComboRevision.Id, combo.RevisionId);
        Assert.Equal("Still public listing", (await f.Db.Listings.AsNoTracking().SingleAsync(row => row.Id == f.Live.Id)).Title);
        Assert.Equal("Still public combo", (await f.Db.ComboTemplates.AsNoTracking().SingleAsync(row => row.Id == f.Combo.Id)).Name);
        Assert.All(result.Items, row => Assert.Equal(DateTimeKind.Utc, row.ChangedAt.Kind));
    }

    [Fact]
    public async Task TypeStatusSearchAndStableServerPagingComposeWithoutRepeatingRows()
    {
        await using var f = await Fixture.Create();
        var first = await f.Page(new(PageSize: 2)); var second = await f.Page(new(Page: 2, PageSize: 2));
        Assert.Equal(2, first.Items.Count); Assert.Equal(2, second.Items.Count);
        Assert.Equal(4, first.Items.Concat(second.Items).Select(row => row.Id).Distinct().Count());
        Assert.Equal(first.Items, (await f.Page(new(PageSize: 2))).Items);
        var bounded = await f.Page(new(Page: int.MaxValue, PageSize: 2)); Assert.Equal(2, bounded.Page);
        var clamped = await f.Page(new(Page: -1, PageSize: 1000)); Assert.Equal(1, clamped.Page); Assert.Equal(20, clamped.PageSize);
        var listing = await f.Page(new(Type: "listing", Search: "LATEST PRIVATE")); Assert.Equal(f.Live.Id, Assert.Single(listing.Items).Id);
        var staleName = await f.Page(new(Type: "listing", Search: "Still public listing")); Assert.Empty(staleName.Items);
        var draft = await f.Page(new(Status: "Draft")); Assert.Equal(2, draft.TotalCount); Assert.Equal(4, draft.PendingCount);
        var approved = await f.Page(new(Type: "service-item", Status: "Approved")); Assert.Single(approved.Items);
        var shop = await f.Page(new(Search: "SYNTHETIC QUEUE SHOP")); Assert.Equal(4, shop.TotalCount);
        var all = await f.Page(new(Status: "all")); Assert.Equal(7, all.TotalCount);
        var empty = await f.Page(new(Search: "missing synthetic term", Page: 5)); Assert.Empty(empty.Items); Assert.Equal(1, empty.Page);
    }

    [Fact]
    public async Task SelectedDetailUsesExactProposalGalleryAndTokenWithoutReadingAllCatalogs()
    {
        await using var f = await Fixture.Create();
        var listing = await f.Detail("listing", f.Live.Id, f.ListingRevision.Id);
        Assert.Null(listing.ServiceItem); Assert.Null(listing.Combo);
        Assert.Equal("Latest private listing proposal", listing.Listing!.Title);
        Assert.Equal("https://cdn.example.invalid/private-listing.jpg", Assert.Single(listing.Listing.Media).MediaUrl);
        Assert.Equal(f.ListingRevision.Id, listing.Listing.LatestRevision!.Id);
        var item = await f.Detail("service-item", f.Service.Id);
        Assert.Null(item.Listing); Assert.Null(item.Combo); Assert.Equal("Private service item", item.ServiceItem!.Name);
        Assert.Equal(64, item.ServiceItem.ReviewVersion.Length); Assert.Single(item.ServiceItem.Variants);
        var combo = await f.Detail("combo", f.Combo.Id, f.ComboRevision.Id);
        Assert.Equal("Latest private combo proposal", combo.Combo!.Name);
        Assert.Equal(f.ComboRevision.Id, combo.Combo.LatestRevision!.Id); Assert.Single(combo.Combo.PreviewOptions!);
        Assert.Equal("https://cdn.example.invalid/private-combo.jpg", Assert.Single(combo.Combo.Content!.GalleryUrls!));
        await Assert.ThrowsAsync<NotFoundException>(() => f.Detail("listing", Guid.NewGuid()));
        await Assert.ThrowsAsync<NotFoundException>(() => f.Detail("combo", f.Combo.Id, f.ListingRevision.Id));
        await Assert.ThrowsAsync<DomainException>(() => f.Detail("service-item", f.Service.Id, Guid.NewGuid()));
    }

    [Fact]
    public async Task ReplacingPendingProposalWithDraftRemovesItFromPendingAndOldReviewRemainsRejected()
    {
        await using var f = await Fixture.Create();
        f.ListingRevision.Supersede();
        var next = ListingRevision.Create(f.Live.Id, 2, f.ListingRevision.ContentJson.Replace("Latest private listing proposal", "New unsent draft"), f.Owner.Id);
        f.Db.ListingRevisions.Add(next); await f.Db.SaveChangesAsync();
        var pending = await f.Page(new()); Assert.Equal(3, pending.TotalCount); Assert.DoesNotContain(pending.Items, row => row.Id == f.Live.Id);
        var draft = await f.Page(new(Type: "listing", Status: "Draft", Search: "New unsent"));
        Assert.Equal(next.Id, Assert.Single(draft.Items).RevisionId);
        await Assert.ThrowsAsync<DomainException>(() => new ApproveListingCommandHandler(f.Db, f.Moderator).Handle(new(f.Live.Id, f.ListingRevision.Id), default));
        Assert.Equal("Still public listing", (await f.Db.Listings.AsNoTracking().SingleAsync(row => row.Id == f.Live.Id)).Title);
    }

    [Fact]
    public async Task AdminCanReadRejectedLatestVersionsAndReviewingDoesNotBringThemBackToPending()
    {
        await using var f = await Fixture.Create();
        var admin = new Current(f.Users.Single(user => user.Role == UserRole.SuperAdmin).Id, "SuperAdmin");
        f.ListingRevision.Review(false, admin.UserId!.Value, "Need better listing photos");
        f.ComboRevision.Review(false, admin.UserId.Value, "SuperAdmin", "Need clearer combo terms");
        await f.Db.SaveChangesAsync(); var audits = await f.Db.AuditLogs.CountAsync();
        var rows = await new GetModerationQueueQueryHandler(f.Db, admin).Handle(new(Status: "Rejected"), default);
        Assert.Equal(2, rows.TotalCount); Assert.Equal(2, rows.PendingCount);
        Assert.Equal(f.ComboRevision.Id, Assert.Single(rows.Items, row => row.Type == "combo").RevisionId);
        var listing = await new GetModerationQueueDetailQueryHandler(f.Db, admin).Handle(new("listing", f.Live.Id, f.ListingRevision.Id), default);
        Assert.Equal("Rejected", listing.Listing!.LatestRevision!.Status);
        Assert.Equal("Need better listing photos", listing.Listing.LatestRevision.ReviewNote);
        Assert.Equal(audits, await f.Db.AuditLogs.CountAsync());
    }

    [Theory]
    [InlineData("invalid-type")] [InlineData("invalid-status")] [InlineData("search-limit")]
    public async Task InvalidFilterValuesFailClosed(string condition)
    {
        await using var f = await Fixture.Create();
        var request = condition switch { "invalid-type" => new GetModerationQueueQuery(Type: "contracts"),
            "invalid-status" => new(Status: "Superseded"), _ => new(Search: new string('x', 151)) };
        await Assert.ThrowsAsync<DomainException>(() => f.Page(request));
        await Assert.ThrowsAsync<DomainException>(() => f.Detail("contracts", f.Live.Id));
    }

    [SqlServerFact]
    public async Task RealSqlUnionsFiltersPagesAndPrivateDetailsTranslateAndPreserveStoredContent()
    {
        var database = $"WeddingPlanTest_ModerationQueue_{Guid.NewGuid():N}";
        var source = Environment.GetEnvironmentVariable("WEDDING_TEST_SQLSERVER");
        var builder = new SqlConnectionStringBuilder(string.IsNullOrWhiteSpace(source) || source == "1" ?
            "Server=localhost;Integrated Security=true;TrustServerCertificate=true;Connection Timeout=5" : source) { InitialCatalog = database };
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlServer(builder.ConnectionString).Options;
        await using var db = new ApplicationDbContext(options);
        try
        {
            await db.Database.MigrateAsync(); await using var f = await Fixture.Seed(db, null);
            db.ChangeTracker.Clear();
            var first = await f.Page(new(PageSize: 2)); var second = await f.Page(new(Page: 2, PageSize: 2));
            Assert.Equal(4, first.TotalCount); Assert.Equal(4, first.PendingCount);
            Assert.Equal(4, first.Items.Concat(second.Items).Select(row => row.Id).Distinct().Count());
            var filtered = await f.Page(new(Type: "combo", Search: "Latest private combo"));
            Assert.Equal(f.ComboRevision.Id, Assert.Single(filtered.Items).RevisionId);
            Assert.Equal("Latest private combo proposal", (await f.Detail("combo", f.Combo.Id, f.ComboRevision.Id)).Combo!.Name);
            Assert.Equal("Latest private listing proposal", (await f.Detail("listing", f.Live.Id, f.ListingRevision.Id)).Listing!.Title);
            Assert.Equal(64, (await f.Detail("service-item", f.Service.Id)).ServiceItem!.ReviewVersion.Length);
            Assert.Equal("Still public combo", (await db.ComboTemplates.AsNoTracking().SingleAsync(row => row.Id == f.Combo.Id)).Name);
            Assert.Empty(await db.Database.GetPendingMigrationsAsync()); Assert.False(db.Database.HasPendingModelChanges());
            await AssertSuccessfulBulk(f);
        }
        finally { Assert.StartsWith("WeddingPlanTest_ModerationQueue_", database); await using var cleanup = new ApplicationDbContext(options); await cleanup.Database.EnsureDeletedAsync(); }
    }

    private sealed record Current(Guid? UserId, string? Role) : ICurrentUserService { public string? Email => null; }
    private sealed class Audit : IAuditLogger { public Task LogAsync(string action, string entityType, string? entityId, object? data = null) => Task.CompletedTask; }
    private sealed class FailingListingHandler(Exception error) : IRequestHandler<ApproveListingCommand>
    { public Task Handle(ApproveListingCommand request, CancellationToken ct) => Task.FromException(error); }
    private sealed class Fixture(ApplicationDbContext db, SqliteConnection? connection) : IAsyncDisposable
    {
        public ApplicationDbContext Db { get; } = db; public List<User> Users { get; } = [];
        public IMediator Mediator(IRequestHandler<ApproveListingCommand>? listingHandler = null)
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton<IApplicationDbContext>(Db); services.AddSingleton<ICurrentUserService>(Moderator); services.AddSingleton<IAuditLogger>(new Audit());
            services.AddMediatR(config => config.RegisterServicesFromAssemblyContaining<AutoApproveModerationQueueCommand>());
            if (listingHandler != null) services.AddSingleton(listingHandler);
            return services.BuildServiceProvider().GetRequiredService<IMediator>();
        }
        public User Owner { get; private set; } = null!; public Current Moderator { get; private set; } = null!;
        public Listing Live { get; private set; } = null!; public ListingRevision ListingRevision { get; private set; } = null!;
        public ServiceItem Service { get; private set; } = null!; public ComboTemplate Combo { get; private set; } = null!;
        public ComboTemplateRevision ComboRevision { get; private set; } = null!;
        public Task<ModerationQueuePage> Page(GetModerationQueueQuery request) => new GetModerationQueueQueryHandler(Db, Moderator).Handle(request, default);
        public Task<ModerationQueueDetail> Detail(string type, Guid id, Guid? revision = null) => new GetModerationQueueDetailQueryHandler(Db, Moderator).Handle(new(type, id, revision), default);
        public static async Task<Fixture> Create()
        {
            var connection = new SqliteConnection("Data Source=:memory:"); await connection.OpenAsync();
            var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options);
            await db.Database.EnsureCreatedAsync(); return await Seed(db, connection);
        }
        public static async Task<Fixture> Seed(ApplicationDbContext db, SqliteConnection? connection)
        {
            var f = new Fixture(db, connection);
            User Add(UserRole role) { var user = User.Create($"Synthetic queue {role}", $"{role}@queue.example.invalid", "0901234567", "synthetic-hash", role); f.Users.Add(user); db.Users.Add(user); return user; }
            f.Owner = Add(UserRole.VendorOwner); f.Moderator = new(Add(UserRole.Moderator).Id, "Moderator");
            Add(UserRole.SuperAdmin); Add(UserRole.Customer); Add(UserRole.Finance); Add(UserRole.CustomerCare);
            var vendor = Vendor.Create(f.Owner.Id, "Synthetic queue shop", "synthetic-queue-shop", "Owner", "0901234567", "HCM", .08m); db.Vendors.Add(vendor);
            var category = await db.Categories.SingleAsync(category => category.Slug == "quay-chup-photo-video");
            f.Live = Listing.Create(vendor.Id, category.Id, "Still public listing", "still-public-queue-listing", 1000, 2000, "Public baseline", "HCM"); f.Live.Approve(); db.Listings.Add(f.Live);
            db.Listings.Add(Listing.Create(vendor.Id, category.Id, "Unsent listing", "unsent-queue-listing", 1000, 2000, "Draft listing", "HCM"));
            var legacy = Listing.Create(vendor.Id, category.Id, "Legacy pending listing", "legacy-pending-queue", 1000, 2000, "Legacy listing", "HCM"); legacy.SubmitForApproval(); db.Listings.Add(legacy);
            var options = new JsonSerializerOptions { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
            f.ListingRevision = ListingRevision.Create(f.Live.Id, 1, JsonSerializer.Serialize(new ListingRevisionContent("Latest private listing proposal", 3000, 4000, "Private revision description", "HCM", ["https://cdn.example.invalid/private-listing.jpg"], null), options), f.Owner.Id, true);
            db.ListingRevisions.Add(f.ListingRevision);
            f.Service = ServiceItem.Create(vendor.Id, f.Live.Id, category.Id, "Private service item", 1000, "Package", 1, 1000, "HCM", "Original cancellation"); f.Service.Submit(); f.Service.Variants.Add(ItemVariant.Create(f.Service.Id, "Service variant", 100)); db.ServiceItems.Add(f.Service);
            var approved = ServiceItem.Create(vendor.Id, f.Live.Id, category.Id, "Approved item", 2000, "Package", 1, 1000, "HCM", "Cancellation"); approved.Submit(); approved.Review(true, null); db.ServiceItems.Add(approved);
            f.Combo = ComboTemplate.Create("Still public combo", "Popular", "Classic", "Original baseline", null, 0); f.Combo.SetScope(vendor.Id, "HCM", null, true); db.ComboTemplates.Add(f.Combo);
            var slot = ComboTemplateItem.Create(f.Combo.Id, category.Id, f.Live.Id, false); slot.ConfigureItems(f.Service.Id, "[]"); db.ComboTemplateItems.Add(slot);
            var proposal = new ComboTemplateProposalDto("Latest private combo proposal", "Popular", "Classic", "Private combo", null, true, 0,
                [new(category.Id, f.Live.Id, false, f.Service.Id, [])], vendor.Id, "HCM", null, new(["https://cdn.example.invalid/private-combo.jpg"], "Private terms"));
            f.ComboRevision = ComboTemplateRevision.Create(f.Combo.Id, 1, JsonSerializer.Serialize(proposal, options), f.Owner.Id, "VendorOwner", true); db.ComboTemplateRevisions.Add(f.ComboRevision);
            var draft = ComboTemplate.Create("Draft combo", "Popular", "Classic", "Draft", null, 0); draft.StageUnpublished(vendor.Id, "Draft"); db.ComboTemplates.Add(draft);
            await db.SaveChangesAsync(); return f;
        }
        public async ValueTask DisposeAsync() { await Db.DisposeAsync(); if (connection != null) await connection.DisposeAsync(); }
    }
}
