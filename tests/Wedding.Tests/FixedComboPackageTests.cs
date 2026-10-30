using System.Text.Json;
using Microsoft.Data.SqlClient;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Wedding.Application.Common.Interfaces;
using Wedding.Application.Features.Combos;
using Wedding.Domain.Entities;
using Wedding.Domain.Enums;
using Wedding.Domain.Exceptions;
using Wedding.Infrastructure.Persistence;

namespace Wedding.Tests;

public class FixedComboPackageTests
{
    [Theory]
    [InlineData("one")] [InlineData("duplicate")] [InlineData("foreign")]
    [InlineData("draft")] [InlineData("inactive")] [InlineData("noncanonical")]
    [InlineData("same-listing")] [InlineData("guest-unit")] [InlineData("nested-id")]
    [InlineData("optional")] [InlineData("alternative")] [InlineData("dynamic-bypass")]
    public async Task NewFixedFlowRejectsInvalidComponentsOnServer(string invalid)
    {
        await using var fixture = await Fixture.Create();
        var command = fixture.Command;
        switch (invalid)
        {
            case "one": command = command with { Items = [fixture.Slot(fixture.First)] }; break;
            case "duplicate": command = command with { Items = [fixture.Slot(fixture.First), fixture.Slot(fixture.First)] }; break;
            case "foreign": command = command with { Items = [fixture.Slot(fixture.First), fixture.Slot(fixture.Foreign)] }; break;
            case "draft": fixture.Second.Update("Changed", 2_000_000, "Package", 1, 1000, "HCM", "Terms", true); break;
            case "inactive": fixture.Second.Listing.SetActivity(false); break;
            case "noncanonical": command = command with { Items = [fixture.Slot(fixture.First), fixture.Slot(fixture.Legacy)] }; break;
            case "same-listing":
                var sibling = ServiceItem.Create(fixture.Vendor.Id, fixture.First.ListingId, fixture.First.CategoryId, "Sibling facet", 2_000_000, "Package", 1, 1000, "HCM", "Terms");
                sibling.Submit(); sibling.Review(true, null); fixture.Db.ServiceItems.Add(sibling);
                command = command with { Items = [fixture.Slot(fixture.First), fixture.Slot(sibling)] }; break;
            case "guest-unit": fixture.Second.Update(fixture.Second.Name, 200_000, "Guest", 1, 1000, "HCM", "Terms", true); fixture.Second.Submit(); fixture.Second.Review(true, null); break;
            case "nested-id": command = command with { Items = [fixture.Slot(fixture.First), fixture.Slot(fixture.Second) with { ServiceItemId = Guid.NewGuid() }] }; break;
            case "optional": command = command with { Items = [fixture.Slot(fixture.First), fixture.Slot(fixture.Second) with { IsOptional = true }] }; break;
            case "alternative": command = command with { Items = [fixture.Slot(fixture.First), fixture.Slot(fixture.Second) with { AllowedItemIds = [fixture.First.Id] }] }; break;
            case "dynamic-bypass": command = command with { PricingMode = "Dynamic", ComboPrice = null, Items = [fixture.Slot(fixture.First)] }; break;
        }
        await fixture.Db.SaveChangesAsync();
        await Assert.ThrowsAsync<DomainException>(() => fixture.Save(command));
        Assert.Empty(await fixture.Db.ComboTemplates.ToListAsync());
        Assert.Empty(await fixture.Db.ComboTemplateRevisions.ToListAsync());
    }

