using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Wedding.Application.Common.Interfaces;
using Wedding.Application.Features.CommissionPolicies;
using Wedding.Domain.Entities;
using Wedding.Domain.Enums;
using Wedding.Domain.Exceptions;
using Wedding.Infrastructure.Persistence;

namespace Wedding.Tests;

public class CommissionPolicyWorkflowGuardTests
{
    private static readonly DateTime Start = new(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData(CommissionRuleScope.System)]
    [InlineData(CommissionRuleScope.Category)]
    [InlineData(CommissionRuleScope.Vendor)]
    [InlineData(CommissionRuleScope.VendorCategory)]
    [InlineData(CommissionRuleScope.Service)]
    public void NonComboPolicyRequiresExplicitRate(CommissionRuleScope scope)
    {
        Assert.Throws<DomainException>(() => CommissionPolicyRule.Create(scope,
            scope is CommissionRuleScope.Category or CommissionRuleScope.VendorCategory ? Guid.NewGuid() : null,
            scope is CommissionRuleScope.Vendor or CommissionRuleScope.VendorCategory ? Guid.NewGuid() : null,
            scope == CommissionRuleScope.Service ? Guid.NewGuid() : null, null, null, null,
            CommissionBasisKind.StandardService, "Test policy", "Explicit rate required", Start, null, Guid.NewGuid()));
    }

    [Fact]
    public void PolicyRejectsRateThatWouldBeRoundedBySqlServer()
    {
        Assert.Throws<DomainException>(() => CommissionPolicyRule.Create(CommissionRuleScope.System,
            null, null, null, null, null, .08001m, CommissionBasisKind.StandardService,
            "Test policy", "Do not silently round financial terms", Start, null, Guid.NewGuid()));
    }

    [Fact]
    public Task SqliteSupplement_PolicyTargetAndReviewGuards() => PolicyTargetAndReviewGuards(false);

    [SqlServerFact]
    public Task SqlServer_PolicyTargetAndReviewGuards() => PolicyTargetAndReviewGuards(true);

