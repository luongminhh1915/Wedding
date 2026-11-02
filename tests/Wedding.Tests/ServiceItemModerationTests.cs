using System.Text.Json;
using Microsoft.Data.SqlClient;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Wedding.Application.Common.Interfaces;
using Wedding.Application.Features.Combos;
using Wedding.Domain.Entities;
using Wedding.Domain.Enums;
using Wedding.Domain.Exceptions;
using Wedding.Infrastructure.Persistence;
using Wedding.Infrastructure.Services;

namespace Wedding.Tests;

public class ServiceItemModerationTests
{
    [Theory]
    [InlineData("anonymous")] [InlineData("empty-id")] [InlineData("unknown-id")]
    [InlineData("Customer")] [InlineData("Finance")] [InlineData("CustomerCare")] [InlineData("VendorOwner")]
    [InlineData("customer-as-moderator")] [InlineData("vendor-as-admin")] [InlineData("staff-role-mismatch")]
    [InlineData("inactive-staff")]
    public async Task ModerationReadAndReviewRequireAnActiveMatchingPersistedStaffIdentity(string identity)
    {
        await using var f = await Fixture.Create();
        var actor = identity switch
        {
            "anonymous" => new Current(null, null), "empty-id" => new Current(Guid.Empty, "Moderator"),
            "unknown-id" => new Current(Guid.NewGuid(), "SuperAdmin"),
            "customer-as-moderator" => new Current(f.Customer.Id, "Moderator"),
            "vendor-as-admin" => new Current(f.Owner.Id, "SuperAdmin"),
            "staff-role-mismatch" => new Current(f.Moderator.Id, "SuperAdmin"),
            "inactive-staff" => f.ModeratorActor,
            "VendorOwner" => f.VendorActor,
            _ => new Current(f.Users.Single(item => item.Role.ToString() == identity).Id, identity)
        };
        if (identity == "inactive-staff") (await f.Db.Users.SingleAsync(item => item.Id == f.Moderator.Id)).SetStatus(false);
        await f.Db.SaveChangesAsync(); var before = await f.Snapshot(); var version = await f.Version();
        await Assert.ThrowsAsync<DomainException>(() => f.List(true, actor));
        await Assert.ThrowsAsync<DomainException>(() => f.ReviewWithVersion(true, version, actor: actor));
        await Assert.ThrowsAsync<DomainException>(() => f.ReviewWithVersion(false, version, "Unauthorized rejection", actor));
        Assert.Equal(before, await f.Snapshot());
    }

    [Fact]
    public async Task VendorOwnReadIsScopedAndForeignItemOrListingCannotBeUsedToWriteAnotherShopsService()
    {
        await using var f = await Fixture.Create();
        var own = Assert.Single(await f.List(false, f.VendorActor)); Assert.Equal(f.Item.Id, own.Id);
        Assert.Equal(f.Vendor.Id, own.VendorId); Assert.NotNull(own.ReviewVersion);
        var foreign = Assert.Single(await f.List(false, f.OtherActor)); Assert.Equal(f.OtherItem.Id, foreign.Id);
        var before = await f.Snapshot();
        await Assert.ThrowsAsync<NotFoundException>(() => f.Save(f.Command with { Id = f.OtherItem.Id }));
        await Assert.ThrowsAsync<DomainException>(() => f.Save(f.Command with { ListingId = f.OtherListing.Id }));
        Assert.Equal(before, await f.Snapshot());
        Assert.Equal(2, (await f.List(true, f.ModeratorActor)).Count);
        Assert.Equal(2, (await f.List(true, f.AdminActor)).Count);
    }

    [Theory]
    [InlineData("anonymous")] [InlineData("unknown-id")] [InlineData("unmapped-vendor")]
    [InlineData("Customer")] [InlineData("Finance")] [InlineData("CustomerCare")] [InlineData("Moderator")] [InlineData("SuperAdmin")]
    [InlineData("customer-as-vendor")] [InlineData("inactive-owner")] [InlineData("inactive-shop")] [InlineData("owner-role-changed")]
    public async Task OwnReadAndSaveFailClosedForMissingRoleMismatchAndDisabledVendorIdentity(string identity)
    {
        await using var f = await Fixture.Create();
        var actor = identity switch
        {
            "anonymous" => new Current(null, null), "unknown-id" => new Current(Guid.NewGuid(), "VendorOwner"),
            "unmapped-vendor" => new Current(f.UnmappedOwner.Id, "VendorOwner"),
            "customer-as-vendor" => new Current(f.Customer.Id, "VendorOwner"),
            "inactive-owner" or "inactive-shop" or "owner-role-changed" => f.VendorActor,
            _ => new Current(f.Users.Single(item => item.Role.ToString() == identity).Id, identity)
        };
        if (identity == "inactive-owner") (await f.Db.Users.SingleAsync(item => item.Id == f.Owner.Id)).SetStatus(false);
        if (identity == "inactive-shop") f.Db.Entry(await f.Db.Vendors.SingleAsync(item => item.Id == f.Vendor.Id)).Property(item => item.IsActive).CurrentValue = false;
        if (identity == "owner-role-changed") f.Db.Entry(await f.Db.Users.SingleAsync(item => item.Id == f.Owner.Id)).Property(item => item.Role).CurrentValue = UserRole.Customer;
        await f.Db.SaveChangesAsync(); var before = await f.Snapshot();
        await Assert.ThrowsAsync<DomainException>(() => f.List(false, actor));
        await Assert.ThrowsAsync<DomainException>(() => f.Save(f.Command, actor));
        await Assert.ThrowsAsync<DomainException>(() => new GetComboParticipationQueryHandler(f.Db, actor).Handle(new(), default));
        await Assert.ThrowsAsync<DomainException>(() => new SaveComboParticipationCommandHandler(f.Db, actor, new AuditLogger(f.Db, actor))
            .Handle(new(true, .15m), default));
        Assert.Equal(before, await f.Snapshot());
    }

