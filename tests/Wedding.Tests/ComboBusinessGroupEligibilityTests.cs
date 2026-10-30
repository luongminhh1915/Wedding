using System.Text.Json;
using Microsoft.Data.SqlClient;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Wedding.Application.Common.Interfaces;
using Wedding.Application.Features.Combos;
using Wedding.Application.Features.Listings;
using Wedding.Application.Features.Listings.Queries.GetListingDetail;
using Wedding.Domain.Entities;
using Wedding.Domain.Enums;
using Wedding.Domain.Exceptions;
using Wedding.Infrastructure.Persistence;

namespace Wedding.Tests;

public class ComboBusinessGroupEligibilityTests
{
    public static TheoryData<string> PublicSurfaces => new()
    {
        "builder", "fixed-catalog", "dynamic-catalog", "service-catalog", "fixed-price", "dynamic-price", "custom-price",
        "fixed-submit", "dynamic-submit", "custom-submit", "fixed-draft"
    };

    [Theory, MemberData(nameof(PublicSurfaces))]
    public async Task InactiveParentGroupCannotRemainAvailableThroughAnyPublicComboPath(string surface)
    {
        await using var fixture = await Fixture.Create();
        await fixture.SetGroupActivity(false);
        await AssertInactiveSurface(fixture, surface);
    }

    [Fact]
    public async Task GroupActivityOnlyChangesPublicAvailabilityAndPreservesPrivateAccessAndHistoricalSnapshots()
    {
        await using var fixture = await Fixture.Create();
        await AssertActive(fixture);
        var before = await fixture.HistoricalSnapshot();
        await fixture.SetGroupActivity(false);
        foreach (var surface in PublicSurfaces) await AssertInactiveSurface(fixture, surface);
        await AssertPrivateAndHistory(fixture);
        Assert.Equal(before, await fixture.HistoricalSnapshot());
        await fixture.SetGroupActivity(true);
        await AssertActive(fixture);
        Assert.Equal(before, await fixture.HistoricalSnapshot());
    }

    [Fact]
    public async Task LegacyCategoryWithoutParentGroupRemainsAvailableForFixedAndDynamicCombos()
    {
        await using var fixture = await Fixture.Create(unmapped: true);
        Assert.Null(fixture.Category.BusinessGroupId);
        await AssertActive(fixture);
        var before = await fixture.HistoricalSnapshot();
        await AssertPrivateAndHistory(fixture);
        Assert.Equal(before, await fixture.HistoricalSnapshot());
    }