    private static async Task PolicyTargetAndReviewGuards(bool sqlServer)
    {
        await using var fixture = await Fixture.Create(sqlServer);
        var db = fixture.Db;
        var actor = new Actor(fixture.Admin.Id, "SuperAdmin");
        var create = new CreateCommissionPolicyRuleCommandHandler(db, actor);
        var submit = new SubmitCommissionPolicyRuleCommandHandler(db, actor);
        var review = new ReviewCommissionPolicyRuleCommandHandler(db, actor);
        var created = await create.Handle(fixture.ServiceCommand, default);
        var submitted = await submit.Handle(new(created.Id, created.Version), default);

        // Revoking a target account must block approval, but must not strand a rejected proposal.
        fixture.Owner.SetStatus(false);
        await db.SaveChangesAsync();
        await Assert.ThrowsAsync<DomainException>(() => create.Handle(fixture.ServiceCommand, default));
        await Assert.ThrowsAsync<DomainException>(() => review.Handle(new(submitted.Id, submitted.Version, true, null), default));
        var pending = await db.CommissionPolicyRules.AsNoTracking().SingleAsync(x => x.Id == submitted.Id);
        Assert.Equal(CommissionPolicyStatus.Submitted, pending.Status);
        Assert.Equal(submitted.Version, pending.Version);
        await Assert.ThrowsAsync<DomainException>(() => review.Handle(new(submitted.Id, Guid.NewGuid(), false, "Stale"), default));
        await Assert.ThrowsAsync<DomainException>(() => review.Handle(new(submitted.Id, submitted.Version, false, " "), default));
        var financeActor = new ReviewCommissionPolicyRuleCommandHandler(db, new Actor(fixture.Finance.Id, "Finance"));
        await Assert.ThrowsAsync<DomainException>(() => financeActor.Handle(new(submitted.Id, submitted.Version, false, "Wrong role"), default));
        var staleAdminClaim = new ReviewCommissionPolicyRuleCommandHandler(db, new Actor(fixture.Finance.Id, "SuperAdmin"));
        await Assert.ThrowsAsync<DomainException>(() => staleAdminClaim.Handle(new(submitted.Id, submitted.Version, false, "Stale role"), default));
        var rejected = await review.Handle(new(submitted.Id, submitted.Version, false, "  Target account is no longer active  "), default);
        Assert.Equal(CommissionPolicyStatus.Rejected, rejected.Status);
        Assert.Equal("Target account is no longer active", rejected.RejectionReason);
        Assert.Equal(fixture.Admin.Id, rejected.ApprovedById);
        Assert.Equal(submitted.Rate, rejected.Rate);
        Assert.Equal(submitted.BasisKind, rejected.BasisKind);
        Assert.Equal(submitted.ServiceItemId, rejected.ServiceItemId);
        Assert.Equal(submitted.EffectiveFrom, rejected.EffectiveFrom);
        Assert.Equal(submitted.EffectiveTo, rejected.EffectiveTo);
        Assert.NotEqual(submitted.Version, rejected.Version);

        // A rejected transactional command clears the change tracker. Reload before each later mutation;
        // retained fixture objects are then detached and must not give a false-positive eligibility test.
        var owner = await db.Users.SingleAsync(x => x.Id == fixture.Owner.Id);
        owner.SetStatus(true);
        await db.SaveChangesAsync();
        Assert.True((await db.Users.AsNoTracking().SingleAsync(x => x.Id == owner.Id)).IsActive);
        var vendor = await db.Vendors.SingleAsync(x => x.Id == fixture.Vendor.Id);
        db.Entry(vendor).Property(x => x.IsActive).CurrentValue = false;
        await db.SaveChangesAsync();
        Assert.False((await db.Vendors.AsNoTracking().SingleAsync(x => x.Id == vendor.Id)).IsActive);
        await Assert.ThrowsAsync<DomainException>(() => create.Handle(fixture.ServiceCommand, default));
        vendor = await db.Vendors.SingleAsync(x => x.Id == fixture.Vendor.Id);
        owner = await db.Users.SingleAsync(x => x.Id == fixture.Owner.Id);
        db.Entry(vendor).Property(x => x.IsActive).CurrentValue = true;
        db.Entry(owner).Property(x => x.Role).CurrentValue = UserRole.Customer;
        await db.SaveChangesAsync();
        Assert.True((await db.Vendors.AsNoTracking().SingleAsync(x => x.Id == vendor.Id)).IsActive);
        Assert.Equal(UserRole.Customer, (await db.Users.AsNoTracking().SingleAsync(x => x.Id == owner.Id)).Role);
        await Assert.ThrowsAsync<DomainException>(() => create.Handle(fixture.ServiceCommand, default));
        owner = await db.Users.SingleAsync(x => x.Id == fixture.Owner.Id);
        db.Entry(owner).Property(x => x.Role).CurrentValue = UserRole.VendorOwner;
        await db.SaveChangesAsync();
        // Concrete category scopes cannot promise ordinary-service basis for an A–Z target.
        var category = await db.Categories.SingleAsync(x => x.Id == fixture.Category.Id);
        db.Entry(category).Property(x => x.RequiresCommissionBasisApproval).CurrentValue = true;
        await db.SaveChangesAsync();
        Assert.Equal(UserRole.VendorOwner, (await db.Users.AsNoTracking().SingleAsync(x => x.Id == owner.Id)).Role);
        Assert.True((await db.Categories.AsNoTracking().SingleAsync(x => x.Id == category.Id)).RequiresCommissionBasisApproval);
        await Assert.ThrowsAsync<DomainException>(() => create.Handle(fixture.ServiceCommand, default));
        var categoryCommand = fixture.ServiceCommand with { Scope = CommissionRuleScope.Category,
            ServiceItemId = null, CategoryId = fixture.Category.Id };
        await Assert.ThrowsAsync<DomainException>(() => create.Handle(categoryCommand, default));
        var az = await create.Handle(categoryCommand with { BasisKind = CommissionBasisKind.AzManagementFee }, default);
        Assert.Equal(CommissionBasisKind.AzManagementFee, az.BasisKind);
    }

