using System.Reflection;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Data.SqlClient;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Wedding.Application.Common.Interfaces;
using Wedding.Application.Features.BusinessGroups;
using Wedding.Application.Features.Categories;
using Wedding.Application.Features.Categories.Queries.GetPublicCategories;
using Wedding.Application.Features.Listings;
using Wedding.Application.Features.Listings.Commands.ApproveListing;
using Wedding.Application.Features.Listings.Commands.CreateListing;
using Wedding.Application.Features.Listings.Commands.SubmitForApproval;
using Wedding.Application.Features.Listings.Commands.UpdateListing;
using Wedding.Application.Features.Listings.Queries.GetListingDetail;
using Wedding.Domain.Entities;
using Wedding.Domain.Enums;
using Wedding.Domain.Exceptions;
using Wedding.Infrastructure.Persistence;
using Wedding.WebApi.Controllers;

namespace Wedding.Tests;

public class VendorCategoryEligibilityTests
{
    [Fact]
    public async Task PublicCategoriesExposeCanonicalGroupRateAndIconWithoutShopOverrides()
    {
        await using var f = await Fixture.Create();
        var rows = await new GetPublicCategoriesQueryHandler(f.Db).Handle(new(), default);
        Assert.Equal(8, rows.Count);
        var photo = Assert.Single(rows, category => category.Id == f.Photo.Id);
        Assert.Equal(BusinessGroupPolicy.PhotoBridalMakeupId, photo.BusinessGroupId);
        Assert.Equal("Quay chụp, Váy cưới & Makeup", photo.BusinessGroupName);
        Assert.Equal(.10m, photo.DefaultCommissionRate); Assert.Equal("Camera", photo.Icon);
        Assert.Equal(.08m, f.Vendor.CommissionRate); Assert.Equal(1, photo.ListingCount);
        var planning = Assert.Single(rows, category => category.Slug == "wedding-planner");
        Assert.True(planning.RequiresCommissionBasisApproval); Assert.Equal(.12m, planning.DefaultCommissionRate);
        Assert.Equal(f.InitialAuditCount, await f.Db.AuditLogs.CountAsync());
    }

    [Theory]
    [InlineData("inactive-category")] [InlineData("inactive-group")] [InlineData("inactive-vendor")]
    [InlineData("inactive-owner")] [InlineData("owner-role-changed")]
    public async Task PublicCategoryCountsAndListingAccessShareTheSameVisibilityGuards(string condition)
    {
        await using var f = await Fixture.Create();
        await f.Disable(condition);
        var rows = await new GetPublicCategoriesQueryHandler(f.Db).Handle(new(), default);
        Assert.False(await f.Db.Listings.PubliclyVisible().AnyAsync(listing => listing.Id == f.Listing.Id));
        Assert.DoesNotContain(rows, category => category.Id == f.Photo.Id && category.ListingCount > 0);
        await Assert.ThrowsAsync<NotFoundException>(() => new GetListingDetailQueryHandler(f.Db, new Current(null, null)).Handle(new(f.Listing.Id), default));
        if (condition is "inactive-category" or "inactive-group") Assert.DoesNotContain(rows, category => category.Id == f.Photo.Id);
        else Assert.Contains(rows, category => category.Id == f.Photo.Id);
    }

    [Fact]
    public async Task OwnCategoryPickerOffersEveryActiveConfiguredCategoryWithoutChangingHistoricalMembershipOrRate()
    {
        await using var f = await Fixture.Create();
        var result = await f.My();
        Assert.False(result.RequiresMembershipReview); Assert.Null(result.Guidance);
        Assert.Equal(new[] { BusinessGroupPolicy.PhotoBridalMakeupId }, result.BusinessGroupIds);
        Assert.Equal(8, result.Categories.Count);
        Assert.All(result.Categories, category => Assert.NotNull(category.BusinessGroupId));
        Assert.Contains(result.Categories, category => category.Id == f.Venue.Id && category.DefaultCommissionRate == .03m);
        Assert.Contains(result.Categories, category => category.Id == f.Photo.Id && category.DefaultCommissionRate == .10m);
        Assert.Equal(f.Photo.Id, Assert.Single(result.ExistingCategories).Id);
        Assert.DoesNotContain(result.ExistingCategories, category => category.Id == f.Venue.Id);
        Assert.Equal(f.InitialAuditCount, await f.Db.AuditLogs.CountAsync());
        Assert.Equal(.08m, (await f.Db.Vendors.AsNoTracking().SingleAsync(vendor => vendor.Id == f.Vendor.Id)).CommissionRate);
    }

