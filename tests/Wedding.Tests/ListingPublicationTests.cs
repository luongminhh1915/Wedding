using System.Reflection;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Wedding.Application.Common.Interfaces;
using Wedding.Application.Features.Listings;
using Wedding.Application.Features.Listings.Commands.ApproveListing;
using Wedding.Application.Features.Listings.Commands.RejectListing;
using Wedding.Application.Features.Listings.Commands.SubmitForApproval;
using Wedding.Application.Features.Listings.Commands.ToggleListingStatus;
using Wedding.Application.Features.Listings.Commands.UpdateListing;
using Wedding.Application.Features.Listings.DTOs;
using Wedding.Application.Features.Listings.Queries.GetListingDetail;
using Wedding.Application.Features.Listings.Queries.GetPendingListings;
using Wedding.Domain.Entities;
using Wedding.Domain.Enums;
using Wedding.Domain.Exceptions;
using Wedding.Infrastructure.Persistence;
using Wedding.WebApi.Controllers;

namespace Wedding.Tests;

public class ListingPublicationTests
{
    [Fact]
    public async Task StudioAlbumsAndLatestUpdateStayPrivateUntilApprovedAndRetainFinancialSnapshots()
    {
        await using var f = await Fixture.Create();
        var original = await f.LiveSnapshot(); var financial = await f.FinancialSnapshot();
        var content = new ServiceDetailsDto(Collections: [new(Guid.NewGuid().ToString(), "New approved gallery", [NewImage])],
            LatestUpdate: new("New wedding season", NewImage));
        await f.Edit("Original title", details: content);
        Assert.Equal(original, await f.LiveSnapshot());
        Assert.Empty((await f.Public()).ServiceDetails!.Collections!);
        var reopened = (await f.Editor()).ServiceDetails!;
        Assert.Equal("New approved gallery", Assert.Single(reopened.Collections!).Name);
        Assert.Equal("New wedding season", reopened.LatestUpdate?.Content);
        await f.Submit();
        Assert.Empty((await f.Public()).ServiceDetails!.Collections!);
        await f.Approve();
        var published = (await f.Public()).ServiceDetails!;
        Assert.Equal("New approved gallery", Assert.Single(published.Collections!).Name);
        Assert.Equal(NewImage, Assert.Single(published.Collections!).Images.Single());
        Assert.Equal("New wedding season", published.LatestUpdate?.Content);
        Assert.Equal(financial, await f.FinancialSnapshot());
        Assert.Equal(Cancellation, (await f.Public()).CancellationPolicy);
    }

    [Fact]
    public async Task PublishedEditsCaptureBaselineWithoutChangingTheLiveListingUntilApproved()
    {
        await using var f = await Fixture.Create();
        var original = await f.LiveSnapshot(); var financial = await f.FinancialSnapshot();
        await f.Edit("Proposed title", [NewImage], new(IncludedItems: "New approved scope"));
        Assert.Equal(original, await f.LiveSnapshot());
        var revisions = await f.Db.ListingRevisions.AsNoTracking().OrderBy(item => item.Sequence).ToListAsync();
        Assert.Equal(new[] { "Baseline", "Draft" }, revisions.Select(item => item.Status));
        Assert.Equal(new[] { 1, 2 }, revisions.Select(item => item.Sequence));
        Assert.Null(revisions[0].ReviewedAt); Assert.Null(revisions[0].ReviewedById);
        Assert.Equal("Original title", ListingPublication.Read(revisions[0]).Title);
        Assert.Equal("Proposed title", (await f.Editor()).Title);
        Assert.Equal("Original title", (await f.Public()).Title);
        Assert.Null((await f.Public()).LatestRevision); Assert.Null((await f.Public()).RevisionHistory);
        await f.Submit(); Assert.Equal(original, await f.LiveSnapshot());
        var pending = Assert.Single(await new GetPendingListingsQueryHandler(f.Db, f.Staff).Handle(new(), default));
        Assert.Equal("Proposed title", pending.Title); Assert.Equal("Pending", pending.LatestRevision?.Status);
        var pendingId = pending.LatestRevision!.Id;
        await f.Approve(pendingId);
        var published = await f.Public();
        Assert.Equal("Proposed title", published.Title); Assert.Equal("New approved scope", published.ServiceDetails?.IncludedItems);
        Assert.Equal(NewImage, Assert.Single(published.Media).MediaUrl);
        Assert.Equal(Cancellation, published.CancellationPolicy); Assert.Equal(financial, await f.FinancialSnapshot());
        var history = (await f.Editor()).RevisionHistory!;
        var approved = Assert.Single(history, item => item.Status == "Approved");
        Assert.Equal(f.Owner.Id, approved.CreatedById); Assert.Equal(f.Moderator.Id, approved.ReviewedById);
        Assert.True(await f.Db.Notifications.AnyAsync(item => item.UserId == f.Owner.Id && item.Message.Contains("phê duyệt")));
    }