    [Fact]
    public async Task VendorConsentPersistsForOnlyItsShopWithAuditWithoutChangingCommissionOrPriorTransactions()
    {
        await using var f = await Fixture.Create(); await f.SeedFinancialHistory(); var before = await f.FinancialSnapshot();
        var originalVersion = await f.Version();
        var consent = await new SaveComboParticipationCommandHandler(f.Db, f.VendorActor, new AuditLogger(f.Db, f.VendorActor))
            .Handle(new(true, .12345m), default);
        Assert.True(consent.AllowInCombos); Assert.Equal(.12345m, consent.MaxComboDiscount);
        await using var reload = f.Open();
        var stored = await new GetComboParticipationQueryHandler(reload, f.VendorActor).Handle(new(), default);
        Assert.True(stored.AllowInCombos); Assert.Equal(.12345m, stored.MaxComboDiscount);
        var other = await new GetComboParticipationQueryHandler(reload, f.OtherActor).Handle(new(), default);
        Assert.False(other.AllowInCombos); Assert.Equal(0m, other.MaxComboDiscount);
        var vendors = await reload.Vendors.AsNoTracking().ToListAsync(); Assert.All(vendors, item => Assert.Equal(.08m, item.CommissionRate));
        var audit = Assert.Single(await reload.AuditLogs.AsNoTracking().Where(item => item.Action == "Vendor.ComboConsent").ToListAsync());
        Assert.Equal(f.Owner.Id, audit.ActorId); Assert.Equal("VendorOwner", audit.ActorRole);
        Assert.Equal(f.Vendor.Id.ToString(), audit.EntityId); Assert.Contains("0.12345", audit.DataJson!);
        Assert.Equal(before, await f.FinancialSnapshot()); Assert.Equal(originalVersion, await f.Version());
        var stable = await f.Snapshot();
        foreach (var invalid in new[] { -.00001m, .30001m })
        {
            await Assert.ThrowsAsync<DomainException>(() => new SaveComboParticipationCommandHandler(f.Db, f.VendorActor, new AuditLogger(f.Db, f.VendorActor))
                .Handle(new(false, invalid), default));
            Assert.Equal(stable, await f.Snapshot());
        }
    }

    [Theory]
    [InlineData("Moderator", true)] [InlineData("SuperAdmin", false)]
    public async Task StaffReviewPersistsTrimmedReasonAndOneOwnerNotificationWithRealActorAudit(string role, bool approve)
    {
        await using var f = await Fixture.Create(); var actor = role == "Moderator" ? f.ModeratorActor : f.AdminActor;
        var version = await f.Version();
        await f.ReviewWithVersion(approve, version, "  Reviewed service details  ", actor);
        await using var reload = f.Open(); var item = await reload.ServiceItems.AsNoTracking().SingleAsync(item => item.Id == f.Item.Id);
        Assert.Equal(approve ? "Approved" : "Rejected", item.ModerationStatus); Assert.Equal("Reviewed service details", item.ReviewNote);
        var notification = Assert.Single(await reload.Notifications.AsNoTracking().ToListAsync());
        Assert.Equal(f.Owner.Id, notification.UserId); Assert.Equal("InApp", notification.Channel);
        Assert.Contains(f.Item.Name, notification.Message); Assert.Contains("Reviewed service details", notification.Message);
        Assert.Null(notification.ContextKind); Assert.Null(notification.ContextId);
        var audit = Assert.Single(await reload.AuditLogs.AsNoTracking().Where(item => item.Action == "ServiceItem.Review").ToListAsync());
        Assert.Equal(actor.UserId, audit.ActorId); Assert.Equal(role, audit.ActorRole); Assert.Equal(nameof(ServiceItem), audit.EntityType);
        Assert.Equal(f.Item.Id.ToString(), audit.EntityId); Assert.NotNull(audit.DataJson);
        var before = await f.Snapshot();
        await Assert.ThrowsAsync<DomainException>(() => f.ReviewWithVersion(approve, version, "Duplicate review", actor));
        Assert.Equal(before, await f.Snapshot());
    }

    [Theory]
    [InlineData("draft-listing")] [InlineData("pending-listing")] [InlineData("rejected-listing")] [InlineData("hidden-listing")]
    [InlineData("inactive-listing")] [InlineData("inactive-category")] [InlineData("inactive-vendor")] [InlineData("inactive-owner")]
    [InlineData("owner-role-changed")] [InlineData("unverified-vendor")] [InlineData("tax-missing")] [InlineData("tax-draft")]
    [InlineData("tax-submitted")] [InlineData("tax-rejected")] [InlineData("tax-suspended")]
    [InlineData("item-category-mismatch")] [InlineData("item-vendor-mismatch")]
    public async Task ApprovalRevalidatesEveryLivePublicationAndBusinessDependencyBeforeMutation(string dependency)
    {
        await using var f = await Fixture.Create(); await f.DisableDependency(dependency);
        var before = await f.Snapshot(); var version = await f.Version();
        await Assert.ThrowsAsync<DomainException>(() => f.ReviewWithVersion(true, version));
        Assert.Equal(before, await f.Snapshot());
    }

    [Fact]
    public async Task ApprovalDoesNotRequireComboConsentAndNeverEnablesAnInactiveService()
    {
        await using var f = await Fixture.Create();
        Assert.False(f.Vendor.AllowInCombos);
        f.Db.Entry(await f.Db.ServiceItems.SingleAsync(item => item.Id == f.Item.Id)).Property(item => item.IsActive).CurrentValue = false;
        await f.Db.SaveChangesAsync(); await f.Review(true);
        var item = await f.Db.ServiceItems.AsNoTracking().SingleAsync(item => item.Id == f.Item.Id);
        Assert.Equal("Approved", item.ModerationStatus); Assert.False(item.IsActive);
        Assert.False((await f.Db.Vendors.AsNoTracking().SingleAsync(item => item.Id == f.Vendor.Id)).AllowInCombos);
    }