    [Theory]
    [InlineData("anonymous")] [InlineData("unknown")]
    [InlineData("Customer")] [InlineData("Moderator")] [InlineData("SuperAdmin")]
    [InlineData("Finance")] [InlineData("CustomerCare")]
    [InlineData("forged-vendor")] [InlineData("inactive-vendor")] [InlineData("inactive-owner")]
    [InlineData("owner-role-changed")]
    public async Task PrivatePickerRequiresTheActivePersistedVendorIdentity(string identity)
    {
        await using var f = await Fixture.Create();
        if (identity is "inactive-vendor" or "inactive-owner" or "owner-role-changed") await f.Disable(identity);
        var actor = identity switch
        {
            "anonymous" => new Current(null, null), "unknown" => new Current(Guid.NewGuid(), "VendorOwner"),
            "forged-vendor" => new Current(f.Users.Single(user => user.Role == UserRole.Customer).Id, "VendorOwner"),
            "inactive-vendor" or "inactive-owner" or "owner-role-changed" => f.OwnerActor,
            _ => new Current(f.Users.Single(user => user.Role.ToString() == identity).Id, identity)
        };
        await Assert.ThrowsAsync<DomainException>(() => new GetMyCategoriesQueryHandler(f.Db, actor).Handle(new(), default));
        Assert.Equal(f.InitialAuditCount, await f.Db.AuditLogs.CountAsync());
    }

    [Theory]
    [InlineData("inactive-category", 7)] [InlineData("inactive-group", 5)] [InlineData("unmapped-category", 7)]
    public async Task CategoryPickerExcludesInvalidConfigurationWithoutMembershipOrFinancialWrites(string condition, int expectedCount)
    {
        await using var f = await Fixture.Create(membership: false);
        if (condition == "unmapped-category") { f.Db.Entry(f.Photo).Property(category => category.BusinessGroupId).CurrentValue = null; await f.Db.SaveChangesAsync(); }
        else await f.Disable(condition);
        var before = await f.FinancialSnapshot();
        var picker = await f.My();
        Assert.False(picker.RequiresMembershipReview); Assert.Null(picker.Guidance);
        Assert.Equal(expectedCount, picker.Categories.Count);
        Assert.DoesNotContain(picker.Categories, category => category.Id == f.Photo.Id);
        Assert.Contains(picker.ExistingCategories, category => category.Id == f.Photo.Id);
        Assert.Empty(picker.BusinessGroupIds);
        Assert.Equal(before, await f.FinancialSnapshot());
    }

    [Theory]
    [InlineData(.08)] [InlineData(0)]
    public async Task InactiveParentCannotBeBypassedByExistingListingOrRateMode(decimal rate)
    {
        await using var f = await Fixture.Create(membership: false);
        f.Db.Entry(f.Vendor).Property(vendor => vendor.CommissionRate).CurrentValue = rate;
        await f.Disable("inactive-group");
        var before = await f.FinancialSnapshot();
        await Assert.ThrowsAsync<DomainException>(() => ListingCategoryAccess.RequireForExistingAsync(f.Db, f.Vendor.Id, f.Photo.Id, default));
        await Assert.ThrowsAsync<DomainException>(() => f.CreateListing(f.Photo.Id));
        Assert.Equal(before, await f.FinancialSnapshot());
    }

    [Fact]
    public async Task HistoricalUnmappedListingRetainsLegacyReadReviewCompatibilityButCannotCreateNewUnconfiguredServices()
    {
        await using var f = await Fixture.Create(membership: false);
        f.Db.Entry(f.Photo).Property(category => category.BusinessGroupId).CurrentValue = null;
        await f.Db.SaveChangesAsync(); var before = await f.FinancialSnapshot();
        Assert.Equal(f.Photo.Id, (await ListingCategoryAccess.RequireForExistingAsync(f.Db, f.Vendor.Id, f.Photo.Id, default)).Id);
        await Assert.ThrowsAsync<DomainException>(() => f.CreateListing(f.Photo.Id));
        Assert.Equal(before, await f.FinancialSnapshot());
        Assert.Equal(2, await f.Db.Listings.CountAsync());
    }

