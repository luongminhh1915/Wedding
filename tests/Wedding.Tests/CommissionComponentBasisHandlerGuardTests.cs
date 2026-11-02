using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Wedding.Application.Common.Interfaces;
using Wedding.Application.Features.CommissionPolicies;
using Wedding.Domain.Entities;
using Wedding.Domain.Enums;
using Wedding.Domain.Exceptions;
using Wedding.Infrastructure.Persistence;

namespace Wedding.Tests;

public class CommissionComponentBasisHandlerGuardTests
{
    private static readonly DateTime Start = new(2027, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    [SqlServerFact]
    public async Task BasisKindMatchesCategoryOrGroupFlag_OrdinaryDoesNotUseAzFee()
    {
        await using var owned = await CommissionSqlTestDatabase.CreateAsync();
        var db = owned.Db;
        foreach (var flags in new[] { (Category: true, Group: false), (Category: false, Group: true), (Category: false, Group: false) })
        {
            var seed = await SeedAsync(db, flags.Category, flags.Group);
            var handler = new CreateCommissionComponentBasisCommandHandler(db, new Actor(seed.AdminId, "SuperAdmin"));
            var expected = flags.Category || flags.Group ? CommissionBasisKind.AzManagementFee : CommissionBasisKind.StandardService;
            var wrong = expected == CommissionBasisKind.AzManagementFee ? CommissionBasisKind.StandardService : CommissionBasisKind.AzManagementFee;

            await Assert.ThrowsAsync<DomainException>(() => handler.Handle(Create(seed, wrong), default));
            var basis = await handler.Handle(Create(seed, expected), default);
            Assert.Equal(expected, basis.BasisKind);
            Assert.Equal(CommissionPolicyStatus.Draft, basis.Status);
        }
    }

    [SqlServerFact]
    public async Task SubmitAndApproveRevalidateBasisKindAfterCategoryChanges()
    {
        await using var owned = await CommissionSqlTestDatabase.CreateAsync();
        var db = owned.Db;
        var seed = await SeedAsync(db);
        var actor = new Actor(seed.AdminId, "SuperAdmin");
        var create = new CreateCommissionComponentBasisCommandHandler(db, actor);
        var standard = await create.Handle(Create(seed, CommissionBasisKind.StandardService), default);

        await SetCategoryBasisFlagAsync(db, seed.CategoryId, true);
        await Assert.ThrowsAsync<DomainException>(() => new SubmitCommissionComponentBasisCommandHandler(db, actor)
            .Handle(new(standard.Id, standard.Version), default));

        var az = await create.Handle(Create(seed, CommissionBasisKind.AzManagementFee), default);
        var submitted = await new SubmitCommissionComponentBasisCommandHandler(db, actor).Handle(new(az.Id, az.Version), default);
        await SetCategoryBasisFlagAsync(db, seed.CategoryId, false);
        await Assert.ThrowsAsync<DomainException>(() => new ReviewCommissionComponentBasisCommandHandler(db, actor)
            .Handle(new(submitted.Id, submitted.Version, true, null), default));
        var pending = await db.CommissionComponentBasisApprovals.AsNoTracking().SingleAsync(row => row.Id == submitted.Id);
        Assert.Equal(CommissionPolicyStatus.Submitted, pending.Status);
    }

    [SqlServerFact]
    public async Task InactiveComboCannotBeApprovedButCanBeRejectedWithHistoryAndVersionChecks()
    {
        await using var owned = await CommissionSqlTestDatabase.CreateAsync();
        var db = owned.Db;
        var seed = await SeedAsync(db);
        var actor = new Actor(seed.AdminId, "SuperAdmin");
        var basis = await new CreateCommissionComponentBasisCommandHandler(db, actor)
            .Handle(Create(seed, CommissionBasisKind.StandardService), default);
        var submitted = await new SubmitCommissionComponentBasisCommandHandler(db, actor).Handle(new(basis.Id, basis.Version), default);
        var combo = await db.ComboTemplates.SingleAsync(row => row.Id == seed.ComboId);
        combo.Archive();
        await db.SaveChangesAsync();

        var review = new ReviewCommissionComponentBasisCommandHandler(db, actor);
        await Assert.ThrowsAsync<DomainException>(() => review.Handle(new(submitted.Id, submitted.Version, true, null), default));
        await Assert.ThrowsAsync<DomainException>(() => review.Handle(new(submitted.Id, Guid.NewGuid(), false, "Stale request"), default));
        var rejected = await review.Handle(new(submitted.Id, submitted.Version, false, "Combo was withdrawn"), default);
        Assert.Equal(CommissionPolicyStatus.Rejected, rejected.Status);
        Assert.Equal("Combo was withdrawn", rejected.RejectionReason);
        Assert.Equal(seed.AdminId, rejected.ApprovedById);
        Assert.NotNull(rejected.ApprovedAt);
        Assert.NotEqual(submitted.Version, rejected.Version);
        Assert.Equal(submitted.BasisDefinition, rejected.BasisDefinition);
        Assert.Equal(submitted.ExcludedCostsJson, rejected.ExcludedCostsJson);
        Assert.True(await db.AuditLogs.AnyAsync(log => log.Action == "CommissionComponentBasis.Rejected" && log.EntityId == basis.Id.ToString()));
        await Assert.ThrowsAsync<DomainException>(() => review.Handle(new(rejected.Id, rejected.Version, false, "Second decision"), default));
    }

    [SqlServerFact]
    public async Task ChangedVendorOwnerRoleBlocksCreateSubmitAndApproveButNotRejection()
    {
        await using var owned = await CommissionSqlTestDatabase.CreateAsync();
        var db = owned.Db;
        var seed = await SeedAsync(db);
        var actor = new Actor(seed.AdminId, "SuperAdmin");
        var create = new CreateCommissionComponentBasisCommandHandler(db, actor);
        var draft = await create.Handle(Create(seed, CommissionBasisKind.StandardService), default);
        var otherDraft = await create.Handle(Create(seed, CommissionBasisKind.StandardService), default);
        var submit = new SubmitCommissionComponentBasisCommandHandler(db, actor);
        var pending = await submit.Handle(new(otherDraft.Id, otherDraft.Version), default);
        var owner = await db.Users.SingleAsync(row => row.Id == seed.OwnerId);
        db.Entry(owner).Property(row => row.Role).CurrentValue = UserRole.Customer;
        await db.SaveChangesAsync();

        await Assert.ThrowsAsync<DomainException>(() => create.Handle(Create(seed, CommissionBasisKind.StandardService), default));
        await Assert.ThrowsAsync<DomainException>(() => submit.Handle(new(draft.Id, draft.Version), default));
        var review = new ReviewCommissionComponentBasisCommandHandler(db, actor);
        await Assert.ThrowsAsync<DomainException>(() => review.Handle(new(pending.Id, pending.Version, true, null), default));
        var rejected = await review.Handle(new(pending.Id, pending.Version, false, "Shop account role changed"), default);
        Assert.Equal(CommissionPolicyStatus.Rejected, rejected.Status);
    }

    [SqlServerFact]
    public async Task ApprovedBasisDefinitionExclusionsRateAndHistoryCannotBeRewrittenOrDeleted()
    {
        await using var owned = await CommissionSqlTestDatabase.CreateAsync();
        var db = owned.Db;
        var seed = await SeedAsync(db);
        var actor = new Actor(seed.AdminId, "SuperAdmin");
        var draft = await new CreateCommissionComponentBasisCommandHandler(db, actor)
            .Handle(Create(seed, CommissionBasisKind.StandardService), default);
        var pending = await new SubmitCommissionComponentBasisCommandHandler(db, actor).Handle(new(draft.Id, draft.Version), default);
        var approved = await new ReviewCommissionComponentBasisCommandHandler(db, actor)
            .Handle(new(pending.Id, pending.Version, true, null), default);

        foreach (var change in new (string Name, object Value)[]
        {
            (nameof(CommissionComponentBasisApproval.BasisDefinition), "Rewritten basis"),
            (nameof(CommissionComponentBasisApproval.ExcludedCostsJson), "[\"Unapproved exclusion\"]"),
            (nameof(CommissionComponentBasisApproval.Rate), .07m)
        })
        {
            db.ChangeTracker.Clear();
            var row = await db.CommissionComponentBasisApprovals.SingleAsync(item => item.Id == approved.Id);
            db.Entry(row).Property(change.Name).CurrentValue = change.Value;
            await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
        }
        db.ChangeTracker.Clear();
        var immutable = await db.CommissionComponentBasisApprovals.SingleAsync(item => item.Id == approved.Id);
        Assert.Equal(approved.BasisDefinition, immutable.BasisDefinition);
        Assert.Equal(approved.ExcludedCostsJson, immutable.ExcludedCostsJson);
        Assert.Equal(approved.Rate, immutable.Rate);
        db.CommissionComponentBasisApprovals.Remove(immutable);
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
    }

    private static CreateCommissionComponentBasisCommand Create(Seed seed, CommissionBasisKind kind) =>
        new(seed.RevisionId, seed.ServiceId, .08m, kind, "Only approved vendor service or A–Z management fee basis",
            "[]", "Synthetic target validation", Start, null);

    private static async Task SetCategoryBasisFlagAsync(ApplicationDbContext db, Guid id, bool flag)
    {
        var category = await db.Categories.SingleAsync(row => row.Id == id);
        db.Entry(category).Property(row => row.RequiresCommissionBasisApproval).CurrentValue = flag;
        await db.SaveChangesAsync();
    }

    private static async Task<Seed> SeedAsync(ApplicationDbContext db, bool categoryFlag = false, bool groupFlag = false)
    {
        User AddUser(UserRole role)
        {
            var user = User.Create("Synthetic basis actor", $"{Guid.NewGuid():N}@basis.example.invalid", "0900000000", "synthetic-not-a-login", role);
            db.Users.Add(user);
            return user;
        }
        var owner = AddUser(UserRole.VendorOwner);
        var admin = AddUser(UserRole.SuperAdmin);
        var vendor = Vendor.Create(owner.Id, "Synthetic basis vendor", $"basis-{Guid.NewGuid():N}", "Synthetic owner", "0900000000", "HCM", .08m);
        vendor.SetVerified(true);
        vendor.ConfigureComboParticipation(true, 0m);
        db.Vendors.Add(vendor);
        var category = Category.Create("Synthetic basis category", $"basis-{Guid.NewGuid():N}", null, null, .08m);
        db.Categories.Add(category);
        db.Entry(category).Property(row => row.RequiresCommissionBasisApproval).CurrentValue = categoryFlag;
        if (groupFlag) db.Entry(category).Property(row => row.BusinessGroupId).CurrentValue = BusinessGroupPolicy.FullServicePlanningId;
        var listing = Listing.Create(vendor.Id, category.Id, "Synthetic basis service", $"basis-{Guid.NewGuid():N}", 100_000, 100_000, "Synthetic service", "HCM");
        listing.Approve();
        db.Listings.Add(listing);
        var service = ServiceItem.Create(vendor.Id, listing.Id, category.Id, "Synthetic basis component", 100_000, "Package", 1, 100, "HCM", "Synthetic cancellation policy");
        service.Submit();
        service.Review(true, null);
        db.ServiceItems.Add(service);
        var combo = ComboTemplate.Create("Synthetic basis combo", "Popular", "Classic", null, null, 1);
        combo.SetScope(vendor.Id, "HCM", 0m, true);
        combo.SetPackagePricing("FixedPrice", 100_000, 100_000);
        db.ComboTemplates.Add(combo);
        var snapshot = JsonSerializer.Serialize(new { Items = new[] { new { ServiceItemId = service.Id, AllowedItemIds = Array.Empty<Guid>() } } });
        var revision = ComboTemplateRevision.Create(combo.Id, 1, snapshot, owner.Id, "VendorOwner", true);
        revision.Review(true, admin.Id, "SuperAdmin", "Synthetic approval");
        db.ComboTemplateRevisions.Add(revision);
        await db.SaveChangesAsync();
        return new(admin.Id, owner.Id, category.Id, service.Id, combo.Id, revision.Id);
    }

    private sealed record Seed(Guid AdminId, Guid OwnerId, Guid CategoryId, Guid ServiceId, Guid ComboId, Guid RevisionId);
    private sealed record Actor(Guid? UserId, string? Role) : ICurrentUserService
    {
        public string? Email => null;
    }
}