    [Fact]
    public Task SqliteSupplement_PolicyOverlapAndHistoryGuards() => PolicyOverlapAndHistoryGuards(false);

    [SqlServerFact]
    public Task SqlServer_PolicyOverlapAndHistoryGuards() => PolicyOverlapAndHistoryGuards(true);

    private static async Task PolicyOverlapAndHistoryGuards(bool sqlServer)
    {
        await using var fixture = await Fixture.Create(sqlServer);
        var db = fixture.Db;
        var actor = new Actor(fixture.Admin.Id, "SuperAdmin");
        var create = new CreateCommissionPolicyRuleCommandHandler(db, actor);
        var submit = new SubmitCommissionPolicyRuleCommandHandler(db, actor);
        var review = new ReviewCommissionPolicyRuleCommandHandler(db, actor);
        var first = await create.Handle(fixture.ServiceCommand with { EffectiveTo = Start.AddDays(10) }, default);
        first = await submit.Handle(new(first.Id, first.Version), default);
        await review.Handle(new(first.Id, first.Version, true, null), default);
        var boundary = await create.Handle(fixture.ServiceCommand with { EffectiveFrom = Start.AddDays(10), EffectiveTo = Start.AddDays(20) }, default);
        boundary = await submit.Handle(new(boundary.Id, boundary.Version), default);
        await Assert.ThrowsAsync<DomainException>(() => review.Handle(new(boundary.Id, boundary.Version, true, null), default));
        await review.Handle(new(boundary.Id, boundary.Version, false, "Inclusive boundary overlaps"), default);
        var next = await create.Handle(fixture.ServiceCommand with { EffectiveFrom = Start.AddDays(11), EffectiveTo = Start.AddDays(20) }, default);
        next = await submit.Handle(new(next.Id, next.Version), default);
        var approved = await review.Handle(new(next.Id, next.Version, true, null), default);
        Assert.Equal(CommissionPolicyStatus.Approved, approved.Status);
        Assert.Equal(2, await db.CommissionPolicyRules.CountAsync(x => x.Status == CommissionPolicyStatus.Approved));

        var row = await db.CommissionPolicyRules.SingleAsync(x => x.Id == first.Id);
        db.Entry(row).Property(x => x.Rate).CurrentValue = .09m;
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
        db.ChangeTracker.Clear();
        Assert.Equal(.08m, (await db.CommissionPolicyRules.AsNoTracking().SingleAsync(x => x.Id == first.Id)).Rate);
        Assert.Equal(0, await db.Commissions.CountAsync());
    }