    [Fact]
    public async Task LegacyShopWithoutMembershipCanCreateAnyActiveCategoryAndOldListingRemainsEditableReviewable()
    {
        await using var f = await Fixture.Create(membership: false);
        var before = await f.FinancialSnapshot();
        var picker = await f.My();
        Assert.False(picker.RequiresMembershipReview); Assert.Equal(8, picker.Categories.Count);
        Assert.Null(picker.Guidance); Assert.Empty(picker.BusinessGroupIds);
        Assert.Equal(f.Photo.Id, Assert.Single(picker.ExistingCategories).Id);
        var created = await f.CreateListing(f.Venue.Id);
        Assert.Equal(f.Venue.Id, (await f.Db.Listings.SingleAsync(listing => listing.Id == created)).CategoryId);
        Assert.Equal(3, await f.Db.Listings.CountAsync());
        Assert.True(await f.Db.Listings.PubliclyVisible().AnyAsync(listing => listing.Id == f.Listing.Id));
        await new UpdateListingCommandHandler(f.Db, f.OwnerActor).Handle(new(f.Listing.Id, "Legacy revised safely", 1000, 2000, "Existing description", "HCM"), default);
        await new SubmitForApprovalCommandHandler(f.Db, f.OwnerActor).Handle(new(f.Listing.Id), default);
        var pending = await f.Db.ListingRevisions.SingleAsync(revision => revision.ListingId == f.Listing.Id && revision.Status == "Pending");
        await new ApproveListingCommandHandler(f.Db, f.AdminActor).Handle(new(f.Listing.Id, pending.Id), default);
        Assert.Equal("Legacy revised safely", (await f.Db.Listings.AsNoTracking().SingleAsync(listing => listing.Id == f.Listing.Id)).Title);
        Assert.Equal(before, await f.FinancialSnapshot()); Assert.Empty(await f.Db.VendorBusinessGroups.Where(membership => membership.VendorId == f.Vendor.Id).ToListAsync());
    }

    [Fact]
    public async Task CategoryInheritingShopWithoutMembershipCanCreateAndReviewMappedCategoriesWithoutPolicyWrites()
    {
        await using var f = await Fixture.Create(membership: false);
        f.Db.Entry(f.Vendor).Property(vendor => vendor.CommissionRate).CurrentValue = 0;
        await f.Db.SaveChangesAsync();
        var before = await f.FinancialSnapshot();
        Assert.Equal(f.Photo.Id, (await ListingCategoryAccess.RequireForExistingAsync(f.Db, f.Vendor.Id, f.Photo.Id, default)).Id);
        var created = await f.CreateListing(f.Venue.Id);
        Assert.Equal(f.Venue.Id, (await f.Db.Listings.SingleAsync(listing => listing.Id == created)).CategoryId);
        Assert.Empty(await f.Db.VendorBusinessGroups.Where(membership => membership.VendorId == f.Vendor.Id).ToListAsync());
        Assert.Equal(before, await f.FinancialSnapshot());
        Assert.Empty(await f.Db.ListingRevisions.ToListAsync());
    }

    [Fact]
    public async Task HistoricalMembershipChangesDoNotRestrictOtherCategoriesForNewOrExistingServices()
    {
        await using var f = await Fixture.Create(membership: false);
        var oldVenue = Listing.Create(f.Vendor.Id, f.Venue.Id, "Existing legacy venue", "existing-legacy-venue", 1000, 2000, "Legacy venue description", "HCM");
        oldVenue.Approve(); f.Db.Listings.Add(oldVenue); await f.Db.SaveChangesAsync();
        var before = await f.FinancialSnapshot();
        await f.Assign([BusinessGroupPolicy.PhotoBridalMakeupId], "Confirm photo group without withdrawing older venue");
        Assert.Equal(f.Venue.Id, (await ListingCategoryAccess.RequireForExistingAsync(f.Db, f.Vendor.Id, f.Venue.Id, default)).Id);
        var created = await f.CreateListing(f.Venue.Id);
        Assert.Equal(f.Venue.Id, (await f.Db.Listings.SingleAsync(listing => listing.Id == created)).CategoryId);
        await new UpdateListingCommandHandler(f.Db, f.OwnerActor).Handle(new(oldVenue.Id, "Revised legacy venue", 1000, 2000, "Legacy venue description", "HCM"), default);
        await new SubmitForApprovalCommandHandler(f.Db, f.OwnerActor).Handle(new(oldVenue.Id), default);
        var revision = await f.Db.ListingRevisions.SingleAsync(row => row.ListingId == oldVenue.Id && row.Status == "Pending");
        await new ApproveListingCommandHandler(f.Db, f.AdminActor).Handle(new(oldVenue.Id, revision.Id), default);
        Assert.True(await f.Db.Listings.PubliclyVisible().AnyAsync(row => row.Id == oldVenue.Id));
        Assert.Equal("Revised legacy venue", (await f.Db.Listings.AsNoTracking().SingleAsync(row => row.Id == oldVenue.Id)).Title);
        Assert.Equal(new[] { BusinessGroupPolicy.PhotoBridalMakeupId }, await f.Db.VendorBusinessGroups.Where(row => row.VendorId == f.Vendor.Id).Select(row => row.BusinessGroupId).ToArrayAsync());
        Assert.Equal(before, await f.FinancialSnapshot());
    }