    [Fact]
    public async Task OmittedMediaAndMetadataPreserveTheLatestProposalRatherThanReplacingItWithPublishedValues()
    {
        await using var f = await Fixture.Create();
        var original = await f.LiveSnapshot();
        await f.Edit("First proposal", [NewImage], new(ShortDescription: "Proposal metadata"));
        await f.Edit("Second proposal");
        var latest = await f.Editor();
        Assert.Equal(NewImage, Assert.Single(latest.Media).MediaUrl);
        Assert.Equal("Proposal metadata", latest.ServiceDetails?.ShortDescription);
        Assert.Equal(original, await f.LiveSnapshot());
        await f.Submit(); await f.Approve();
        var published = await f.Public();
        Assert.Equal("Second proposal", published.Title);
        Assert.Equal(NewImage, Assert.Single(published.Media).MediaUrl);
        Assert.Equal("Proposal metadata", published.ServiceDetails?.ShortDescription);
        Assert.Equal(Cancellation, published.CancellationPolicy);
    }

    [Fact]
    public async Task RejectRequiresReasonAndResubmitRetainsHistoryWithoutReplacingTheLiveVersion()
    {
        await using var f = await Fixture.Create();
        await f.Edit("Rejected proposal"); await f.Submit();
        var pending = (await f.Editor()).LatestRevision!;
        var state = await f.FullSnapshot();
        foreach (var reason in new[] { "", "  ", new string('x', 1001) })
        {
            await Assert.ThrowsAsync<DomainException>(() => f.Reject(reason, pending.Id));
            Assert.Equal(state, await f.FullSnapshot());
        }
        await f.Reject("  Clarify included items  ", pending.Id);
        Assert.Equal("Original title", (await f.Public()).Title);
        var rejected = await f.Db.ListingRevisions.AsNoTracking().SingleAsync(item => item.Id == pending.Id);
        Assert.Equal("Rejected", rejected.Status); Assert.Equal("Clarify included items", rejected.ReviewNote);
        var immutableContent = rejected.ContentJson;
        await f.Submit();
        var history = (await f.Editor()).RevisionHistory!;
        Assert.Contains(history, item => item.Id == rejected.Id && item.ReviewNote == "Clarify included items");
        var latest = history.OrderByDescending(item => item.Sequence).First(); Assert.Equal("Pending", latest.Status);
        Assert.Equal(immutableContent, (await f.Db.ListingRevisions.AsNoTracking().SingleAsync(item => item.Id == latest.Id)).ContentJson);
        await f.Approve(latest.Id); Assert.Equal("Rejected proposal", (await f.Public()).Title);
        var editor = await f.Editor();
        Assert.Equal("Rejected proposal", editor.Title); Assert.Equal("Approved", editor.LatestRevision?.Status);
        Assert.NotEqual(rejected.Id, editor.LatestRevision!.Id);
    }

    [Fact]
    public async Task UnpublishedRejectedEditsReopenTheNewBaseInsteadOfTheOldRejectedSnapshot()
    {
        await using var f = await Fixture.Create(ListingStatus.Draft);
        await f.Submit(); await f.Reject("More detail needed");
        await f.Edit("Corrected unpublished title", [NewImage], new(Notes: "Corrected notes"));
        var reopened = await f.Editor();
        Assert.Equal("Corrected unpublished title", reopened.Title);
        Assert.Equal(NewImage, Assert.Single(reopened.Media).MediaUrl); Assert.Equal("Corrected notes", reopened.ServiceDetails?.Notes);
        var rejectedId = reopened.RevisionHistory!.Single(item => item.Status == "Rejected").Id;
        Assert.Equal("Original title", (await f.Editor(rejectedId)).Title);
        await f.Submit(); await f.Approve();
        Assert.Equal("Corrected unpublished title", (await f.Public()).Title);
        Assert.Equal(Cancellation, (await f.Public()).CancellationPolicy);
    }