    [Fact]
    public async Task SameCategoryServicesCanBeReusedInSeveralModeratedPackagesWithoutCopyingServices()
    {
        await using var fixture = await Fixture.Create();
        var count = await fixture.Db.ServiceItems.CountAsync();
        var draft = await fixture.Save(fixture.Command with { SubmitForApproval = false });
        Assert.Equal("FixedPrice", draft.PricingMode); Assert.Equal(3_000_000, draft.OriginalPrice); Assert.Equal(2_500_000, draft.ComboPrice);
        Assert.Equal(500_000, draft.SavingsAmount); Assert.Equal(2, draft.Components!.Count);
        Assert.Empty((await fixture.Builder()).Templates);
        var submitted = await new SubmitComboTemplateCommandHandler(fixture.Db, fixture.OwnerActor, new Audit()).Handle(new(draft.Id), default);
        Assert.Equal("Pending", submitted.LatestRevision!.Status);
        await Assert.ThrowsAsync<DomainException>(() => fixture.Review(draft.Id, fixture.OwnerActor));
        await fixture.Review(draft.Id);
        var second = await fixture.Publish(fixture.Command with { Name = "Another fixed package" });
        Assert.Equal(count, await fixture.Db.ServiceItems.CountAsync());
        Assert.Equal(2, (await fixture.Builder()).Templates.Count);
        Assert.Equal(4, await fixture.Db.ComboTemplateItems.CountAsync());
        var catalog = await new GetComboCatalogPageQueryHandler(fixture.Db).Handle(new(TemplateId: second.Id, Guests: 250), default);
        Assert.Equal(2_500_000, catalog.Estimates!.Single(estimate => estimate.TemplateId == second.Id).Total);
        Assert.Equal(3_000_000, catalog.SelectedTemplate!.OriginalPrice);
        Assert.All(catalog.SelectedOptions, option => Assert.Empty(option.Variants));
        var price = await fixture.Price(second.Id);
        Assert.Equal("FixedPrice", price.PricingMode); Assert.Equal(3_000_000, price.Subtotal); Assert.Equal(2_500_000, price.Total);
        Assert.Equal(500_000, price.Lines.Sum(line => line.DiscountShare));
        Assert.Single(price.Lines.Select(line => line.CategoryId).Distinct());
        Assert.All(price.Lines, line => Assert.Equal(1, line.Quantity));
    }

    [Theory]
    [InlineData("missing")] [InlineData("extra")] [InlineData("variant")] [InlineData("addon")] [InlineData("hours")]
    public async Task PublicFixedPriceCannotBeUsedForModifiedComposition(string change)
    {
        await using var fixture = await Fixture.Create(); var package = await fixture.Publish();
        var selections = fixture.Selections;
        if (change == "missing") selections.RemoveAt(1);
        if (change == "extra") selections.Add(new(fixture.Legacy.Id, null, null));
        if (change == "variant") selections[0] = selections[0] with { VariantIds = [Guid.NewGuid()] };
        if (change == "addon") selections[0] = selections[0] with { AddOnIds = [Guid.NewGuid()] };
        if (change == "hours") selections[0] = selections[0] with { Hours = 2 };
        await Assert.ThrowsAsync<DomainException>(() => ComboPricing.CalculateAsync(fixture.Db,
            new(package.Id, "HCM", fixture.Date, 100, selections), null, default));
    }

    [Fact]
    public async Task ApprovedSourceRevisionHoldsPublicPackageAndDoesNotRepriceOldOrderSnapshots()
    {
        await using var fixture = await Fixture.Create(); var package = await fixture.Publish();
        var order = await new SubmitComboOrderCommandHandler(fixture.Db, fixture.CustomerActor, new Notify())
            .Handle(new(package.Id, [], fixture.Date, 100, "0901234567", "Original order", "HCM", fixture.Selections, 2_500_000), default);
        Assert.Equal(2_500_000, order.Total); Assert.Equal(2, order.Items.Count); Assert.All(order.Items, item => Assert.NotNull(item.Component));
        var originalOrder = await fixture.OrderSnapshot(order.Id);
        var oldPackage = await fixture.Db.ComboTemplates.AsNoTracking().Include(template => template.Items).SingleAsync(template => template.Id == package.Id);
        var originalComponents = oldPackage.Items.Select(item => item.ComponentSnapshotJson).OrderBy(json => json).ToList();
        fixture.Second.Update("Changed approved name", 4_000_000, "Package", 1, 1000, "HCM", "New cancellation terms", true);
        fixture.Second.Submit(); fixture.Second.Review(true, null); await fixture.Db.SaveChangesAsync();
        Assert.Empty((await fixture.Builder()).Templates);
        var catalog = await new GetComboCatalogPageQueryHandler(fixture.Db).Handle(new(TemplateId: package.Id), default);
        Assert.Null(catalog.SelectedTemplate);
        await Assert.ThrowsAsync<DomainException>(() => fixture.Price(package.Id));
        var own = (await new GetAllComboTemplatesQueryHandler(fixture.Db, fixture.OwnerActor).Handle(new(true), default)).Single();
        Assert.True(own.RequiresComponentReview); Assert.Equal(3_000_000, own.OriginalPrice); Assert.Equal(2_500_000, own.ComboPrice);
        Assert.Contains(own.Components!, component => component.Name == "Second service" && component.BasePrice == 2_000_000);
        Assert.Equal(originalOrder, await fixture.OrderSnapshot(order.Id));
        Assert.Equal(originalComponents, (await fixture.Db.ComboTemplateItems.AsNoTracking().Where(item => item.ComboTemplateId == package.Id).ToListAsync()).Select(item => item.ComponentSnapshotJson).OrderBy(json => json));
        var proposal = await fixture.Save(fixture.Command with { Id = package.Id });
        await fixture.Review(package.Id);
        var refreshed = Assert.Single((await fixture.Builder()).Templates);
        Assert.Equal(5_000_000, refreshed.OriginalPrice); Assert.Equal(2_500_000, refreshed.ComboPrice);
        Assert.False((await new GetAllComboTemplatesQueryHandler(fixture.Db, fixture.OwnerActor).Handle(new(true), default)).Single().RequiresComponentReview);
        Assert.Equal(originalOrder, await fixture.OrderSnapshot(order.Id));
    }