    [Theory]
    [InlineData("inactive-listing")] [InlineData("inactive-vendor")] [InlineData("inactive-owner")] [InlineData("tax-suspended")]
    public async Task StaffCanRejectPendingWorkWithReasonEvenWhenPublicationDependenciesHaveBecomeUnavailable(string dependency)
    {
        await using var f = await Fixture.Create(); await f.DisableDependency(dependency);
        await f.Review(false, "Please restore the missing publication requirements");
        var item = await f.Db.ServiceItems.AsNoTracking().SingleAsync(item => item.Id == f.Item.Id);
        Assert.Equal("Rejected", item.ModerationStatus); Assert.Equal("Please restore the missing publication requirements", item.ReviewNote);
        Assert.Single(await f.Db.Notifications.AsNoTracking().ToListAsync());
    }

    [Theory]
    [InlineData(null)] [InlineData("")] [InlineData("   ")] [InlineData("oversized")]
    public async Task RejectRequiresANonWhitespaceReasonWithinTheBoundAndCannotLeaveAuditOrNotification(string? reason)
    {
        await using var f = await Fixture.Create(); var before = await f.Snapshot();
        await Assert.ThrowsAsync<DomainException>(() => f.Review(false, reason == "oversized" ? new string('n', 2001) : reason));
        Assert.Equal(before, await f.Snapshot());
    }

    [Fact]
    public async Task PendingContentVersionIsRequiredAndStaleStaffCannotReviewAnUpdatedResubmittedItem()
    {
        await using var f = await Fixture.Create(); var old = await f.Version(); var before = await f.Snapshot();
        foreach (var version in new[] { null, "", "  ", "not-a-current-service-version" })
        {
            await Assert.ThrowsAsync<DomainException>(() => f.ReviewWithVersion(true, version));
            Assert.Equal(before, await f.Snapshot());
        }
        await f.Save(f.Command with { Name = "Changed after staff loaded the queue", SubmitForApproval = true,
            Variants = [new(null, "New selected variant", 20000)], AddOns = [new(null, "New optional addon", 50000, true)] });
        var latest = await f.Version(); Assert.NotEqual(old, latest); before = await f.Snapshot();
        await Assert.ThrowsAsync<DomainException>(() => f.ReviewWithVersion(true, old));
        await Assert.ThrowsAsync<DomainException>(() => f.ReviewWithVersion(false, old, "Outdated rejection"));
        Assert.Equal(before, await f.Snapshot());
        await f.ReviewWithVersion(true, latest);
        var current = Assert.Single(await f.List(false, f.VendorActor));
        Assert.Equal("Approved", current.ModerationStatus); Assert.Equal("Changed after staff loaded the queue", current.Name);
        Assert.Equal("New selected variant", Assert.Single(current.Variants).Name); Assert.Equal("New optional addon", Assert.Single(current.AddOns).Name);
        Assert.Single(await f.Db.Notifications.AsNoTracking().ToListAsync());
    }

    [Fact]
    public async Task SavingDraftIsPersistedWithNormalizedChildrenAndNullCollectionsRemainCompatibleEmptyLists()
    {
        await using var f = await Fixture.Create();
        var saved = await f.Save(f.Command with { Name = "  Updated draft  ", Regions = "  HCM  ", CancellationPolicy = "  Updated cancellation policy  ",
            Variants = [new(null, "  Trimmed variant  ", 10000)], AddOns = [new(null, "  Trimmed addon  ", 20000, true)], SubmitForApproval = false });
        Assert.Equal("Draft", saved.ModerationStatus); Assert.Equal("Updated draft", saved.Name); Assert.Equal("HCM", saved.Regions);
        Assert.Equal("Updated cancellation policy", saved.CancellationPolicy); Assert.Equal("Trimmed variant", Assert.Single(saved.Variants).Name);
        Assert.Equal("Trimmed addon", Assert.Single(saved.AddOns).Name);
        var read = Assert.Single(await f.List(false, f.VendorActor)); Assert.Equal(saved.Id, read.Id); Assert.Equal(saved.Name, read.Name);
        Assert.False(string.IsNullOrWhiteSpace(read.ReviewVersion));
        var audit = Assert.Single(await f.Db.AuditLogs.AsNoTracking().Where(item => item.Action == "ServiceItem.Save").ToListAsync());
        Assert.Equal(f.Owner.Id, audit.ActorId); Assert.Equal("VendorOwner", audit.ActorRole); Assert.Equal(saved.Id.ToString(), audit.EntityId);
        await f.Save(f.Command with { Variants = null!, AddOns = null!, SubmitForApproval = true });
        await using var reload = f.Open(); var item = await reload.ServiceItems.AsNoTracking().Include(item => item.Variants).Include(item => item.AddOns)
            .SingleAsync(item => item.Id == f.Item.Id);
        Assert.Equal("Pending", item.ModerationStatus); Assert.Empty(item.Variants); Assert.Empty(item.AddOns);
        Assert.Empty(await reload.Notifications.AsNoTracking().ToListAsync());
    }