    [Fact]
    public async Task StaleApprovalAndRejectionCannotReviewANewerSubmittedProposal()
    {
        await using var f = await Fixture.Create();
        await f.Edit("First pending"); await f.Submit(); var staleId = (await f.Editor()).LatestRevision!.Id;
        await f.Edit("Replacement pending"); await f.Submit(); var newId = (await f.Editor()).LatestRevision!.Id;
        Assert.NotEqual(staleId, newId); var before = await f.FullSnapshot();
        await Assert.ThrowsAsync<DomainException>(() => f.Approve(staleId));
        await Assert.ThrowsAsync<DomainException>(() => f.Reject("Stale review", staleId));
        Assert.Equal(before, await f.FullSnapshot());
        await f.Approve(newId); Assert.Equal("Replacement pending", (await f.Public()).Title);
        Assert.Equal("Superseded", (await f.Db.ListingRevisions.AsNoTracking().SingleAsync(item => item.Id == staleId)).Status);
    }

    [Theory]
    [InlineData(ListingStatus.Active, false)]
    [InlineData(ListingStatus.Hidden, true)]
    [InlineData(ListingStatus.Hidden, false)]
    public async Task ApprovalPreservesIndependentVendorActivityAndModeratorHiddenState(ListingStatus status, bool active)
    {
        await using var f = await Fixture.Create(status);
        await new SetListingActivityCommandHandler(f.Db, f.OwnerActor).Handle(new(f.Listing.Id, active), default);
        await f.Edit("Approved while unavailable"); await f.Submit(); await f.Approve();
        var stored = await f.Db.Listings.AsNoTracking().SingleAsync(item => item.Id == f.Listing.Id);
        Assert.Equal(status, stored.Status); Assert.Equal(active, stored.IsActive);
        await Assert.ThrowsAsync<NotFoundException>(() => f.Public());
        if (status == ListingStatus.Hidden)
        {
            await new SetListingActivityCommandHandler(f.Db, f.OwnerActor).Handle(new(f.Listing.Id, true), default);
            await Assert.ThrowsAsync<NotFoundException>(() => f.Public());
            await new ToggleListingStatusCommandHandler(f.Db, f.Staff).Handle(new(f.Listing.Id, ListingStatus.Active), default);
            Assert.Equal("Approved while unavailable", (await f.Public()).Title);
        }
        else
        {
            await new SetListingActivityCommandHandler(f.Db, f.OwnerActor).Handle(new(f.Listing.Id, true), default);
            Assert.Equal("Approved while unavailable", (await f.Public()).Title);
        }
    }

    [Fact]
    public async Task DraftAndEditorRoutesNeverExposeOtherShopsPrivateContentOrRevisionIds()
    {
        await using var f = await Fixture.Create(ListingStatus.Draft);
        await f.Submit(); var revisionId = (await f.Editor()).LatestRevision!.Id;
        foreach (var actor in new[] { new Current(null, null), f.CustomerActor, f.OtherActor })
        {
            await Assert.ThrowsAsync<NotFoundException>(() => new GetListingDetailQueryHandler(f.Db, actor).Handle(new(f.Listing.Id), default));
            if (actor.Role == "VendorOwner")
                await Assert.ThrowsAsync<NotFoundException>(() => new GetListingEditorQueryHandler(f.Db, actor).Handle(new(f.Listing.Id, revisionId), default));
            else
                await Assert.ThrowsAsync<DomainException>(() => new GetListingEditorQueryHandler(f.Db, actor).Handle(new(f.Listing.Id, revisionId), default));
        }
        Assert.Equal("Original title", (await new GetListingEditorQueryHandler(f.Db, f.Staff).Handle(new(f.Listing.Id, revisionId), default)).Title);
        await Assert.ThrowsAsync<NotFoundException>(() => f.Editor(Guid.NewGuid()));
        var before = await f.FullSnapshot();
        await Assert.ThrowsAsync<NotFoundException>(() => new UpdateListingCommandHandler(f.Db, f.OtherActor).Handle(f.Update("Intruder"), default));
        await Assert.ThrowsAsync<NotFoundException>(() => new SetListingActivityCommandHandler(f.Db, f.OtherActor).Handle(new(f.Listing.Id, false), default));
        Assert.Equal(before, await f.FullSnapshot());
    }