    [Fact]
    public async Task SameCategoryPackageClaimsWizardSeedOnlyOnceAndCancellationRestoresOriginalEstimate()
    {
        await using var fixture = await Fixture.Create();
        var seed = BudgetItem.Create(fixture.CustomerActor.UserId!.Value, fixture.First.CategoryId, "Chụp ảnh cưới", 8_000_000);
        seed.MarkPlanTemplate("photo"); fixture.Db.BudgetItems.Add(seed); await fixture.Db.SaveChangesAsync();
        var package = await fixture.Publish();
        var order = await new SubmitComboOrderCommandHandler(fixture.Db, fixture.CustomerActor, new Notify())
            .Handle(new(package.Id, [], fixture.Date, 100, "0901234567", null, "HCM", fixture.Selections, 2_500_000), default);
        var lines = await fixture.Db.ComboOrderItems.AsNoTracking().Where(line => line.ComboOrderId == order.Id).ToListAsync();
        Assert.Equal(2, lines.Select(line => line.BudgetItemId).Distinct().Count());
        Assert.Single(lines, line => line.BudgetItemId == seed.Id);
        var reserved = await fixture.Db.BudgetItems.AsNoTracking().ToListAsync();
        Assert.Equal(2, reserved.Count); Assert.Equal(order.Total, reserved.Sum(row => row.PlannedCost));
        foreach (var line in lines)
            Assert.Equal(line.Price - line.DiscountShare, reserved.Single(row => row.Id == line.BudgetItemId).PlannedCost);
        await new CancelComboOrderCommandHandler(fixture.Db, fixture.CustomerActor)
            .Handle(new(order.Id), default);
        var restored = Assert.Single(await fixture.Db.BudgetItems.AsNoTracking().ToListAsync());
        Assert.Equal(seed.Id, restored.Id); Assert.Equal(8_000_000, restored.PlannedCost); Assert.Equal("photo", restored.PlanTemplateKey);
    }

    [Fact]
    public async Task ComponentContentChangesAfterSubmissionRequireANewReviewedSnapshot()
    {
        await using var fixture = await Fixture.Create(); var pending = await fixture.Save(fixture.Command);
        fixture.Second.Listing.Update("Changed title", 2_000_000, 2_000_000, "New description", "HCM");
        await fixture.Db.SaveChangesAsync();
        await Assert.ThrowsAsync<DomainException>(() => fixture.Review(pending.Id));
        Assert.Empty((await fixture.Builder()).Templates);
        Assert.Equal("Pending", (await fixture.Db.ComboTemplateRevisions.AsNoTracking().SingleAsync()).Status);
        await fixture.Save(fixture.Command with { Id = pending.Id }); await fixture.Review(pending.Id);
        Assert.Contains(Assert.Single((await fixture.Builder()).Templates).Components!, component => component.ListingTitle == "Changed title");
    }

    [Fact]
    public async Task MarkupPackageShowsFixedPriceWithoutSavingsButRequiresSeparateQuote()
    {
        await using var fixture = await Fixture.Create(); var package = await fixture.Publish(fixture.Command with { ComboPrice = 3_500_000 });
        var published = Assert.Single((await fixture.Builder()).Templates); Assert.Equal(3_500_000, published.ComboPrice);
        Assert.Null(published.SavingsAmount); Assert.Null(published.SavingsPercentage);
        var error = await Assert.ThrowsAsync<DomainException>(() => fixture.Price(package.Id));
        Assert.Contains("báo giá riêng", error.Message);
    }