    [Fact]
    public async Task AdminScopeAssignmentIsAddOnlyIdempotentAuditedAndDoesNotChangeAnyFinancialSnapshotOrAcknowledgement()
    {
        await using var f = await Fixture.Create(membership: false);
        var before = await f.FinancialSnapshot(); var existingMembership = await f.Db.VendorBusinessGroups.CountAsync();
        var assigned = await f.Assign([BusinessGroupPolicy.PhotoBridalMakeupId], "  Hồ sơ NCC đã xác minh phạm vi quay chụp  ");
        Assert.True(assigned.UsesLegacyCommissionRate); Assert.Equal(.08m, assigned.LegacyCommissionRate);
        Assert.Null(assigned.PolicyAcceptedAt); Assert.Null(assigned.PolicyVersion);
        Assert.Equal(BusinessGroupPolicy.PhotoBridalMakeupId, Assert.Single(assigned.Groups).Id);
        var audit = Assert.Single(await f.Db.AuditLogs.AsNoTracking().Where(log => log.Action == "Vendor.BusinessGroupsAssigned").ToListAsync());
        Assert.Equal("Vendor.BusinessGroupsAssigned", audit.Action); Assert.Equal(f.AdminActor.UserId, audit.ActorId);
        using (var payload = JsonDocument.Parse(audit.DataJson!))
            Assert.Equal("Hồ sơ NCC đã xác minh phạm vi quay chụp", payload.RootElement.GetProperty("Reason").GetString());
        await f.Assign([BusinessGroupPolicy.PhotoBridalMakeupId], "Idempotent retry");
        Assert.Equal(f.InitialAuditCount + 1, await f.Db.AuditLogs.CountAsync());
        await f.Assign([BusinessGroupPolicy.DecorInvitationsId], "Bổ sung trang trí");
        Assert.Equal(existingMembership + 2, await f.Db.VendorBusinessGroups.CountAsync());
        var read = await new GetVendorBusinessGroupsQueryHandler(f.Db, f.AdminActor).Handle(new(f.Vendor.Id), default);
        Assert.Equal(2, read.Groups.Count); Assert.Equal(f.InitialAuditCount + 2, await f.Db.AuditLogs.CountAsync());
        Assert.Equal(before, await f.FinancialSnapshot());
        var my = await f.My(); Assert.False(my.RequiresMembershipReview); Assert.Equal(8, my.Categories.Count);
        var created = await f.CreateListing(f.Photo.Id);
        Assert.Equal(f.Photo.Id, (await f.Db.Listings.AsNoTracking().SingleAsync(listing => listing.Id == created)).CategoryId);
        Assert.Equal(before, await f.FinancialSnapshot());
    }