    [Theory]
    [InlineData("Customer")]
    [InlineData("VendorOwner")]
    [InlineData("Finance")]
    [InlineData("CustomerCare")]
    [InlineData(null)]
    public async Task ControllerAndDirectStaffCommandsDenyNonModerationActors(string? role)
    {
        await using var f = await Fixture.Create(ListingStatus.Draft); await f.Submit();
        var actor = role == "VendorOwner" ? f.OwnerActor : new Current(f.Customer.Id, role);
        var before = await f.FullSnapshot();
        await Assert.ThrowsAsync<DomainException>(() => new ApproveListingCommandHandler(f.Db, actor).Handle(new(f.Listing.Id), default));
        await Assert.ThrowsAsync<DomainException>(() => new RejectListingCommandHandler(f.Db, actor).Handle(new(f.Listing.Id, "Denied"), default));
        await Assert.ThrowsAsync<DomainException>(() => new GetPendingListingsQueryHandler(f.Db, actor).Handle(new(), default));
        await Assert.ThrowsAsync<DomainException>(() => new ToggleListingStatusCommandHandler(f.Db, actor).Handle(new(f.Listing.Id, ListingStatus.Hidden), default));
        Assert.Equal(before, await f.FullSnapshot());
        foreach (var method in new[] { nameof(ListingsController.ApproveListing), nameof(ListingsController.RejectListing), nameof(ListingsController.GetPendingListings), nameof(ListingsController.UpdateListingStatus) })
            Assert.False(await ControllerAllows(method, role));
    }

    [Theory]
    [InlineData("Moderator")]
    [InlineData("SuperAdmin")]
    public async Task OnlyActivePersistedModerationRoleMayReviewEvenWhenClaimsSayStaff(string role)
    {
        await using var f = await Fixture.Create(ListingStatus.Draft); await f.Submit();
        var forged = new Current(f.Customer.Id, role); var before = await f.FullSnapshot();
        await Assert.ThrowsAsync<DomainException>(() => new ApproveListingCommandHandler(f.Db, forged).Handle(new(f.Listing.Id), default));
        Assert.Equal(before, await f.FullSnapshot()); Assert.True(await ControllerAllows(nameof(ListingsController.ApproveListing), role));
        var staffId = role == "Moderator" ? f.Moderator.Id : f.Admin.Id;
        var staff = await f.Db.Users.SingleAsync(item => item.Id == staffId); staff.SetStatus(false); await f.Db.SaveChangesAsync();
        await Assert.ThrowsAsync<DomainException>(() => new ApproveListingCommandHandler(f.Db, new Current(staff.Id, role)).Handle(new(f.Listing.Id), default));
    }

    [Theory]
    [InlineData("not-verified")]
    [InlineData("tax-suspended")]
    [InlineData("owner-disabled")]
    [InlineData("category-disabled")]
    public async Task SubmissionAndApprovalRecheckRealVerificationAndActiveState(string condition)
    {
        await using var f = await Fixture.Create();
        await f.Edit("Needs verification"); await f.Submit();
        switch (condition)
        {
            case "not-verified": f.Vendor.SetVerified(false); break;
            case "tax-suspended": (await f.Db.VendorTaxProfiles.SingleAsync(item => item.VendorId == f.Vendor.Id)).Suspend(); break;
            case "owner-disabled": f.Owner.SetStatus(false); break;
            default: f.Db.Entry(f.Category).Property(item => item.IsActive).CurrentValue = false; break;
        }
        await f.Db.SaveChangesAsync(); var before = await f.FullSnapshot();
        await Assert.ThrowsAsync<DomainException>(() => f.Approve()); Assert.Equal(before, await f.FullSnapshot());
    }