    [Theory]
    [InlineData("name-blank")] [InlineData("name-limit")] [InlineData("negative-price")] [InlineData("fractional-price")]
    [InlineData("price-limit")] [InlineData("unit-invalid")] [InlineData("guest-min")] [InlineData("guest-order")] [InlineData("guest-limit")]
    [InlineData("region-blank")] [InlineData("region-limit")] [InlineData("policy-blank")] [InlineData("policy-limit")]
    [InlineData("capacity-min")] [InlineData("capacity-max")]
    [InlineData("variant-count")] [InlineData("addon-count")] [InlineData("variant-null")] [InlineData("addon-null")]
    [InlineData("variant-name")] [InlineData("variant-name-limit")] [InlineData("variant-fractional")] [InlineData("variant-negative-total")]
    [InlineData("addon-name")] [InlineData("addon-name-limit")] [InlineData("addon-negative")] [InlineData("addon-fractional")]
    public async Task InvalidCreateOrReplacementLeavesOriginalScalarsChildrenAndAuditCompletelyUnchanged(string invalid)
    {
        await using var f = await Fixture.Create(); var before = await f.Snapshot();
        var command = Invalid(f.Command with { Name = "Must not persist", BasePrice = 2000000 }, invalid);
        await Assert.ThrowsAsync<DomainException>(() => f.Save(command));
        Assert.Equal(before, await f.Snapshot());
        await Assert.ThrowsAsync<DomainException>(() => f.Save(command with { Id = null }));
        Assert.Equal(before, await f.Snapshot());
    }

    [Fact]
    public async Task FailedSaveOrReviewRollsBackScalarsChildrenAuditAndNotificationTogether()
    {
        await using var f = await Fixture.Create(); var before = await f.Snapshot();
        await using (var broken = new FailingAfterSaveContext(f.Options))
            await Assert.ThrowsAsync<InvalidOperationException>(() => new SaveServiceItemCommandHandler(broken, f.VendorActor, new AuditLogger(broken, f.VendorActor))
                .Handle(f.Command with { Name = "Must roll back", Variants = [], AddOns = [] }, default));
        Assert.Equal(before, await f.Snapshot());
        var version = await f.Version();
        await using (var broken = new FailingAfterSaveContext(f.Options))
            await Assert.ThrowsAsync<InvalidOperationException>(() => new ReviewServiceItemCommandHandler(broken, new AuditLogger(broken, f.ModeratorActor), f.ModeratorActor)
                .Handle(new(f.Item.Id, true, "Atomic review", version), default));
        Assert.Equal(before, await f.Snapshot());
    }

    [Theory]
    [InlineData(true)] [InlineData(false)]
    public async Task ReviewingChangedServiceNeverRepricesConfirmedOrderContractOrPreviouslyPaidCommission(bool approve)
    {
        await using var f = await Fixture.Create(); await f.SeedFinancialHistory(); var financial = await f.FinancialSnapshot();
        await f.Save(f.Command with { Name = "Future service configuration", BasePrice = 5000000, CancellationPolicy = "New policy for future orders",
            Variants = [new(null, "Future variant", 200000)], AddOns = [], SubmitForApproval = true });
        Assert.Equal(financial, await f.FinancialSnapshot());
        await f.Review(approve, "Reviewed changed service configuration");
        Assert.Equal(financial, await f.FinancialSnapshot());
        var contract = await f.Db.BookingContracts.AsNoTracking().SingleAsync(); Assert.Equal(.08m, contract.CommissionRateSnapshot);
        Assert.Equal(ContractStatus.Confirmed, contract.Status);
        var paid = await f.Db.Commissions.AsNoTracking().SingleAsync(); Assert.Equal(38800m, paid.PaidAmount);
        Assert.Equal(CommissionStatus.Settled, paid.Status); Assert.Equal("SYNTHETIC-SERVICE-PAID", paid.PaymentReferenceCode);
    }