    [Theory]
    [InlineData("unknown-category")] [InlineData("inactive-category")]
    [InlineData("inactive-group")] [InlineData("unmapped-category")]
    public async Task CreationAndSubmissionRejectInvalidCategoryConfigurationWithoutCreatingPartialData(string condition)
    {
        await using var f = await Fixture.Create();
        // A new policy still needs configured, active category metadata; no membership is required.
        f.Db.Entry(f.Vendor).Property(vendor => vendor.CommissionRate).CurrentValue = 0; await f.Db.SaveChangesAsync();
        var categoryId = f.Photo.Id;
        if (condition == "unknown-category") categoryId = Guid.NewGuid();
        else if (condition == "unmapped-category") { f.Db.Entry(f.Photo).Property(category => category.BusinessGroupId).CurrentValue = null; await f.Db.SaveChangesAsync(); }
        else await f.Disable(condition);
        var before = await f.FinancialSnapshot();
        var audits = await f.Db.AuditLogs.CountAsync();
        await Assert.ThrowsAsync<DomainException>(() => f.CreateListing(categoryId));
        Assert.Equal(audits, await f.Db.AuditLogs.CountAsync());
        if (condition != "unknown-category")
        {
            var listing = await f.Db.Listings.SingleAsync(row => row.Id == f.Listing.Id);
            f.Db.Entry(listing).Property(row => row.CategoryId).CurrentValue = categoryId;
            listing.SetStatus(ListingStatus.Draft); await f.Db.SaveChangesAsync(); audits = await f.Db.AuditLogs.CountAsync();
            await Assert.ThrowsAsync<DomainException>(() => new SubmitForApprovalCommandHandler(f.Db, f.OwnerActor).Handle(new(f.Listing.Id), default));
            Assert.Equal(audits, await f.Db.AuditLogs.CountAsync());
            listing = await f.Db.Listings.SingleAsync(row => row.Id == f.Listing.Id);
            listing.SetStatus(ListingStatus.PendingApproval); await f.Db.SaveChangesAsync(); audits = await f.Db.AuditLogs.CountAsync();
            await Assert.ThrowsAsync<DomainException>(() => new ApproveListingCommandHandler(f.Db, f.AdminActor).Handle(new(f.Listing.Id), default));
            Assert.Equal(audits, await f.Db.AuditLogs.CountAsync());
        }
        Assert.Equal(2, await f.Db.Listings.CountAsync()); Assert.Empty(await f.Db.ListingMedias.ToListAsync());
        Assert.Empty(await f.Db.ListingRevisions.ToListAsync()); Assert.Equal(audits, await f.Db.AuditLogs.CountAsync());
        Assert.Empty(await f.Db.Notifications.ToListAsync()); Assert.Equal(before, await f.FinancialSnapshot());
    }

    [Theory]
    [InlineData("anonymous")] [InlineData("unknown")] [InlineData("Moderator")]
    [InlineData("Finance")] [InlineData("CustomerCare")] [InlineData("Customer")]
    [InlineData("VendorOwner")] [InlineData("forged-admin")] [InlineData("inactive-admin")]
    public async Task OnlyActualActiveAdminCanReadOrAssignVendorGroups(string identity)
    {
        await using var f = await Fixture.Create(membership: false);
        var actor = identity switch
        {
            "anonymous" => new Current(null, null), "unknown" => new Current(Guid.NewGuid(), "SuperAdmin"),
            "forged-admin" => new Current(f.OwnerActor.UserId, "SuperAdmin"),
            "inactive-admin" => f.AdminActor,
            _ => new Current(f.Users.First(user => user.Role.ToString() == identity).Id, identity)
        };
        if (identity == "inactive-admin") { f.Users.Single(user => user.Role == UserRole.SuperAdmin).SetStatus(false); await f.Db.SaveChangesAsync(); }
        var before = await f.FinancialSnapshot();
        await Assert.ThrowsAsync<DomainException>(() => new GetVendorBusinessGroupsQueryHandler(f.Db, actor).Handle(new(f.Vendor.Id), default));
        await Assert.ThrowsAsync<DomainException>(() => new AssignVendorBusinessGroupsCommandHandler(f.Db, actor)
            .Handle(new(f.Vendor.Id, [BusinessGroupPolicy.PhotoBridalMakeupId], "Must not assign"), default));
        Assert.Empty(await f.Db.VendorBusinessGroups.Where(membership => membership.VendorId == f.Vendor.Id).ToListAsync());
        Assert.Equal(f.InitialAuditCount, await f.Db.AuditLogs.CountAsync()); Assert.Equal(before, await f.FinancialSnapshot());
    }

    [Theory]
    [InlineData("null")] [InlineData("empty")] [InlineData("duplicate")] [InlineData("empty-id")]
    [InlineData("unknown")] [InlineData("too-many")] [InlineData("reason-empty")]
    [InlineData("reason-limit")] [InlineData("inactive-group")] [InlineData("inactive-vendor")]
    public async Task InvalidAdminAssignmentsHaveNoPartialMembershipAuditOrFinancialWrites(string condition)
    {
        await using var f = await Fixture.Create(membership: false);
        List<Guid>? ids = [BusinessGroupPolicy.PhotoBridalMakeupId]; var reason = "Verified synthetic scope";
        switch (condition)
        {
            case "null": ids = null; break; case "empty": ids = []; break;
            case "duplicate": ids = [ids[0], ids[0]]; break; case "empty-id": ids = [Guid.Empty]; break;
            case "unknown": ids.Add(Guid.NewGuid()); break;
            case "too-many": ids = Enumerable.Range(0, 21).Select(_ => Guid.NewGuid()).ToList(); break;
            case "reason-empty": reason = "  "; break; case "reason-limit": reason = new string('x', 2001); break;
            default: await f.Disable(condition); break;
        }
        var before = await f.FinancialSnapshot();
        await Assert.ThrowsAsync<DomainException>(() => f.Assign(ids, reason));
        Assert.Empty(await f.Db.VendorBusinessGroups.Where(membership => membership.VendorId == f.Vendor.Id).ToListAsync());
        Assert.Equal(f.InitialAuditCount, await f.Db.AuditLogs.CountAsync()); Assert.Equal(before, await f.FinancialSnapshot());
    }