    [Fact]
    public async Task ContentAndAuditHistoryCannotBeRewrittenOrDeletedThroughAsyncPersistence()
    {
        await using var f = await Fixture.Create(); await f.Edit("Immutable proposal");
        var revisionId = (await f.Editor()).LatestRevision!.Id;
        foreach (var mutation in new[] { "content", "author", "delete", "audit" })
        {
            await using var isolated = f.Open();
            var revision = await isolated.ListingRevisions.SingleAsync(item => item.Id == revisionId);
            switch (mutation)
            {
                case "content": isolated.Entry(revision).Property(item => item.ContentJson).CurrentValue = "{}"; break;
                case "author": isolated.Entry(revision).Property(item => item.CreatedById).CurrentValue = f.Customer.Id; break;
                case "delete": isolated.ListingRevisions.Remove(revision); break;
                default: var audit = await isolated.AuditLogs.FirstAsync(); isolated.Entry(audit).Property(item => item.Action).CurrentValue = "Tampered"; break;
            }
            await Assert.ThrowsAsync<InvalidOperationException>(() => isolated.SaveChangesAsync());
        }
        Assert.Equal("Immutable proposal", (await f.Editor()).Title);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("Customer")]
    [InlineData("Moderator")]
    [InlineData("SuperAdmin")]
    public async Task ActivityAndPrivateEditorNeverTrustAnOwnerIdPairedWithTheWrongRole(string? role)
    {
        await using var f = await Fixture.Create();
        var actor = new Current(f.Owner.Id, role); var before = await f.FullSnapshot();
        await Assert.ThrowsAsync<DomainException>(() => new SetListingActivityCommandHandler(f.Db, actor).Handle(new(f.Listing.Id, false), default));
        await Assert.ThrowsAsync<DomainException>(() => new GetListingEditorQueryHandler(f.Db, actor).Handle(new(f.Listing.Id), default));
        Assert.Equal(before, await f.FullSnapshot());
    }

    [Fact]
    public async Task FailureAfterSaveRollsBackProposalAndPublicationTogetherWithMediaMetadataAuditAndNotifications()
    {
        await using var f = await Fixture.Create(); var before = await f.FullSnapshot();
        await using (var failure = new FailAfterSaveContext(f.Options))
            await Assert.ThrowsAsync<InvalidOperationException>(() => new UpdateListingCommandHandler(failure, f.OwnerActor).Handle(f.Update("Never committed"), default));
        Assert.Equal(before, await f.FullSnapshot());
        await f.Edit("Pending atomic title", [NewImage], new(Notes: "Pending metadata")); await f.Submit();
        var pendingId = (await f.Editor()).LatestRevision!.Id; before = await f.FullSnapshot();
        await using (var failure = new FailAfterSaveContext(f.Options))
            await Assert.ThrowsAsync<InvalidOperationException>(() => new ApproveListingCommandHandler(failure, f.Staff).Handle(new(f.Listing.Id, pendingId), default));
        Assert.Equal(before, await f.FullSnapshot()); Assert.Equal("Original title", (await f.Public()).Title);
    }