    [SqlServerFact]
    public async Task SqlParentGroupGuardCoversEveryComboPathWithoutChangingLegacyOrFinancialSnapshots()
    {
        var database = $"WeddingPlanTest_ComboGroup_{Guid.NewGuid():N}";
        var source = Environment.GetEnvironmentVariable("WEDDING_TEST_SQLSERVER");
        var connection = new SqlConnectionStringBuilder(string.IsNullOrWhiteSpace(source) || source == "1"
            ? "Server=localhost;Integrated Security=true;TrustServerCertificate=true;Connection Timeout=5" : source)
            { InitialCatalog = database };
        await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlServer(connection.ConnectionString).Options);
        try
        {
            await db.Database.MigrateAsync();
            Assert.False(db.Database.HasPendingModelChanges());
            await using var fixture = await Fixture.Create(db);
            await AssertActive(fixture);
            var before = await fixture.HistoricalSnapshot();
            await fixture.SetGroupActivity(false);
            foreach (var surface in PublicSurfaces) await AssertInactiveSurface(fixture, surface);
            await AssertPrivateAndHistory(fixture);
            Assert.Equal(before, await fixture.HistoricalSnapshot());
            await fixture.SetGroupActivity(true);
            await AssertActive(fixture);
            Assert.Equal(before, await fixture.HistoricalSnapshot());

            await using var unmapped = await Fixture.Create(db, unmapped: true);
            Assert.Null(unmapped.Category.BusinessGroupId);
            await AssertActive(unmapped);
            await AssertPrivateAndHistory(unmapped);
            Assert.Equal(before, await fixture.HistoricalSnapshot());
        }
        finally
        {
            Assert.StartsWith("WeddingPlanTest_ComboGroup_", database);
            await db.Database.EnsureDeletedAsync();
        }
    }

    private static async Task AssertInactiveSurface(Fixture fixture, string surface)
    {
        Assert.True(fixture.Category.IsActive);
        Assert.False(await fixture.Db.Listings.PubliclyVisible().AnyAsync(listing => listing.Id == fixture.First.ListingId));
        var originalOrders = await fixture.Db.ComboOrders.CountAsync();
        switch (surface)
        {
            case "builder":
                var builder = await new GetComboBuilderQueryHandler(fixture.Db).Handle(new(null), default);
                Assert.DoesNotContain(builder.Categories.SelectMany(category => category.Options), option => fixture.ItemIds.Contains(option.ServiceItemId));
                Assert.DoesNotContain(builder.Templates, template => fixture.TemplateIds.Contains(template.Id));
                break;
            case "fixed-catalog":
            case "dynamic-catalog":
            case "service-catalog":
                var catalog = await new GetComboCatalogPageQueryHandler(fixture.Db).Handle(new(
                    CategoryId: surface == "service-catalog" ? fixture.Category.Id : null,
                    TemplateId: surface == "dynamic-catalog" ? fixture.Dynamic.Id : fixture.Fixed.Id,
                    SelectedItemIds: fixture.ItemIds), default);
                Assert.Null(catalog.SelectedTemplate);
                Assert.DoesNotContain(catalog.Builder.Templates, template => fixture.TemplateIds.Contains(template.Id));
                Assert.DoesNotContain(catalog.SelectedOptions, option => fixture.ItemIds.Contains(option.ServiceItemId));
                Assert.DoesNotContain(catalog.Builder.Categories.SelectMany(category => category.Options), option => fixture.ItemIds.Contains(option.ServiceItemId));
                Assert.DoesNotContain(catalog.CategoryCounts, category => category.CategoryId == fixture.Category.Id && category.Count > 0);
                break;
            case "fixed-price":
            case "dynamic-price":
            case "custom-price":
                await Assert.ThrowsAsync<DomainException>(() => ComboPricing.CalculateAsync(fixture.Db, fixture.Price(surface.Split('-')[0]), fixture.Customer.UserId, default));
                break;
            default:
                var request = fixture.Price(surface.Split('-')[0]);
                await Assert.ThrowsAsync<DomainException>(() => new SubmitComboOrderCommandHandler(fixture.Db, fixture.Customer, new Notify())
                    .Handle(new(request.TemplateId, [], request.WeddingDate, request.GuestCount, "0901234567", null,
                        request.Region, request.Selections, SaveAsDraft: surface == "fixed-draft"), default));
                Assert.Equal(originalOrders, await fixture.Db.ComboOrders.CountAsync());
                break;
        }
    }

    private static async Task AssertActive(Fixture fixture)
    {
        Assert.True(await fixture.Db.Listings.PubliclyVisible().AnyAsync(listing => listing.Id == fixture.First.ListingId));
        Assert.Equal(fixture.ItemIds.Count, await ComboPublicCatalog.Eligible(fixture.Db).CountAsync(item => fixture.ItemIds.Contains(item.Id)));
        var builder = await new GetComboBuilderQueryHandler(fixture.Db).Handle(new(null), default);
        Assert.All(fixture.TemplateIds, id => Assert.Contains(builder.Templates, template => template.Id == id));
        foreach (var (mode, expected) in new[] { ("fixed", 2_500_000m), ("dynamic", 970_000m), ("custom", 1_000_000m) })
            Assert.Equal(expected, (await ComboPricing.CalculateAsync(fixture.Db, fixture.Price(mode), fixture.Customer.UserId, default)).Total);
        foreach (var id in fixture.TemplateIds)
        {
            var catalog = await new GetComboCatalogPageQueryHandler(fixture.Db).Handle(new(TemplateId: id), default);
            Assert.Equal(id, catalog.SelectedTemplate?.Id);
        }
    }

    private static async Task AssertPrivateAndHistory(Fixture fixture)
    {
        foreach (var actor in new[] { fixture.Owner, fixture.Moderator })
        {
            var preview = await new GetComboTemplatePreviewQueryHandler(fixture.Db, actor).Handle(new(fixture.Fixed.Id), default);
            Assert.Equal(fixture.Fixed.Id, preview.Id); Assert.Equal(2_500_000, preview.ComboPrice); Assert.Equal(2, preview.Components!.Count);
            var service = await new GetListingDetailQueryHandler(fixture.Db, actor).Handle(new(fixture.First.ListingId), default);
            Assert.Equal(fixture.First.ListingId, service.Id);
        }
        await Assert.ThrowsAsync<NotFoundException>(() => new GetComboTemplatePreviewQueryHandler(fixture.Db, fixture.ForeignOwner)
            .Handle(new(fixture.Fixed.Id), default));
        var history = await new GetMyComboOrdersQueryHandler(fixture.Db, fixture.Customer).Handle(new(), default);
        var original = Assert.Single(history, order => order.Id == fixture.Order.Id);
        Assert.Equal(2_500_000, original.Total); Assert.All(original.Items, line => Assert.NotNull(line.Component));
    }

    private sealed record Actor(Guid? UserId, string? Role) : ICurrentUserService { public string? Email => null; }
    private sealed class Audit : IAuditLogger { public Task LogAsync(string action, string entityType, string? entityId, object? data = null) => Task.CompletedTask; }
    private sealed class Notify : INotificationSender { public Task SendAsync(string channel, string recipient, string message, CancellationToken cancellationToken = default) => Task.CompletedTask; }
    private sealed class Fixture(ApplicationDbContext db, SqliteConnection? sqlite) : IAsyncDisposable
    {
        public ApplicationDbContext Db => db;
        public Category Category { get; private set; } = null!;
        public ServiceItem First { get; private set; } = null!;
        private ServiceItem second = null!;
        public Actor Customer { get; private set; } = null!;
        public Actor Owner { get; private set; } = null!;
        public Actor Moderator { get; private set; } = null!;
        public Actor ForeignOwner { get; private set; } = null!;
        public ComboTemplate Fixed { get; private set; } = null!;
        public ComboTemplate Dynamic { get; private set; } = null!;
        public ComboOrderDto Order { get; private set; } = null!;
        private Guid contractId;
        public List<Guid> ItemIds => [First.Id, second.Id];
        public List<Guid> TemplateIds => [Fixed.Id, Dynamic.Id];
        public ComboPriceCommand Price(string mode) => new(mode == "fixed" ? Fixed.Id : mode == "dynamic" ? Dynamic.Id : null,
            "HCM", DateTime.UtcNow.AddDays(30), 100,
            mode == "fixed" ? [new(First.Id, null, null), new(second.Id, null, null)] : [new(First.Id, null, null)]);
        public async Task SetGroupActivity(bool active)
        {
            var group = await db.BusinessGroups.SingleAsync(group => group.Id == Category.BusinessGroupId);
            db.Entry(group).Property(group => group.IsActive).CurrentValue = active;
            await db.SaveChangesAsync();
        }
        public async Task<string> HistoricalSnapshot()
        {
            var templateIds = TemplateIds;
            var templates = await db.ComboTemplates.AsNoTracking().Include(template => template.Items).Where(template => templateIds.Contains(template.Id)).OrderBy(template => template.Id).ToListAsync();
            var order = await db.ComboOrders.AsNoTracking().Include(order => order.Items).SingleAsync(order => order.Id == Order.Id);
            var contract = await db.BookingContracts.AsNoTracking().SingleAsync(contract => contract.Id == contractId);
            return JsonSerializer.Serialize(new
            {
                Templates = templates.Select(template => new { template.Id, template.Name, template.PricingMode, template.ComboPrice, template.OriginalPrice,
                    template.ModerationStatus, template.IsActive, Items = template.Items.OrderBy(item => item.Id).Select(item => new { item.Id, item.ServiceItemId, item.ComponentSnapshotJson }) }),
                Order = new { order.Id, order.Subtotal, order.Total, order.DiscountAmount, order.DiscountPercent, order.PriceLockUntil,
                    Items = order.Items.OrderBy(item => item.Id).Select(item => new { item.Id, item.Price, item.DiscountShare, item.Title, item.Unit, item.Quantity,
                        item.ComponentSnapshotJson, item.SelectionJson, item.CancellationPolicySnapshot }) },
                Contract = new { contract.Id, contract.ContractValue, contract.DepositAmount, contract.Status, contract.ConfirmedAt, contract.Version,
                    contract.CommissionRateSnapshot, contract.CommissionCategoryIdSnapshot, contract.CommissionCategoryNameSnapshot,
                    contract.CommissionBusinessGroupIdSnapshot, contract.CommissionBusinessGroupNameSnapshot,
                    contract.CommissionRateSourceSnapshot, contract.CommissionPolicyVersionSnapshot, contract.CancellationPolicySnapshot },
                Commissions = await db.Commissions.AsNoTracking().Where(commission => commission.ContractId == contractId).OrderBy(commission => commission.Id)
                    .Select(commission => new { commission.Id, commission.CommissionRate, commission.CommissionAmount, commission.PaidAmount, commission.PaymentReferenceCode,
                        commission.Status, commission.Version, commission.ReleaseAt, commission.DueDate }).ToListAsync()
            });
        }
        public static async Task<Fixture> Create(ApplicationDbContext? existing = null, bool unmapped = false)
        {
            SqliteConnection? sqlite = null;
            if (existing == null)
            {
                sqlite = new("Data Source=:memory:"); await sqlite.OpenAsync();
                existing = new(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(sqlite).Options); await existing.Database.EnsureCreatedAsync();
            }
            var fixture = new Fixture(existing, sqlite); var db = existing;
            User User(UserRole role) { var user = Wedding.Domain.Entities.User.Create("Synthetic group eligibility actor", $"{Guid.NewGuid():N}@combo-group.example.invalid", "0901234567", "synthetic-hash", role); db.Users.Add(user); return user; }
            var owner = User(UserRole.VendorOwner); var customer = User(UserRole.Customer); var moderator = User(UserRole.Moderator); var foreign = User(UserRole.VendorOwner);
            fixture.Owner = new(owner.Id, "VendorOwner"); fixture.Customer = new(customer.Id, "Customer"); fixture.Moderator = new(moderator.Id, "Moderator"); fixture.ForeignOwner = new(foreign.Id, "VendorOwner");
            Vendor Shop(User user) { var shop = Vendor.Create(user.Id, "Synthetic group shop", "combo-group-" + Guid.NewGuid().ToString("N"), "Owner", "0901234567", "HCM", .08m); shop.SetVerified(true); shop.ConfigureComboParticipation(true, .10m); db.Vendors.Add(shop); return shop; }
            var vendor = Shop(owner); _ = Shop(foreign);
            fixture.Category = unmapped ? Category.Create("Legacy unmapped service", "combo-group-unmapped-" + Guid.NewGuid().ToString("N"), null, null, .08m)
                : await db.Categories.Include(category => category.BusinessGroup).SingleAsync(category => category.Slug == "quay-chup-photo-video");
            if (unmapped) db.Categories.Add(fixture.Category);
            ServiceItem Service(string name, decimal price)
            {
                var listing = Listing.Create(vendor.Id, fixture.Category.Id, name, "combo-group-" + Guid.NewGuid().ToString("N"), price, price, "Original approved scope", "HCM"); listing.Approve(); db.Listings.Add(listing);
                var item = ServiceItem.Create(vendor.Id, listing.Id, fixture.Category.Id, name, price, "Package", 1, 1000, "HCM", "Original cancellation terms");
                item.SetDailyCapacity(10); item.Submit(); item.Review(true, null); listing.AttachPricingServiceItem(item.Id); db.ServiceItems.Add(item); return item;
            }
            fixture.First = Service("First approved source", 1_000_000); fixture.second = Service("Second approved source", 2_000_000);
            await db.SaveChangesAsync();
            ComboTemplateItemDto Slot(ServiceItem item) => new(item.CategoryId, item.ListingId, false, item.Id, []);
            var saved = await new SaveComboTemplateCommandHandler(db, new Audit(), fixture.Owner).Handle(new(null, "Fixed eligible package", "Popular", "Classic", "Original fixed scope", null,
                true, 0, [Slot(fixture.First), Slot(fixture.second)], "HCM", PricingMode: "FixedPrice", ComboPrice: 2_500_000), default);
            await new ReviewComboTemplateCommandHandler(db, new Audit(), fixture.Moderator).Handle(new(saved.Id, true, RevisionId: saved.LatestRevision!.Id), default);
            fixture.Fixed = await db.ComboTemplates.Include(template => template.Items).SingleAsync(template => template.Id == saved.Id);
            fixture.Dynamic = ComboTemplate.Create("Original legacy dynamic", "Popular", "Classic", "Original legacy scope", null, 0);
            fixture.Dynamic.SetScope(vendor.Id, "HCM", .03m, true);
            var slot = ComboTemplateItem.Create(fixture.Dynamic.Id, fixture.Category.Id, fixture.First.ListingId, false); slot.ConfigureItems(fixture.First.Id, "[]"); fixture.Dynamic.Items.Add(slot); db.ComboTemplates.Add(fixture.Dynamic);
            await db.SaveChangesAsync();
            var price = fixture.Price("fixed");
            fixture.Order = await new SubmitComboOrderCommandHandler(db, fixture.Customer, new Notify()).Handle(new(price.TemplateId, [], price.WeddingDate, price.GuestCount, "0901234567", "Historical fixed order", "HCM", price.Selections), default);
            var lead = Lead.Create(customer.Id, vendor.Id, fixture.First.ListingId, "0901234567", price.WeddingDate, 100, 1_000_000, "Historical contract lead"); lead.Accept(); lead.MarkContracted(); db.Leads.Add(lead);
            var contract = BookingContract.Create(lead.Id, null, vendor.Id, customer.Id, 1_000_000, 200_000, null, price.WeddingDate);
            contract.ConfirmByCustomer(); contract.SnapshotCommissionRate(.08m); contract.SnapshotCommissionCategory(fixture.Category, "LegacyVendor"); db.BookingContracts.Add(contract); fixture.contractId = contract.Id;
            var split = CommissionRules.Split(contract.ContractValue, contract.CommissionRateSnapshot!.Value);
            var first = Commission.Create(contract.Id, vendor.Id, CommissionPeriod.Period1_Deposit, .08m, split.First, DateTime.UtcNow.AddDays(20)); first.ApplyPayment(10_000, "SYNTHETIC-GROUP-RECEIPT"); db.Commissions.Add(first);
            var second = Commission.Create(contract.Id, vendor.Id, CommissionPeriod.Period2_Completion, .08m, split.Second, DateTime.UtcNow.AddDays(40)); second.LockUntil(price.WeddingDate.AddDays(3)); db.Commissions.Add(second);
            await db.SaveChangesAsync();
            return fixture;
        }
        public async ValueTask DisposeAsync() { if (sqlite != null) { await db.DisposeAsync(); await sqlite.DisposeAsync(); } }
    }
}