    [Fact]
    public async Task AssignmentFailureAfterSaveRollsBackMembershipAndAuditTogether()
    {
        await using var f = await Fixture.Create(membership: false); var before = await f.FinancialSnapshot();
        await using var failing = new FailAfterSaveContext(f.Options);
        await Assert.ThrowsAsync<InvalidOperationException>(() => new AssignVendorBusinessGroupsCommandHandler(failing, f.AdminActor)
            .Handle(new(f.Vendor.Id, [BusinessGroupPolicy.PhotoBridalMakeupId], "Synthetic rollback proof"), default));
        Assert.Empty(await f.Db.VendorBusinessGroups.AsNoTracking().Where(membership => membership.VendorId == f.Vendor.Id).ToListAsync());
        Assert.Equal(f.InitialAuditCount, await f.Db.AuditLogs.AsNoTracking().CountAsync()); Assert.Equal(before, await f.FinancialSnapshot());
    }

    [Fact]
    public void CategoryAndAdminEndpointsHaveExplicitRoleBoundaries()
    {
        Assert.Equal("VendorOwner", typeof(CategoriesController).GetMethod(nameof(CategoriesController.My))!.GetCustomAttribute<AuthorizeAttribute>()!.Roles);
        foreach (var method in new[] { nameof(BusinessGroupsController.VendorGroups), nameof(BusinessGroupsController.AssignGroups) })
            Assert.Equal("SuperAdmin", typeof(BusinessGroupsController).GetMethod(method)!.GetCustomAttribute<AuthorizeAttribute>()!.Roles);
    }

    [SqlServerFact]
    public async Task SqlUnrestrictedCategoryCreationWithoutMembershipAndFinancialPreservationSurviveReload()
    {
        const string prefix = "WeddingPlanTest_CategoryEligibility_";
        var database = prefix + Guid.NewGuid().ToString("N");
        Assert.Equal("1", Environment.GetEnvironmentVariable("WEDDING_TEST_SQLSERVER"));
        var builder = new SqlConnectionStringBuilder { DataSource = "localhost", InitialCatalog = database, IntegratedSecurity = true, TrustServerCertificate = true, ConnectTimeout = 5 };
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlServer(builder.ConnectionString).Options;
        await using var db = new ApplicationDbContext(options);
        var ownsDatabase = false;
        void AssertTarget(ApplicationDbContext targetContext)
        {
            Assert.StartsWith(prefix, database); Assert.True(Guid.TryParseExact(database[prefix.Length..], "N", out _));
            var target = new SqlConnectionStringBuilder(targetContext.Database.GetConnectionString());
            Assert.Equal("localhost", target.DataSource); Assert.Equal(database, target.InitialCatalog);
            Assert.True(target.IntegratedSecurity); Assert.Empty(target.AttachDBFilename);
        }
        try
        {
            AssertTarget(db);
            await using (var master = new SqlConnection(new SqlConnectionStringBuilder(builder.ConnectionString) { InitialCatalog = "master" }.ConnectionString))
            {
                await master.OpenAsync(); await using var check = master.CreateCommand();
                check.CommandText = "SELECT DB_ID(@database)"; check.Parameters.AddWithValue("@database", database);
                Assert.Equal(DBNull.Value, await check.ExecuteScalarAsync());
            }
            ownsDatabase = true;
            await db.Database.MigrateAsync();
            await using var f = await Fixture.Seed(db, options, null, membership: false);
            var before = await f.FinancialSnapshot();
            db.ChangeTracker.Clear();
            await using var reload = new ApplicationDbContext(options);
            var picker = await new GetMyCategoriesQueryHandler(reload, f.OwnerActor).Handle(new(), default);
            Assert.False(picker.RequiresMembershipReview); Assert.Null(picker.Guidance); Assert.Empty(picker.BusinessGroupIds);
            Assert.Equal(8, picker.Categories.Count);
            Assert.Contains(picker.Categories, category => category.Id == f.Venue.Id && category.DefaultCommissionRate == .03m);
            var id = await new CreateListingCommandHandler(reload, f.OwnerActor).Handle(f.Command(f.Photo.Id), default);
            Assert.Equal(f.Photo.Id, (await reload.Listings.AsNoTracking().SingleAsync(listing => listing.Id == id)).CategoryId);
            var venueId = await new CreateListingCommandHandler(reload, f.OwnerActor).Handle(f.Command(f.Venue.Id), default);
            Assert.Equal(f.Venue.Id, (await reload.Listings.AsNoTracking().SingleAsync(listing => listing.Id == venueId)).CategoryId);
            Assert.Equal(before, await f.FinancialSnapshot());
            Assert.Empty(await reload.VendorBusinessGroups.Where(membership => membership.VendorId == f.Vendor.Id).ToListAsync());
            Assert.Equal(0, await reload.AuditLogs.CountAsync(log => log.Action == "Vendor.BusinessGroupsAssigned"));
            Assert.Empty(await reload.Database.GetPendingMigrationsAsync()); Assert.False(reload.Database.HasPendingModelChanges());
        }
        finally
        {
            if (ownsDatabase)
            {
                await using var cleanup = new ApplicationDbContext(options);
                AssertTarget(cleanup);
                await cleanup.Database.EnsureDeletedAsync();
            }
        }
    }