    private static async Task<bool> ControllerAllows(string methodName, string? role)
    {
        using var services = new ServiceCollection().AddLogging().AddAuthorizationCore().BuildServiceProvider();
        var principal = role == null ? new ClaimsPrincipal(new ClaimsIdentity()) : new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Role, role)], "Test"));
        var method = typeof(ListingsController).GetMethod(methodName)!;
        var data = typeof(ListingsController).GetCustomAttributes<AuthorizeAttribute>().Concat(method.GetCustomAttributes<AuthorizeAttribute>()).Cast<IAuthorizeData>();
        var policy = await AuthorizationPolicy.CombineAsync(services.GetRequiredService<IAuthorizationPolicyProvider>(), data);
        return (await services.GetRequiredService<IAuthorizationService>().AuthorizeAsync(principal, null, policy!)).Succeeded;
    }

    private const string Cancellation = "Original confirmed cancellation policy remains unchanged.";
    private const string OriginalImage = "https://images.example.invalid/original.jpg";
    private const string NewImage = "https://images.example.invalid/new.jpg";
    private sealed record Current(Guid? UserId, string? Role) : ICurrentUserService { public string? Email => null; }
    private sealed class FailAfterSaveContext(DbContextOptions<ApplicationDbContext> options) : ApplicationDbContext(options)
    {
        public override async Task<int> SaveChangesAsync(CancellationToken ct = default)
        { await base.SaveChangesAsync(ct); throw new InvalidOperationException("Synthetic failure before commit."); }
    }

    private sealed class Fixture(SqliteConnection connection, ApplicationDbContext db) : IAsyncDisposable
    {
        public ApplicationDbContext Db { get; } = db;
        public User Owner { get; private set; } = null!;
        public User OtherOwner { get; private set; } = null!;
        public User Customer { get; private set; } = null!;
        public User Moderator { get; private set; } = null!;
        public User Admin { get; private set; } = null!;
        public Vendor Vendor { get; private set; } = null!;
        public Category Category { get; private set; } = null!;
        public Listing Listing { get; private set; } = null!;
        public Current OwnerActor => new(Owner.Id, "VendorOwner");
        public Current OtherActor => new(OtherOwner.Id, "VendorOwner");
        public Current CustomerActor => new(Customer.Id, "Customer");
        public Current Staff => new(Moderator.Id, "Moderator");
        public DbContextOptions<ApplicationDbContext> Options => new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options;
        public ApplicationDbContext Open() => new(Options);
        public UpdateListingCommand Update(string title, List<string>? images = null, ServiceDetailsDto? details = null)
            => new(Listing.Id, title, 1200, 2400, "Proposed description", "Hà Nội", images, details);
        public Task Edit(string title, List<string>? images = null, ServiceDetailsDto? details = null)
            => new UpdateListingCommandHandler(Db, OwnerActor).Handle(Update(title, images, details), default);
        public Task Submit() => new SubmitForApprovalCommandHandler(Db, OwnerActor).Handle(new(Listing.Id), default);
        public Task Approve(Guid? revision = null) => new ApproveListingCommandHandler(Db, Staff).Handle(new(Listing.Id, revision), default);
        public Task Reject(string reason, Guid? revision = null) => new RejectListingCommandHandler(Db, Staff).Handle(new(Listing.Id, reason, revision), default);
        public Task<ListingDto> Editor(Guid? revision = null) => new GetListingEditorQueryHandler(Db, OwnerActor).Handle(new(Listing.Id, revision), default);
        public Task<ListingDto> Public() => new GetListingDetailQueryHandler(Db, new Current(null, null)).Handle(new(Listing.Id), default);

        public async Task<string> LiveSnapshot()
        {
            await using var read = Open();
            var listing = await read.Listings.AsNoTracking().SingleAsync(item => item.Id == Listing.Id);
            var media = await read.ListingMedias.AsNoTracking().Where(item => item.ListingId == Listing.Id).OrderBy(item => item.DisplayOrder).ThenBy(item => item.Id).ToListAsync();
            return JsonSerializer.Serialize(new { listing.Id, listing.Title, listing.MinPrice, listing.MaxPrice, listing.Description, listing.Location,
                listing.Status, listing.IsActive, listing.ServiceDetailsJson, listing.CancellationPolicy, listing.UpdatedAt, listing.ModeratedAt,
                Media = media.Select(item => new { item.Id, item.MediaUrl, item.DisplayOrder, item.IsFeatured }) });
        }
        public async Task<string> FinancialSnapshot()
        {
            await using var read = Open();
            return JsonSerializer.Serialize(new {
                Contracts = await read.BookingContracts.AsNoTracking().OrderBy(item => item.Id).Select(item => new { item.Id, item.ContractValue, item.DepositAmount,
                    item.CommissionRateSnapshot, item.CancellationPolicySnapshot, item.Status, item.ConfirmedAt, item.Version }).ToListAsync(),
                Commissions = await read.Commissions.AsNoTracking().OrderBy(item => item.Id).Select(item => new { item.Id, item.CommissionAmount, item.CommissionRate,
                    item.PaidAmount, item.Status, item.PaymentReferenceCode, item.Version }).ToListAsync(),
                Orders = await read.ComboOrders.AsNoTracking().OrderBy(item => item.Id).Select(item => new { item.Id, item.Subtotal, item.Total, item.DiscountAmount, item.Status }).ToListAsync(),
                OrderItems = await read.ComboOrderItems.AsNoTracking().OrderBy(item => item.Id).Select(item => new { item.Id, item.ListingId, item.VendorId,
                    item.Title, item.Price, item.DiscountShare, item.SelectionJson, item.CancellationPolicySnapshot, item.Status, item.Quantity, item.Unit }).ToListAsync()
            });
        }
        public async Task<string> FullSnapshot()
        {
            await using var read = Open();
            var revisions = await read.ListingRevisions.AsNoTracking().OrderBy(item => item.Sequence).ToListAsync();
            return JsonSerializer.Serialize(new { Live = await LiveSnapshot(), Financial = await FinancialSnapshot(),
                Revisions = revisions.Select(item => new { item.Id, item.Sequence, item.ContentJson, item.Status, item.CreatedById, item.SubmittedAt, item.ReviewedById, item.ReviewedAt, item.ReviewNote }),
                AuditCount = await read.AuditLogs.CountAsync(), Notifications = await read.Notifications.CountAsync() });
        }
        public static async Task<Fixture> Create(ListingStatus status = ListingStatus.Active)
        {
            var connection = new SqliteConnection("Data Source=:memory:"); await connection.OpenAsync();
            var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options);
            await db.Database.EnsureCreatedAsync(); var f = new Fixture(connection, db);
            User Add(string label, UserRole role)
            { var user = User.Create(label, $"{label}@publication.example.invalid", "0901234567", "synthetic-hash", role); db.Users.Add(user); return user; }
            f.Owner = Add("owner", UserRole.VendorOwner); f.OtherOwner = Add("other", UserRole.VendorOwner);
            f.Customer = Add("customer", UserRole.Customer); f.Moderator = Add("moderator", UserRole.Moderator); f.Admin = Add("admin", UserRole.SuperAdmin);
            f.Vendor = Vendor.Create(f.Owner.Id, "Publication shop", "publication-shop", "Owner", "0901234567", "Hà Nội", .08m);
            f.Vendor.SetVerified(true); db.Vendors.Add(f.Vendor);
            db.Vendors.Add(Vendor.Create(f.OtherOwner.Id, "Other shop", "other-publication-shop", "Other", "0901234568", "Hà Nội", .08m));
            var profile = VendorTaxProfile.Create(f.Vendor.Id);
            profile.Update("Synthetic registered business", "1234567890", "VCB", "123456789", "Synthetic account holder", true); profile.Submit(); profile.Review(true);
            db.VendorTaxProfiles.Add(profile);
            f.Category = await db.Categories.SingleAsync(item => item.Slug == "quay-chup-photo-video");
            f.Listing = Listing.Create(f.Vendor.Id, f.Category.Id, "Original title", "publication-original", 1000, 2000, "Original description", "Hà Nội");
            f.Listing.SetStatus(status); f.Listing.SetCancellationPolicy(Cancellation);
            f.Listing.SetServiceDetailsJson(ListingServiceDetails.NormalizeAndSerialize(new(ShortDescription: "Original metadata")));
            db.Listings.Add(f.Listing); db.ListingMedias.Add(ListingMedia.Create(f.Listing.Id, OriginalImage, null, isFeatured: true));
            var lead = Lead.Create(f.Customer.Id, f.Vendor.Id, f.Listing.Id, f.Customer.PhoneNumber, DateTime.UtcNow.AddDays(30), 100, 10000000, "Synthetic confirmed transaction");
            db.Leads.Add(lead);
            var contract = BookingContract.Create(lead.Id, null, f.Vendor.Id, f.Customer.Id, 10000000, 3000000, null, DateTime.UtcNow.AddDays(30));
            contract.SetCancellationPolicy(Cancellation); contract.SnapshotCommissionRate(.08m); contract.ConfirmByCustomer(); db.BookingContracts.Add(contract);
            var commission = Commission.Create(contract.Id, f.Vendor.Id, CommissionPeriod.Period1_Deposit, .08m, 400000, DateTime.UtcNow.AddDays(10));
            commission.ApplyPayment(100000, "synthetic-partial-payment"); db.Commissions.Add(commission);
            db.ComboOrders.Add(ComboOrder.Create(f.Customer.Id, null, DateTime.UtcNow.AddDays(30), 100, "Original order snapshot",
                new[] { (f.Listing.Id, f.Vendor.Id, f.Category.Id, "Original order line", 1000m) }));
            await db.SaveChangesAsync(); return f;
        }
        public async ValueTask DisposeAsync() { await Db.DisposeAsync(); await connection.DisposeAsync(); }
    }
}