    [Fact]
    public async Task AStaffAuthorCannotApproveTheirOwnFixedProposal()
    {
        await using var fixture = await Fixture.Create();
        var saved = await new SaveComboTemplateCommandHandler(fixture.Db, new Audit(), fixture.ModeratorActor).Handle(fixture.Command, default);
        await Assert.ThrowsAsync<DomainException>(() => fixture.Review(saved.Id));
        Assert.Equal("Pending", (await fixture.Db.ComboTemplateRevisions.AsNoTracking().SingleAsync()).Status);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task FixedDraftOrPendingProposalCannotDowngradeToOneServiceDynamicCreation(bool submit)
    {
        await using var fixture = await Fixture.Create();
        var saved = await fixture.Save(fixture.Command with { SubmitForApproval = submit });
        var originalRevision = await fixture.Db.ComboTemplateRevisions.AsNoTracking().SingleAsync();
        await Assert.ThrowsAsync<DomainException>(() => fixture.Save(fixture.Command with
        {
            Id = saved.Id, PricingMode = "Dynamic", ComboPrice = null, Items = [fixture.Slot(fixture.First)]
        }));
        var unchanged = await fixture.Db.ComboTemplateRevisions.AsNoTracking().SingleAsync();
        Assert.Equal(originalRevision.Id, unchanged.Id); Assert.Equal(originalRevision.SnapshotJson, unchanged.SnapshotJson);
        Assert.Empty((await fixture.Builder()).Templates);
    }

    [Fact]
    public async Task RealLegacyDynamicCanStillBeRevisedButItsNewFixedDraftCannotDowngrade()
    {
        await using var fixture = await Fixture.Create();
        var legacy = ComboTemplate.Create("Existing legacy dynamic", "Popular", "Classic", null, null, 0);
        legacy.StageUnpublished(fixture.Vendor.Id, "Draft"); fixture.Db.ComboTemplates.Add(legacy); await fixture.Db.SaveChangesAsync();
        var legacyCommand = fixture.Command with { Id = legacy.Id, PricingMode = "Dynamic", ComboPrice = null, Items = [fixture.Slot(fixture.First)] };
        var saved = await fixture.Save(legacyCommand); await fixture.Review(saved.Id);
        var published = Assert.Single((await fixture.Builder()).Templates);
        Assert.Equal("Dynamic", published.PricingMode); Assert.Single(published.Items); Assert.Null(published.ComboPrice);
        var fixedDraft = await fixture.Save(fixture.Command with { Id = legacy.Id, SubmitForApproval = false });
        Assert.Equal("FixedPrice", fixedDraft.LatestRevision!.Proposal!.PricingMode);
        Assert.Equal("Dynamic", fixedDraft.PricingMode); // Published legacy version is still intact.
        await Assert.ThrowsAsync<DomainException>(() => fixture.Save(legacyCommand));
        Assert.Equal(2, await fixture.Db.ComboTemplateRevisions.CountAsync());
        Assert.Equal("Dynamic", Assert.Single((await fixture.Builder()).Templates).PricingMode);
    }

    [Theory]
    [InlineData(0)] [InlineData(-1)]
    public async Task FixedPackagePriceMustBePositive(decimal comboPrice)
    {
        await using var fixture = await Fixture.Create();
        await Assert.ThrowsAsync<DomainException>(() => fixture.Save(fixture.Command with { ComboPrice = comboPrice }));
        Assert.Empty(await fixture.Db.ComboTemplates.ToListAsync());
    }

    [SqlServerFact]
    public async Task SqlAdditiveUpgradePreservesLegacyModeAndRunsFixedSameCategoryOrderWithoutModelDrift()
    {
        var database = $"WeddingPlanTest_FixedCombo_{Guid.NewGuid():N}";
        var source = Environment.GetEnvironmentVariable("WEDDING_TEST_SQLSERVER");
        var connection = new SqlConnectionStringBuilder(string.IsNullOrWhiteSpace(source) || source == "1"
            ? "Server=localhost;Integrated Security=true;TrustServerCertificate=true;Connection Timeout=5" : source) { InitialCatalog = database };
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlServer(connection.ConnectionString).Options;
        await using var db = new ApplicationDbContext(options);
        try
        {
            await db.GetService<IMigrator>().MigrateAsync("20261019174943_ContractCategorySnapshots");
            var oldId = Guid.NewGuid(); var created = DateTime.UtcNow;
            await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO ComboTemplates (Id,Name,Tier,Style,IsActive,SortOrder,ModerationStatus,CreatedAt) VALUES ({oldId},{"Original legacy template"},{"Popular"},{"Classic"},{false},{0},{"Draft"},{created})");
            await db.Database.MigrateAsync(); await db.Database.MigrateAsync();
            Assert.False(db.Database.HasPendingModelChanges()); Assert.Empty(await db.Database.GetPendingMigrationsAsync());
            var old = await db.ComboTemplates.AsNoTracking().SingleAsync(template => template.Id == oldId);
            Assert.Equal("Dynamic", old.PricingMode); Assert.Null(old.ComboPrice); Assert.Null(old.OriginalPrice); Assert.Equal("Original legacy template", old.Name);
            await using var fixture = await Fixture.Create(db);
            var package = await fixture.Publish();
            Assert.Equal(2_500_000, (await fixture.Price(package.Id)).Total);
            var seed = BudgetItem.Create(fixture.CustomerActor.UserId!.Value, fixture.First.CategoryId, "Chụp ảnh cưới", 8_000_000);
            seed.MarkPlanTemplate("photo"); db.BudgetItems.Add(seed); await db.SaveChangesAsync();
            var order = await new SubmitComboOrderCommandHandler(db, fixture.CustomerActor, new Notify()).Handle(
                new(package.Id, [], fixture.Date, 100, "0901234567", null, "HCM", fixture.Selections, 2_500_000), default);
            Assert.Equal(2_500_000, order.Total); Assert.All(order.Items, item => Assert.NotNull(item.Component));
            Assert.Equal(500_000, order.Items.Sum(item => item.DiscountShare));
            db.ChangeTracker.Clear();
            Assert.Equal(3_000_000, (await db.ComboTemplates.SingleAsync(template => template.Id == package.Id)).OriginalPrice);
            Assert.Equal(2, await db.ComboOrderItems.CountAsync(item => item.ComboOrderId == order.Id && item.ComponentSnapshotJson != null));
            var budgetIds = await db.ComboOrderItems.Where(item => item.ComboOrderId == order.Id).Select(item => item.BudgetItemId).ToListAsync();
            Assert.Equal(2, budgetIds.Distinct().Count()); Assert.Contains(seed.Id, budgetIds);
            Assert.Equal(order.Total, await db.BudgetItems.Where(row => budgetIds.Contains(row.Id)).SumAsync(row => row.PlannedCost));
            Assert.Equal("Dynamic", (await db.ComboTemplates.SingleAsync(template => template.Id == oldId)).PricingMode);
            await new CancelComboOrderCommandHandler(db, fixture.CustomerActor).Handle(new(order.Id), default);
            var restored = Assert.Single(await db.BudgetItems.AsNoTracking().ToListAsync());
            Assert.Equal(seed.Id, restored.Id); Assert.Equal(8_000_000, restored.PlannedCost); Assert.Equal("photo", restored.PlanTemplateKey);
        }
        finally { Assert.StartsWith("WeddingPlanTest_FixedCombo_", database); await db.Database.EnsureDeletedAsync(); }
    }

    private sealed record Current(Guid? UserId, string? Role) : ICurrentUserService { public string? Email => null; }
    private sealed class Audit : IAuditLogger { public Task LogAsync(string action, string entityType, string? entityId, object? data = null) => Task.CompletedTask; }
    private sealed class Notify : INotificationSender { public Task SendAsync(string channel, string recipient, string message, CancellationToken cancellationToken = default) => Task.CompletedTask; }
    private sealed class Fixture(ApplicationDbContext db, SqliteConnection? sqlite) : IAsyncDisposable
    {
        public ApplicationDbContext Db => db;
        public Vendor Vendor { get; private set; } = null!;
        public ServiceItem First { get; private set; } = null!; public ServiceItem Second { get; private set; } = null!;
        public ServiceItem Foreign { get; private set; } = null!; public ServiceItem Legacy { get; private set; } = null!;
        public Current OwnerActor { get; private set; } = null!; public Current CustomerActor { get; private set; } = null!;
        public Current ModeratorActor { get; private set; } = null!;
        public DateTime Date => DateTime.UtcNow.AddDays(30);
        public List<ComboSelection> Selections => [new(First.Id, null, null), new(Second.Id, null, null)];
        public ComboTemplateItemDto Slot(ServiceItem item) => new(item.CategoryId, item.ListingId, false, item.Id, []);
        public SaveComboTemplateCommand Command => new(null, "Fixed package", "Popular", "Modern", "Original combo description", null,
            true, 0, [Slot(First), Slot(Second)], "HCM", Content: new(Terms: "Package terms", DetailedDescription: "Detailed combo content"), PricingMode: "FixedPrice", ComboPrice: 2_500_000);
        public Task<ComboTemplateDto> Save(SaveComboTemplateCommand command) => new SaveComboTemplateCommandHandler(db, new Audit(), OwnerActor).Handle(command, default);
        public Task<bool> Review(Guid id, Current? actor = null) => new ReviewComboTemplateCommandHandler(db, new Audit(), actor ?? ModeratorActor).Handle(new(id, true), default);
        public async Task<ComboTemplateDto> Publish(SaveComboTemplateCommand? command = null) { var saved = await Save(command ?? Command); await Review(saved.Id); return saved; }
        public Task<ComboBuilderDto> Builder() => new GetComboBuilderQueryHandler(db).Handle(new(null), default);
        public Task<ComboPriceDto> Price(Guid id) => ComboPricing.CalculateAsync(db, new(id, "HCM", Date, 100, Selections), null, default);
        public async Task<string> OrderSnapshot(Guid id)
        {
            var order = await db.ComboOrders.AsNoTracking().Include(order => order.Items).SingleAsync(order => order.Id == id);
            return JsonSerializer.Serialize(new { order.Subtotal, order.Total, order.DiscountAmount,
                Items = order.Items.OrderBy(item => item.Id).Select(item => new { item.Title, item.Price, item.DiscountShare, item.ComponentSnapshotJson, item.SelectionJson, item.CancellationPolicySnapshot }) });
        }
        public static async Task<Fixture> Create(ApplicationDbContext? existing = null)
        {
            SqliteConnection? sqlite = null;
            if (existing == null) { sqlite = new SqliteConnection("Data Source=:memory:"); await sqlite.OpenAsync(); existing = new(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(sqlite).Options); await existing.Database.EnsureCreatedAsync(); }
            var fixture = new Fixture(existing, sqlite); var db = existing;
            User Add(string name, UserRole role) { var user = User.Create(name, $"{Guid.NewGuid():N}@fixed-package.example.invalid", "0901234567", "hash", role); db.Users.Add(user); return user; }
            var owner = Add("Owner", UserRole.VendorOwner); var foreignOwner = Add("Foreign owner", UserRole.VendorOwner);
            fixture.OwnerActor = new(owner.Id, "VendorOwner"); fixture.CustomerActor = new(Add("Customer", UserRole.Customer).Id, "Customer"); fixture.ModeratorActor = new(Add("Moderator", UserRole.Moderator).Id, "Moderator");
            Vendor Shop(User user) { var vendor = Vendor.Create(user.Id, "Synthetic vendor", "fixed-" + Guid.NewGuid().ToString("N"), "Owner", "0901234567", "HCM", .08m); vendor.SetVerified(true); vendor.ConfigureComboParticipation(true, .10m); db.Vendors.Add(vendor); return vendor; }
            fixture.Vendor = Shop(owner); var foreignVendor = Shop(foreignOwner);
            var category = await db.Categories.FirstAsync(category => category.Slug == "quay-chup-photo-video");
            ServiceItem Service(Vendor vendor, string name, decimal price, bool canonical = true)
            {
                var listing = Listing.Create(vendor.Id, category.Id, name, "fixed-" + Guid.NewGuid().ToString("N"), price, price, "Original service description", "HCM"); listing.Approve(); db.Listings.Add(listing);
                var item = ServiceItem.Create(vendor.Id, listing.Id, category.Id, name, price, "Package", 1, 1000, "HCM", "Original cancellation terms"); item.Submit(); item.Review(true, null); item.SetDailyCapacity(10);
                if (canonical) listing.AttachPricingServiceItem(item.Id); db.ServiceItems.Add(item); return item;
            }
            fixture.First = Service(fixture.Vendor, "First service", 1_000_000); fixture.Second = Service(fixture.Vendor, "Second service", 2_000_000);
            fixture.Foreign = Service(foreignVendor, "Foreign service", 3_000_000); fixture.Legacy = Service(fixture.Vendor, "Legacy unlinked facet", 4_000_000, false);
            await db.SaveChangesAsync(); return fixture;
        }
        public async ValueTask DisposeAsync() { if (sqlite != null) { await db.DisposeAsync(); await sqlite.DisposeAsync(); } }
    }
}