    [SqlServerFact]
    public async Task SqlReloadPreservesOpaqueReviewVersionForWholeMoneyWithDifferentDecimalScalesAndRemainsReviewable()
    {
        var database = $"WeddingPlanTest_ServiceModeration_{Guid.NewGuid():N}";
        var source = Environment.GetEnvironmentVariable("WEDDING_TEST_SQLSERVER");
        var builder = new SqlConnectionStringBuilder(string.IsNullOrWhiteSpace(source) || source == "1"
            ? "Server=localhost;Integrated Security=true;TrustServerCertificate=true;Connection Timeout=5" : source)
            { InitialCatalog = database };
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlServer(builder.ConnectionString).Options;
        await using var db = new ApplicationDbContext(options);
        try
        {
            await db.Database.MigrateAsync();
            var owner = User.Create("Synthetic SQL owner", "owner@service-sql.example.invalid", "0901234567", "synthetic-owner-hash", UserRole.VendorOwner);
            var moderator = User.Create("Synthetic SQL moderator", "moderator@service-sql.example.invalid", "0901234568", "synthetic-mod-hash", UserRole.Moderator);
            db.Users.AddRange(owner, moderator);
            var vendor = Vendor.Create(owner.Id, "Synthetic SQL shop", "synthetic-sql-service-shop", "Owner", "0901234567", "HCM", .08m);
            vendor.SetVerified(true); db.Vendors.Add(vendor);
            var tax = VendorTaxProfile.Create(vendor.Id); tax.Update("Synthetic SQL business", "1234567890", "VCB", "123456789", "Synthetic holder", true);
            tax.Submit(); tax.Review(true); db.VendorTaxProfiles.Add(tax);
            var category = await db.Categories.SingleAsync(item => item.Slug == "quay-chup-photo-video");
            var listing = Listing.Create(vendor.Id, category.Id, "Synthetic SQL listing", "synthetic-sql-service-listing", 1000m, 10000m, "Synthetic description", "HCM");
            listing.Approve(); db.Listings.Add(listing); await db.SaveChangesAsync();
            var ownerActor = new Current(owner.Id, "VendorOwner"); var staffActor = new Current(moderator.Id, "Moderator");
            var command = new SaveServiceItemCommand(null, listing.Id, "SQL roundtrip service", 1000.00m, "Package", 1, 1000, "HCM", "Synthetic original policy", true,
                [new(null, "Scaled positive", 1000.0m), new(null, "Scaled negative", -1000.000m)],
                [new(null, "Scaled whole addon", 1000.00m, true), new(null, "Scaled zero addon", 0.0000m, false)], true, 10);
            var saved = await new SaveServiceItemCommandHandler(db, ownerActor, new AuditLogger(db, ownerActor)).Handle(command, default);
            Assert.Equal("Pending", saved.ModerationStatus); Assert.False(string.IsNullOrWhiteSpace(saved.ReviewVersion));
            db.ChangeTracker.Clear();
            await using var reload = new ApplicationDbContext(options);
            var own = Assert.Single(await new GetServiceItemsQueryHandler(reload, ownerActor).Handle(new(false), default));
            var queued = Assert.Single(await new GetServiceItemsQueryHandler(reload, staffActor).Handle(new(true), default));
            Assert.Equal(saved.ReviewVersion, own.ReviewVersion); Assert.Equal(saved.ReviewVersion, queued.ReviewVersion);
            Assert.Equal(1000m, queued.BasePrice); Assert.Equal(1000m, queued.Variants.Single(item => item.Name == "Scaled positive").PriceDelta);
            Assert.Equal(-1000m, queued.Variants.Single(item => item.Name == "Scaled negative").PriceDelta);
            Assert.Equal(1000m, queued.AddOns.Single(item => item.Name == "Scaled whole addon").Price);
            await new ReviewServiceItemCommandHandler(reload, new AuditLogger(reload, staffActor), staffActor)
                .Handle(new(saved.Id, true, "Approved from pre-reload queue token", saved.ReviewVersion), default);
            reload.ChangeTracker.Clear();
            var approved = await reload.ServiceItems.AsNoTracking().SingleAsync(item => item.Id == saved.Id);
            Assert.Equal("Approved", approved.ModerationStatus); Assert.Equal(1000m, approved.BasePrice);
            var notification = Assert.Single(await reload.Notifications.AsNoTracking().ToListAsync());
            Assert.Equal(owner.Id, notification.UserId); Assert.Null(notification.ContextKind); Assert.Null(notification.ContextId);
            var reviewAudit = Assert.Single(await reload.AuditLogs.AsNoTracking().Where(item => item.Action == "ServiceItem.Review").ToListAsync());
            Assert.Equal(moderator.Id, reviewAudit.ActorId); Assert.Contains(saved.ReviewVersion, reviewAudit.DataJson!);

            // Editing an existing parent must INSERT fresh children, not infer UPDATE for their generated Guids.
            var oldVariantIds = queued.Variants.Select(item => item.Id).ToHashSet();
            var oldAddOnIds = queued.AddOns.Select(item => item.Id).ToHashSet();
            var replacement = command with { Id = saved.Id, Name = "Replaced SQL service", BasePrice = 2500.000m,
                Variants = [new(null, "Replacement scaled variant", -500.00m)],
                AddOns = [new(null, "Replacement scaled addon", 1000.0m, false)], SubmitForApproval = true };
            var replaced = await new SaveServiceItemCommandHandler(reload, ownerActor, new AuditLogger(reload, ownerActor)).Handle(replacement, default);
            Assert.Equal(saved.Id, replaced.Id); Assert.Equal("Pending", replaced.ModerationStatus);
            Assert.NotEqual(saved.ReviewVersion, replaced.ReviewVersion);
            Assert.DoesNotContain(Assert.Single(replaced.Variants).Id, oldVariantIds);
            Assert.DoesNotContain(Assert.Single(replaced.AddOns).Id, oldAddOnIds);
            reload.ChangeTracker.Clear();
            await using var afterReplacement = new ApplicationDbContext(options);
            var fresh = Assert.Single(await new GetServiceItemsQueryHandler(afterReplacement, staffActor).Handle(new(true), default));
            Assert.Equal(replaced.ReviewVersion, fresh.ReviewVersion); Assert.Equal(2500m, fresh.BasePrice);
            var freshVariant = Assert.Single(fresh.Variants); Assert.Equal("Replacement scaled variant", freshVariant.Name); Assert.Equal(-500m, freshVariant.PriceDelta);
            var freshAddOn = Assert.Single(fresh.AddOns); Assert.Equal("Replacement scaled addon", freshAddOn.Name); Assert.Equal(1000m, freshAddOn.Price); Assert.False(freshAddOn.PerUnit);
            Assert.Equal(replaced.Variants[0].Id, freshVariant.Id); Assert.Equal(replaced.AddOns[0].Id, freshAddOn.Id);
            Assert.Equal(1, await afterReplacement.ItemVariants.CountAsync(item => item.ServiceItemId == saved.Id));
            Assert.Equal(1, await afterReplacement.ItemAddOns.CountAsync(item => item.ServiceItemId == saved.Id));
            await Assert.ThrowsAsync<DomainException>(() => new ReviewServiceItemCommandHandler(afterReplacement, new AuditLogger(afterReplacement, staffActor), staffActor)
                .Handle(new(saved.Id, true, "Must not approve changed children using the old queue", queued.ReviewVersion), default));
            Assert.Equal(1, await afterReplacement.Notifications.CountAsync());
            Assert.Equal(1, await afterReplacement.AuditLogs.CountAsync(item => item.Action == "ServiceItem.Review"));
            await new ReviewServiceItemCommandHandler(afterReplacement, new AuditLogger(afterReplacement, staffActor), staffActor)
                .Handle(new(saved.Id, true, "Approved the replacement children", fresh.ReviewVersion), default);
            afterReplacement.ChangeTracker.Clear();
            var final = await afterReplacement.ServiceItems.AsNoTracking().Include(item => item.Variants).Include(item => item.AddOns).SingleAsync(item => item.Id == saved.Id);
            Assert.Equal("Approved", final.ModerationStatus); Assert.Equal(2500m, final.BasePrice);
            Assert.Equal(freshVariant.Id, Assert.Single(final.Variants).Id); Assert.Equal(-500m, final.Variants.Single().PriceDelta);
            Assert.Equal(freshAddOn.Id, Assert.Single(final.AddOns).Id); Assert.Equal(1000m, final.AddOns.Single().Price);
            var notifications = await afterReplacement.Notifications.AsNoTracking().ToListAsync(); Assert.Equal(2, notifications.Count);
            Assert.All(notifications, item => { Assert.Equal(owner.Id, item.UserId); Assert.Null(item.ContextKind); Assert.Null(item.ContextId); });
            var reviews = await afterReplacement.AuditLogs.AsNoTracking().Where(item => item.Action == "ServiceItem.Review").ToListAsync(); Assert.Equal(2, reviews.Count);
            Assert.All(reviews, item => Assert.Equal(moderator.Id, item.ActorId));
            Assert.Contains(reviews, item => item.DataJson!.Contains(saved.ReviewVersion)); Assert.Contains(reviews, item => item.DataJson!.Contains(fresh.ReviewVersion));
            Assert.Equal(.08m, (await reload.Vendors.AsNoTracking().SingleAsync(item => item.Id == vendor.Id)).CommissionRate);
            Assert.Empty(await reload.BookingContracts.AsNoTracking().ToListAsync()); Assert.Empty(await reload.Commissions.AsNoTracking().ToListAsync());
            Assert.Empty(await reload.Database.GetPendingMigrationsAsync()); Assert.False(reload.Database.HasPendingModelChanges());
        }
        finally
        {
            Assert.StartsWith("WeddingPlanTest_ServiceModeration_", database);
            await db.Database.EnsureDeletedAsync();
        }
    }