    [SqlServerFact]
    public async Task SqlServer_PolicyVersionPreventsConcurrentDecisionOverwrite()
    {
        await using var fixture = await Fixture.Create(true);
        var actor = new Actor(fixture.Admin.Id, "SuperAdmin");
        var draft = await new CreateCommissionPolicyRuleCommandHandler(fixture.Db, actor).Handle(fixture.ServiceCommand, default);
        var submitted = await new SubmitCommissionPolicyRuleCommandHandler(fixture.Db, actor).Handle(new(draft.Id, draft.Version), default);
        await using var secondContext = fixture.CreateContext();
        var stale = await secondContext.CommissionPolicyRules.SingleAsync(x => x.Id == submitted.Id);
        await new ReviewCommissionPolicyRuleCommandHandler(fixture.Db, actor).Handle(new(submitted.Id, submitted.Version, true, null), default);
        stale.Reject(fixture.Admin.Id, "Stale competing decision");
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => secondContext.SaveChangesAsync());
        Assert.Equal(CommissionPolicyStatus.Approved,
            (await fixture.Db.CommissionPolicyRules.AsNoTracking().SingleAsync(x => x.Id == submitted.Id)).Status);
    }

    [Fact]
    public Task SqliteSupplement_ComboApprovalRevalidatesApprovedBasisTargets() => ComboApprovalRevalidatesApprovedBasisTargets(false);

    [SqlServerFact]
    public Task SqlServer_ComboApprovalRevalidatesApprovedBasisTargets() => ComboApprovalRevalidatesApprovedBasisTargets(true);

    private static async Task ComboApprovalRevalidatesApprovedBasisTargets(bool sqlServer)
    {
        await using var fixture = await Fixture.Create(sqlServer);
        var db = fixture.Db;
        fixture.Vendor.SetVerified(true);
        fixture.Vendor.ConfigureComboParticipation(true, .10m);
        var second = fixture.AddService("Second component");
        var combo = ComboTemplate.Create("Synthetic fixed combo", "Popular", "Classic", null, null, 0);
        combo.SetScope(fixture.Vendor.Id, "HCM", null, true);
        combo.SetPackagePricing("FixedPrice", 150_000, 200_000);
        db.ComboTemplates.Add(combo);
        var snapshot = JsonSerializer.Serialize(new { Items = new[] {
            new { ServiceItemId = fixture.Service.Id, AllowedItemIds = Array.Empty<Guid>() },
            new { ServiceItemId = second.Id, AllowedItemIds = Array.Empty<Guid>() } } });
        var revision = ComboTemplateRevision.Create(combo.Id, 1, snapshot, fixture.Owner.Id, "VendorOwner", true);
        revision.Review(true, fixture.Admin.Id, "SuperAdmin", "Synthetic approved revision");
        db.ComboTemplateRevisions.Add(revision);
        foreach (var service in new[] { fixture.Service, second })
        {
            var basis = CommissionComponentBasisApproval.Create(revision.Id, service.Id, 1, .08m,
                CommissionBasisKind.StandardService, "Approved service value before VAT", "[]", "Synthetic basis", Start, null, fixture.Admin.Id);
            basis.Submit(); basis.Approve(fixture.Admin.Id);
            db.CommissionComponentBasisApprovals.Add(basis);
        }
        await db.SaveChangesAsync();
        var actor = new Actor(fixture.Admin.Id, "SuperAdmin");
        var rule = await new CreateCommissionPolicyRuleCommandHandler(db, actor).Handle(fixture.ServiceCommand with {
            Scope = CommissionRuleScope.Combo, ServiceItemId = null, ComboTemplateId = combo.Id,
            ComboTemplateRevisionId = revision.Id, Rate = null, BasisKind = CommissionBasisKind.ComboComponents }, default);
        var submitted = await new SubmitCommissionPolicyRuleCommandHandler(db, actor).Handle(new(rule.Id, rule.Version), default);

        second.Update(second.Name, second.BasePrice, second.Unit, second.MinGuests, second.MaxGuests,
            second.Regions, second.CancellationPolicy, false);
        await db.SaveChangesAsync();
        var review = new ReviewCommissionPolicyRuleCommandHandler(db, actor);
        await Assert.ThrowsAsync<DomainException>(() => review.Handle(new(submitted.Id, submitted.Version, true, null), default));
        Assert.Equal(CommissionPolicyStatus.Submitted,
            (await db.CommissionPolicyRules.AsNoTracking().SingleAsync(x => x.Id == submitted.Id)).Status);
        // Rejection remains available even though the previously approved component is now ineligible.
        var rejected = await review.Handle(new(submitted.Id, submitted.Version, false, "Component is unavailable"), default);
        Assert.Equal(CommissionPolicyStatus.Rejected, rejected.Status);
        Assert.Equal(2, await db.CommissionComponentBasisApprovals.CountAsync(x => x.Status == CommissionPolicyStatus.Approved));
        Assert.Equal(0, await db.Commissions.CountAsync());
    }

    [Fact]
    public Task SqliteSupplement_ComboCoverageUsesExactRevisionAndIncludesAlternatives() => ComboCoverageUsesExactRevisionAndIncludesAlternatives(false);

    [SqlServerFact]
    public Task SqlServer_ComboCoverageUsesExactRevisionAndIncludesAlternatives() => ComboCoverageUsesExactRevisionAndIncludesAlternatives(true);

    private static async Task ComboCoverageUsesExactRevisionAndIncludesAlternatives(bool sqlServer)
    {
        await using var fixture = await Fixture.Create(sqlServer);
        var db = fixture.Db;
        fixture.Vendor.SetVerified(true);
        fixture.Vendor.ConfigureComboParticipation(true, .10m);
        var second = fixture.AddService("Second component");
        var alternative = fixture.AddService("Approved historical alternative");
        var combo = ComboTemplate.Create("Pinned combo", "Popular", "Classic", null, null, 0);
        combo.SetScope(fixture.Vendor.Id, "HCM", null, true);
        combo.SetPackagePricing("FixedPrice", 150_000, 200_000);
        db.ComboTemplates.Add(combo);
        // Exact approved historical snapshots may retain alternatives; every selectable ID needs its own approval.
        var snapshot = JsonSerializer.Serialize(new { Items = new[] {
            new { ServiceItemId = fixture.Service.Id, AllowedItemIds = new[] { alternative.Id } },
            new { ServiceItemId = second.Id, AllowedItemIds = Array.Empty<Guid>() } } });
        var revision = fixture.AddApprovedRevision(combo, 1, snapshot);
        var otherRevision = fixture.AddApprovedRevision(combo, 2, snapshot);
        fixture.AddApprovedBasis(revision.Id, fixture.Service.Id);
        fixture.AddApprovedBasis(revision.Id, second.Id);
        fixture.AddApprovedBasis(otherRevision.Id, alternative.Id);
        await db.SaveChangesAsync();
        var actor = new Actor(fixture.Admin.Id, "SuperAdmin");
        var create = new CreateCommissionPolicyRuleCommandHandler(db, actor);
        var command = fixture.ServiceCommand with { Scope = CommissionRuleScope.Combo, ServiceItemId = null,
            ComboTemplateId = combo.Id, ComboTemplateRevisionId = revision.Id, Rate = null, BasisKind = CommissionBasisKind.ComboComponents };
        await Assert.ThrowsAsync<DomainException>(() => create.Handle(command with { ComboTemplateRevisionId = Guid.NewGuid() }, default));
        var created = await create.Handle(command, default);
        var submitted = await new SubmitCommissionPolicyRuleCommandHandler(db, actor).Handle(new(created.Id, created.Version), default);
        var review = new ReviewCommissionPolicyRuleCommandHandler(db, actor);
        var missing = await Assert.ThrowsAsync<DomainException>(() => review.Handle(new(submitted.Id, submitted.Version, true, null), default));
        Assert.Contains("thiếu basis/rate", missing.Message);
        // A basis for a different revision is not evidence for the pinned revision. Nor is a shorter effective window.
        fixture.AddApprovedBasis(revision.Id, alternative.Id, 1, Start, Start.AddDays(2));
        await db.SaveChangesAsync();
        await Assert.ThrowsAsync<DomainException>(() => review.Handle(new(submitted.Id, submitted.Version, true, null), default));
        fixture.AddApprovedBasis(revision.Id, alternative.Id, 2, Start.AddDays(3), null);
        await db.SaveChangesAsync();
        // Disjoint partial windows must not be guessed into one continuous approval covering the whole policy.
        await Assert.ThrowsAsync<DomainException>(() => review.Handle(new(submitted.Id, submitted.Version, true, null), default));
        // Use a fresh revision rather than overwrite or expand the existing immutable basis records.
        var completeRevision = fixture.AddApprovedRevision(combo, 3, snapshot);
        foreach (var service in new[] { fixture.Service, second, alternative }) fixture.AddApprovedBasis(completeRevision.Id, service.Id);
        await db.SaveChangesAsync();
        var complete = await create.Handle(command with { ComboTemplateRevisionId = completeRevision.Id }, default);
        complete = await new SubmitCommissionPolicyRuleCommandHandler(db, actor).Handle(new(complete.Id, complete.Version), default);
        var approved = await review.Handle(new(complete.Id, complete.Version, true, null), default);
        Assert.Equal(CommissionPolicyStatus.Approved, approved.Status);
        Assert.Null(approved.Rate);
        Assert.Equal(completeRevision.Id, approved.ComboTemplateRevisionId);
        Assert.Equal(0, await db.Commissions.CountAsync());
    }

    private sealed record Actor(Guid? UserId, string? Role) : ICurrentUserService
    {
        public string? Email => null;
    }

    private sealed class Fixture(ApplicationDbContext db, IAsyncDisposable resource) : IAsyncDisposable
    {
        public ApplicationDbContext Db => db;
        public User Admin = null!;
        public User Finance = null!;
        public User Owner = null!;
        public Vendor Vendor = null!;
        public Category Category = null!;
        public ServiceItem Service = null!;
        public CreateCommissionPolicyRuleCommand ServiceCommand => new(CommissionRuleScope.Service, null, null,
            Service.Id, null, null, .08m, CommissionBasisKind.StandardService, "Synthetic v1", "Synthetic test policy", Start, null);

        public static async Task<Fixture> Create(bool sqlServer)
        {
            ApplicationDbContext db;
            IAsyncDisposable resource;
            if (sqlServer)
            {
                var sql = await CommissionSqlTestDatabase.CreateAsync();
                db = sql.Db; resource = sql;
            }
            else
            {
                var connection = new SqliteConnection("Data Source=:memory:");
                await connection.OpenAsync();
                db = new(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options);
                await db.Database.EnsureCreatedAsync();
                resource = connection;
            }
            var fixture = new Fixture(db, resource);
            fixture.Admin = fixture.AddUser(UserRole.SuperAdmin);
            fixture.Finance = fixture.AddUser(UserRole.Finance);
            fixture.Owner = fixture.AddUser(UserRole.VendorOwner);
            fixture.Vendor = Vendor.Create(fixture.Owner.Id, "Policy test vendor", $"policy-{Guid.NewGuid():N}",
                "Synthetic owner", "0900000000", "HCM", .08m);
            db.Vendors.Add(fixture.Vendor);
            fixture.Category = Category.Create("Policy category", $"policy-{Guid.NewGuid():N}", null, null, .08m);
            db.Categories.Add(fixture.Category);
            fixture.Service = fixture.AddService("Policy service");
            await db.SaveChangesAsync();
            return fixture;
        }

        public ServiceItem AddService(string name)
        {
            var listing = Listing.Create(Vendor.Id, Category.Id, name, $"policy-{Guid.NewGuid():N}", 100_000, 100_000, "Synthetic", "HCM");
            listing.Approve(); Db.Listings.Add(listing);
            var service = ServiceItem.Create(Vendor.Id, listing.Id, Category.Id, name, 100_000, "Package", 1, 100, "HCM", "Synthetic terms");
            service.Submit(); service.Review(true, null); Db.ServiceItems.Add(service);
            return service;
        }

        public ComboTemplateRevision AddApprovedRevision(ComboTemplate combo, int sequence, string snapshot)
        {
            var revision = ComboTemplateRevision.Create(combo.Id, sequence, snapshot, Owner.Id, "VendorOwner", true);
            revision.Review(true, Admin.Id, "SuperAdmin", "Synthetic approved revision");
            Db.ComboTemplateRevisions.Add(revision);
            return revision;
        }

        public void AddApprovedBasis(Guid revisionId, Guid serviceId, int sequence = 1, DateTime? from = null, DateTime? to = null)
        {
            var basis = CommissionComponentBasisApproval.Create(revisionId, serviceId, sequence, .08m,
                CommissionBasisKind.StandardService, "Approved service value before VAT", "[]", "Synthetic basis", from ?? Start, to, Admin.Id);
            basis.Submit(); basis.Approve(Admin.Id); Db.CommissionComponentBasisApprovals.Add(basis);
        }

        public ApplicationDbContext CreateContext() => resource is CommissionSqlTestDatabase sql ? sql.CreateContext() :
            new(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(db.Database.GetDbConnection()).Options);

        private User AddUser(UserRole role)
        {
            var user = User.Create("Synthetic policy actor", $"{Guid.NewGuid():N}@policy.example.invalid", "0900000000", "not-a-real-login", role);
            Db.Users.Add(user); return user;
        }

        public async ValueTask DisposeAsync()
        {
            if (resource is not CommissionSqlTestDatabase) await db.DisposeAsync();
            await resource.DisposeAsync();
        }
    }
}