    private sealed record Current(Guid? UserId, string? Role) : ICurrentUserService { public string? Email => null; }
    private sealed class FailAfterSaveContext(DbContextOptions<ApplicationDbContext> options) : ApplicationDbContext(options)
    { public override async Task<int> SaveChangesAsync(CancellationToken ct = default) { await base.SaveChangesAsync(ct); throw new InvalidOperationException("Synthetic rollback test."); } }
    private sealed class Fixture(ApplicationDbContext db, DbContextOptions<ApplicationDbContext> options, SqliteConnection? connection) : IAsyncDisposable
    {
        public ApplicationDbContext Db { get; } = db; public DbContextOptions<ApplicationDbContext> Options { get; } = options;
        public List<User> Users { get; } = [];
        public int InitialAuditCount { get; private set; }
        public Vendor Vendor { get; private set; } = null!; public Category Photo { get; private set; } = null!;
        public Category Venue { get; private set; } = null!; public Listing Listing { get; private set; } = null!;
        public Current OwnerActor => new(Vendor.UserId, "VendorOwner");
        public Current AdminActor => new(Users.Single(user => user.Role == UserRole.SuperAdmin).Id, "SuperAdmin");
        public Task<VendorCategoryEligibilityDto> My() => new GetMyCategoriesQueryHandler(Db, OwnerActor).Handle(new(), default);
        public Task<VendorBusinessGroupsDto> Assign(List<Guid>? ids, string? reason) => new AssignVendorBusinessGroupsCommandHandler(Db, AdminActor).Handle(new(Vendor.Id, ids, reason), default);
        public CreateListingCommand Command(Guid categoryId) => new(categoryId, "New allowed category listing", 1000, 2000, "Synthetic category scope test", "HCM", ["https://cdn.example.invalid/category.jpg"]);
        public Task<Guid> CreateListing(Guid categoryId) => new CreateListingCommandHandler(Db, OwnerActor).Handle(Command(categoryId), default);
        public async Task Disable(string condition)
        {
            switch (condition)
            {
                case "inactive-category": Db.Entry(Photo).Property(category => category.IsActive).CurrentValue = false; break;
                case "inactive-group": Db.Entry(await Db.BusinessGroups.SingleAsync(group => group.Id == Photo.BusinessGroupId)).Property(group => group.IsActive).CurrentValue = false; break;
                case "inactive-vendor": Db.Entry(Vendor).Property(vendor => vendor.IsActive).CurrentValue = false; break;
                case "inactive-owner": Users.Single(user => user.Id == Vendor.UserId).SetStatus(false); break;
                case "owner-role-changed": Db.Entry(Users.Single(user => user.Id == Vendor.UserId)).Property(user => user.Role).CurrentValue = UserRole.Customer; break;
                default: throw new InvalidOperationException("Unknown synthetic state.");
            }
            await Db.SaveChangesAsync();
        }
        public async Task<string> FinancialSnapshot()
        {
            await using var read = new ApplicationDbContext(Options);
            return JsonSerializer.Serialize(new {
                Vendors = await read.Vendors.AsNoTracking().OrderBy(vendor => vendor.Id).Select(vendor => new { vendor.Id, vendor.CommissionRate, vendor.PartnershipPolicyAcceptedAt, vendor.PartnershipPolicyVersion, vendor.UpdatedAt }).ToListAsync(),
                Contracts = await read.BookingContracts.AsNoTracking().OrderBy(contract => contract.Id).Select(contract => new { contract.Id, contract.ContractValue, contract.DepositAmount, contract.CommissionRateSnapshot, contract.ConfirmedAt, contract.Status, contract.Version }).ToListAsync(),
                Commissions = await read.Commissions.AsNoTracking().OrderBy(commission => commission.Id).Select(commission => new { commission.Id, commission.CommissionRate, commission.CommissionAmount, commission.PaidAmount, commission.Status, commission.PaidAt, commission.PaymentReferenceCode, commission.Version }).ToListAsync()
            });
        }
        public static async Task<Fixture> Create(bool membership = true)
        {
            var connection = new SqliteConnection("Data Source=:memory:"); await connection.OpenAsync();
            var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options;
            var db = new ApplicationDbContext(options); await db.Database.EnsureCreatedAsync();
            return await Seed(db, options, connection, membership);
        }
        public static async Task<Fixture> Seed(ApplicationDbContext db, DbContextOptions<ApplicationDbContext> options, SqliteConnection? connection, bool membership)
        {
            var f = new Fixture(db, options, connection);
            User Add(UserRole role, string suffix = "") { var user = User.Create($"Synthetic {role}{suffix}", $"{role}{suffix}@category.example.invalid", "0901234567", "synthetic-hash", role); db.Users.Add(user); f.Users.Add(user); return user; }
            var owner = Add(UserRole.VendorOwner); var otherOwner = Add(UserRole.VendorOwner, "other"); var customer = Add(UserRole.Customer);
            Add(UserRole.SuperAdmin); Add(UserRole.Moderator); Add(UserRole.Finance); Add(UserRole.CustomerCare);
            f.Vendor = Vendor.Create(owner.Id, "Synthetic category shop", "synthetic-category-shop", "Owner", "0901234567", "HCM", .08m); f.Vendor.SetVerified(true);
            var other = Vendor.Create(otherOwner.Id, "Other category shop", "other-category-shop", "Other", "0901234567", "HCM", .03m); other.SetVerified(true);
            db.Vendors.AddRange(f.Vendor, other);
            if (membership) db.VendorBusinessGroups.Add(VendorBusinessGroup.Create(f.Vendor.Id, BusinessGroupPolicy.PhotoBridalMakeupId));
            db.VendorBusinessGroups.Add(VendorBusinessGroup.Create(other.Id, BusinessGroupPolicy.VenueBanquetId));
            var tax = VendorTaxProfile.Create(f.Vendor.Id); tax.Update("Synthetic business", "1234567890", "VCB", "123456789", "Synthetic holder", true); tax.Submit(); tax.Review(true); db.VendorTaxProfiles.Add(tax);
            f.Photo = await db.Categories.SingleAsync(category => category.Slug == "quay-chup-photo-video"); f.Venue = await db.Categories.SingleAsync(category => category.Slug == "tiem-cuoi-venues");
            f.Listing = Listing.Create(f.Vendor.Id, f.Photo.Id, "Existing legacy photo listing", "legacy-photo-category", 1000, 2000, "Existing description", "HCM"); f.Listing.Approve();
            var otherListing = Listing.Create(other.Id, f.Venue.Id, "Other private venue", "other-private-venue", 1000, 2000, "Other description", "HCM");
            db.Listings.AddRange(f.Listing, otherListing);
            var lead = Lead.Create(customer.Id, f.Vendor.Id, f.Listing.Id, "0901234567", DateTime.UtcNow.AddDays(30), 100, 1000000, null); db.Leads.Add(lead);
            var contract = BookingContract.Create(lead.Id, null, f.Vendor.Id, customer.Id, 1000000, 100000, null, lead.WeddingDate); contract.ConfirmByCustomer(); contract.SnapshotCommissionRate(.08m); db.BookingContracts.Add(contract);
            var commission = Commission.Create(contract.Id, f.Vendor.Id, CommissionPeriod.Period1_Deposit, .08m, 40000, DateTime.UtcNow.AddDays(10)); commission.MarkPaid("SYNTHETIC-CATEGORY-PAID"); db.Commissions.Add(commission);
            await db.SaveChangesAsync(); f.InitialAuditCount = await db.AuditLogs.CountAsync(); return f;
        }
        public async ValueTask DisposeAsync() { await Db.DisposeAsync(); if (connection != null) await connection.DisposeAsync(); }
    }
}