    private static SaveServiceItemCommand Invalid(SaveServiceItemCommand command, string invalid) => invalid switch
    {
        "name-blank" => command with { Name = "  " }, "name-limit" => command with { Name = new string('n', 251) },
        "negative-price" => command with { BasePrice = -1 }, "fractional-price" => command with { BasePrice = 1.5m },
        "price-limit" => command with { BasePrice = 100000000001m }, "unit-invalid" => command with { Unit = "UnknownUnit" },
        "guest-min" => command with { MinGuests = 0 }, "guest-order" => command with { MinGuests = 200, MaxGuests = 100 },
        "guest-limit" => command with { MaxGuests = 10001 }, "region-blank" => command with { Regions = " " },
        "region-limit" => command with { Regions = new string('r', 1001) }, "policy-blank" => command with { CancellationPolicy = " " },
        "policy-limit" => command with { CancellationPolicy = new string('c', 4001) },
        "capacity-min" => command with { MaxBookingsPerDay = 0 }, "capacity-max" => command with { MaxBookingsPerDay = 101 },
        "variant-count" => command with { Variants = Enumerable.Range(0, 21).Select(index => new VariantDto(null, $"Variant {index}", 0)).ToList() },
        "addon-count" => command with { AddOns = Enumerable.Range(0, 31).Select(index => new AddOnDto(null, $"Addon {index}", 0, false)).ToList() },
        "variant-null" => command with { Variants = [null!] }, "addon-null" => command with { AddOns = [null!] },
        "variant-name" => command with { Variants = [new(null, " ", 0)] },
        "variant-name-limit" => command with { Variants = [new(null, new string('v', 201), 0)] },
        "variant-fractional" => command with { Variants = [new(null, "Variant", .5m)] },
        "variant-negative-total" => command with { Variants = [new(null, "Negative resulting price", -command.BasePrice - 1)] },
        "addon-name" => command with { AddOns = [new(null, " ", 0, false)] },
        "addon-name-limit" => command with { AddOns = [new(null, new string('a', 201), 0, false)] },
        "addon-negative" => command with { AddOns = [new(null, "Addon", -1, false)] },
        "addon-fractional" => command with { AddOns = [new(null, "Addon", .5m, false)] },
        _ => throw new InvalidOperationException("Unknown validation fixture.")
    };

    private sealed record Current(Guid? UserId, string? Role) : ICurrentUserService { public string? Email => null; }
    private sealed class FailingAfterSaveContext(DbContextOptions<ApplicationDbContext> options) : ApplicationDbContext(options)
    {
        public override async Task<int> SaveChangesAsync(CancellationToken ct = default)
        { await base.SaveChangesAsync(ct); throw new InvalidOperationException("Synthetic service failure before atomic commit."); }
    }

    private sealed class Fixture(SqliteConnection connection, ApplicationDbContext db) : IAsyncDisposable
    {
        public ApplicationDbContext Db { get; } = db;
        public DbContextOptions<ApplicationDbContext> Options => new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options;
        public ApplicationDbContext Open() => new(Options);
        public List<User> Users { get; } = [];
        public User Owner { get; private set; } = null!; public User OtherOwner { get; private set; } = null!;
        public User UnmappedOwner { get; private set; } = null!; public User Customer { get; private set; } = null!;
        public User Moderator { get; private set; } = null!; public User Admin { get; private set; } = null!;
        public Vendor Vendor { get; private set; } = null!; public Vendor OtherVendor { get; private set; } = null!;
        public Listing Listing { get; private set; } = null!; public Listing OtherListing { get; private set; } = null!;
        public Category Category { get; private set; } = null!; public Category OtherCategory { get; private set; } = null!;
        public ServiceItem Item { get; private set; } = null!; public ServiceItem OtherItem { get; private set; } = null!;
        public Current VendorActor => new(Owner.Id, "VendorOwner"); public Current OtherActor => new(OtherOwner.Id, "VendorOwner");
        public Current ModeratorActor => new(Moderator.Id, "Moderator"); public Current AdminActor => new(Admin.Id, "SuperAdmin");
        public SaveServiceItemCommand Command => new(Item.Id, Listing.Id, "Original service", 1000000, "Package", 1, 1000, "HCM",
            "Original cancellation terms", true, [new(null, "Original variant", 10000)], [new(null, "Original addon", 20000, false)], MaxBookingsPerDay: 10);
        public Task<List<ServiceItemDto>> List(bool moderation, Current actor) => new GetServiceItemsQueryHandler(Db, actor).Handle(new(moderation), default);
        public Task<ServiceItemDto> Save(SaveServiceItemCommand command, Current? actor = null)
        { var current = actor ?? VendorActor; return new SaveServiceItemCommandHandler(Db, current, new AuditLogger(Db, current)).Handle(command, default); }
        public async Task<string?> Version() => (await List(true, AdminActor)).Single(item => item.Id == Item.Id).ReviewVersion;
        public async Task Review(bool approve, string? note = null, Current? actor = null) => await ReviewWithVersion(approve, await Version(), note, actor);
        public Task<bool> ReviewWithVersion(bool approve, string? version, string? note = null, Current? actor = null)
        { var current = actor ?? ModeratorActor; return new ReviewServiceItemCommandHandler(Db, new AuditLogger(Db, current), current).Handle(new(Item.Id, approve, note, version), default); }
        public async Task DisableDependency(string dependency)
        {
            var listing = await Db.Listings.SingleAsync(item => item.Id == Listing.Id);
            switch (dependency)
            {
                case "draft-listing": listing.SetStatus(ListingStatus.Draft); break;
                case "pending-listing": listing.SetStatus(ListingStatus.PendingApproval); break;
                case "rejected-listing": listing.SetStatus(ListingStatus.Rejected); break;
                case "hidden-listing": listing.SetStatus(ListingStatus.Hidden); break;
                case "inactive-listing": listing.SetActivity(false); break;
                case "inactive-category": Db.Entry(await Db.Categories.SingleAsync(item => item.Id == Category.Id)).Property(item => item.IsActive).CurrentValue = false; break;
                case "inactive-vendor": Db.Entry(await Db.Vendors.SingleAsync(item => item.Id == Vendor.Id)).Property(item => item.IsActive).CurrentValue = false; break;
                case "inactive-owner": (await Db.Users.SingleAsync(item => item.Id == Owner.Id)).SetStatus(false); break;
                case "owner-role-changed": Db.Entry(await Db.Users.SingleAsync(item => item.Id == Owner.Id)).Property(item => item.Role).CurrentValue = UserRole.Customer; break;
                case "unverified-vendor": (await Db.Vendors.SingleAsync(item => item.Id == Vendor.Id)).SetVerified(false); break;
                case "tax-missing": Db.VendorTaxProfiles.Remove(await Db.VendorTaxProfiles.SingleAsync(item => item.VendorId == Vendor.Id)); break;
                case "tax-draft": case "tax-submitted": case "tax-rejected": case "tax-suspended":
                    Db.Entry(await Db.VendorTaxProfiles.SingleAsync(item => item.VendorId == Vendor.Id)).Property(item => item.Status).CurrentValue = dependency switch
                    { "tax-draft" => VerificationStatus.Draft, "tax-submitted" => VerificationStatus.Submitted, "tax-rejected" => VerificationStatus.Rejected, _ => VerificationStatus.Suspended }; break;
                case "item-category-mismatch": Db.Entry(await Db.ServiceItems.SingleAsync(item => item.Id == Item.Id)).Property(item => item.CategoryId).CurrentValue = OtherCategory.Id; break;
                case "item-vendor-mismatch": Db.Entry(await Db.ServiceItems.SingleAsync(item => item.Id == Item.Id)).Property(item => item.VendorId).CurrentValue = OtherVendor.Id; break;
                default: throw new InvalidOperationException("Unknown dependency fixture.");
            }
            await Db.SaveChangesAsync();
        }
        public async Task<string> Snapshot()
        {
            await using var read = Open(); var items = await read.ServiceItems.AsNoTracking().Include(item => item.Variants).Include(item => item.AddOns).OrderBy(item => item.Id).ToListAsync();
            return JsonSerializer.Serialize(new { Items = items.Select(item => new { item.Id, item.VendorId, item.ListingId, item.CategoryId, item.Name, item.BasePrice,
                item.Unit, item.MinGuests, item.MaxGuests, item.Regions, item.CancellationPolicy, item.ModerationStatus, item.ReviewNote, item.IsActive, item.MaxBookingsPerDay,
                item.CreatedAt, item.UpdatedAt, Variants = item.Variants.OrderBy(child => child.Id).Select(child => new { child.Id, child.Name, child.PriceDelta }),
                AddOns = item.AddOns.OrderBy(child => child.Id).Select(child => new { child.Id, child.Name, child.Price, child.PerUnit }) }),
                Vendors = await read.Vendors.AsNoTracking().OrderBy(item => item.Id).Select(item => new { item.Id, item.AllowInCombos, item.MaxComboDiscount,
                    item.CommissionRate, item.UpdatedAt }).ToListAsync(),
                Audits = await read.AuditLogs.CountAsync(), Notifications = await read.Notifications.CountAsync() });
        }
        public async Task SeedFinancialHistory()
        {
            var date = DateTime.UtcNow.AddDays(30);
            var lead = Lead.Create(Customer.Id, Vendor.Id, Listing.Id, Customer.PhoneNumber, date, 100, 1000000, "Synthetic prior agreement"); Db.Leads.Add(lead);
            var order = ComboOrder.Create(Customer.Id, null, date, 100, "Original accepted order", new[] { (Listing.Id, Vendor.Id, Category.Id, "Original accepted line", 1000000m) });
            order.SetPricing(.03m, "HCM", false); var line = order.Items.Single(); line.SetSelection(Item.Id, 1, "Package", "{\"original\":true}", "Original locked cancellation");
            line.AttachLead(lead.Id); line.SetStatus(ComboItemStatus.Contracted); order.Recalculate(); Db.ComboOrders.Add(order);
            var contract = BookingContract.Create(lead.Id, null, Vendor.Id, Customer.Id, 970000, 200000, null, date);
            contract.SetCancellationPolicy("Original locked cancellation"); contract.SnapshotCommissionRate(.08m); contract.ConfirmByCustomer(); Db.BookingContracts.Add(contract);
            var commission = Commission.Create(contract.Id, Vendor.Id, CommissionPeriod.Period1_Deposit, .08m, 38800, DateTime.UtcNow.AddDays(10));
            commission.MarkPaid("SYNTHETIC-SERVICE-PAID"); Db.Commissions.Add(commission); await Db.SaveChangesAsync();
        }
        public async Task<string> FinancialSnapshot()
        {
            await using var read = Open();
            return JsonSerializer.Serialize(new {
                Orders = await read.ComboOrders.AsNoTracking().OrderBy(item => item.Id).Select(item => new { item.Id, item.Subtotal, item.DiscountPercent, item.DiscountAmount,
                    item.Total, item.PriceLockUntil, item.Status }).ToListAsync(),
                Lines = await read.ComboOrderItems.AsNoTracking().OrderBy(item => item.Id).Select(item => new { item.Id, item.Title, item.Price, item.DiscountShare,
                    item.SelectionJson, item.CancellationPolicySnapshot, item.Quantity, item.Unit, item.Status, item.ServiceItemId }).ToListAsync(),
                Contracts = await read.BookingContracts.AsNoTracking().OrderBy(item => item.Id).Select(item => new { item.Id, item.ContractValue, item.DepositAmount,
                    item.CommissionRateSnapshot, item.ConfirmedAt, item.CancellationPolicySnapshot, item.Version, item.Status }).ToListAsync(),
                Commissions = await read.Commissions.AsNoTracking().OrderBy(item => item.Id).Select(item => new { item.Id, item.CommissionRate, item.CommissionAmount,
                    item.PaidAmount, item.PaidAt, item.PaymentReferenceCode, item.Version, item.Status }).ToListAsync() });
        }
        public static async Task<Fixture> Create()
        {
            var connection = new SqliteConnection("Data Source=:memory:"); await connection.OpenAsync();
            var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options); await db.Database.EnsureCreatedAsync(); var f = new Fixture(connection, db);
            User Add(string name, UserRole role) { var user = User.Create(name, $"{name}@service-moderation.example.invalid", "0901234567", "synthetic-hash", role); db.Users.Add(user); f.Users.Add(user); return user; }
            f.Owner = Add("owner", UserRole.VendorOwner); f.OtherOwner = Add("other", UserRole.VendorOwner); f.UnmappedOwner = Add("unmapped", UserRole.VendorOwner);
            f.Customer = Add("customer", UserRole.Customer); f.Moderator = Add("moderator", UserRole.Moderator); f.Admin = Add("admin", UserRole.SuperAdmin);
            Add("finance", UserRole.Finance); Add("care", UserRole.CustomerCare);
            f.Vendor = Vendor.Create(f.Owner.Id, "Synthetic service shop", "synthetic-service-shop", "Owner", "0901234567", "HCM", .08m);
            f.OtherVendor = Vendor.Create(f.OtherOwner.Id, "Other synthetic shop", "other-service-shop", "Other", "0901234568", "HCM", .08m);
            foreach (var vendor in new[] { f.Vendor, f.OtherVendor })
            {
                vendor.SetVerified(true); db.Vendors.Add(vendor);
                var tax = VendorTaxProfile.Create(vendor.Id); tax.Update("Synthetic business", "1234567890", "VCB", "123456789", "Synthetic holder", true); tax.Submit(); tax.Review(true); db.VendorTaxProfiles.Add(tax);
            }
            f.Category = await db.Categories.SingleAsync(item => item.Slug == "quay-chup-photo-video"); f.OtherCategory = await db.Categories.SingleAsync(item => item.Slug == "tiem-cuoi-venues");
            Listing ListingFor(Vendor vendor, string slug) { var listing = Listing.Create(vendor.Id, f.Category.Id, "Synthetic service listing", slug, 1000000, 2000000, "Synthetic description", "HCM"); listing.Approve(); db.Listings.Add(listing); return listing; }
            f.Listing = ListingFor(f.Vendor, "synthetic-service-listing"); f.OtherListing = ListingFor(f.OtherVendor, "other-service-listing");
            f.Item = ServiceItem.Create(f.Vendor.Id, f.Listing.Id, f.Category.Id, "Original service", 1000000, "Package", 1, 1000, "HCM", "Original cancellation terms");
            f.Item.SetDailyCapacity(10); f.Item.Variants.Add(ItemVariant.Create(f.Item.Id, "Original variant", 10000)); f.Item.AddOns.Add(ItemAddOn.Create(f.Item.Id, "Original addon", 20000, false)); f.Item.Submit();
            f.OtherItem = ServiceItem.Create(f.OtherVendor.Id, f.OtherListing.Id, f.Category.Id, "Other private service", 2000000, "Package", 1, 1000, "HCM", "Other policy"); f.OtherItem.Submit();
            db.ServiceItems.AddRange(f.Item, f.OtherItem); await db.SaveChangesAsync(); return f;
        }
        public async ValueTask DisposeAsync() { await Db.DisposeAsync(); await connection.DisposeAsync(); }
    }
}
