using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Wedding.Application.Features.Combos;
using Wedding.Domain.Entities;
using Wedding.Domain.Enums;
using Wedding.Domain.Exceptions;
using Wedding.Infrastructure.Persistence;

namespace Wedding.Tests;

public class ComboCatalogFilterTests
{
    [Fact]
    public async Task FiltersAreAndedBeforePagingWithAuthoritativeQuantityAndCappedSavings()
    {
        await using var f = await Fixture.Create();
        await Combined(f);
    }

    private static async Task Combined(Fixture f)
    {
        var r = new GetComboCatalogPageQuery(Guests: 21, City: "TP Hồ Chí Minh", Region: "Sài Gòn",
            MinPrice: 1400, MaxPrice: 1500, MinServices: 2, MaxServices: 2, MinSavings: 150, MinRating: 4.5m,
            TemplateCategoryId: f.Photo.Id, FilterVendorId: f.ShopA.Id, Page: 999, PageSize: 1);
        var page = await f.Query(r);
        Assert.Equal(1, page.TotalCount); Assert.Equal(1, page.Page);
        Assert.Equal(f.TemplateA.Id, Assert.Single(page.Builder.Templates).Id);
        var estimate = Assert.Single(page.Estimates!);
        Assert.Equal(1600m, estimate.Subtotal); Assert.Equal(160m, estimate.Savings); Assert.Equal(1440m, estimate.Total);
        Assert.Equal(.10m, estimate.DiscountPercent); Assert.Equal(2, estimate.RequiredServiceCount); Assert.Equal(21, estimate.Guests);
        var price = await ComboPricing.CalculateAsync(f.Db, new(f.TemplateA.Id, "Hồ Chí Minh", DateTime.UtcNow.AddDays(20), 21,
            [new(f.APhoto.Id, [], []), new(f.ATable.Id, [], [])]), null, default);
        Assert.Equal(price.Subtotal, estimate.Subtotal); Assert.Equal(price.DiscountAmount, estimate.Savings); Assert.Equal(price.Total, estimate.Total);
        foreach (var miss in new[] { r with { MaxPrice = 1439 }, r with { MinSavings = 161 }, r with { MinRating = 4.9m },
                     r with { MinServices = 3, MaxServices = 3 }, r with { City = "Hà Nội" }, r with { Region = "Hà Nội" },
                     r with { VendorId = f.ShopB.Id }, r with { TemplateCategoryId = f.Other.Id } })
            Assert.Equal(0, (await f.Query(miss)).TotalCount);
    }

    [Fact]
    public async Task SortsAreStableAndTotalsNotUnitPricesAndUnknownShopRatingNeverInvented()
    {
        await using var f = await Fixture.Create();
        await Sorts(f);
    }
    private static async Task Sorts(Fixture f)
    {
        var asc = await f.Query(new(Guests: 21, Sort: "price-asc", PageSize: 1));
        var desc = await f.Query(new(Guests: 21, Sort: "price-desc", PageSize: 1));
        Assert.Equal(f.TemplateA.Id, Assert.Single(asc.Builder.Templates).Id);
        Assert.Equal(f.TemplateB.Id, Assert.Single(desc.Builder.Templates).Id);
        Assert.Equal(f.TemplateB.Id, Assert.Single((await f.Query(new(Guests: 21, Sort: "rating", PageSize: 1))).Builder.Templates).Id);
        Assert.Equal(f.TemplateC.Id, Assert.Single((await f.Query(new(Guests: 21, Sort: "savings", PageSize: 1))).Builder.Templates).Id);
        var second = await f.Query(new(Guests: 21, Sort: "price-asc", Page: 2, PageSize: 1));
        Assert.Equal(3, second.TotalCount); Assert.NotEqual(asc.Builder.Templates[0].Id, second.Builder.Templates[0].Id);
        Assert.Equal(f.TemplateC.Id, second.Builder.Templates[0].Id);
        f.ShopB.UpdateRating(5m, 2); await f.Db.SaveChangesAsync();
        var rated = await f.Query(new(Guests: 21, MinRating: 4.8m)); Assert.Equal(0, rated.TotalCount);
        var unfiltered = await f.Query(new(Guests: 21));
        Assert.Null(unfiltered.Estimates!.Single(e => e.TemplateId == f.TemplateB.Id).ShopRating);
        Assert.Null(unfiltered.Builder.Vendors!.Single(v => v.Id == f.ShopB.Id).RatingAvg);
    }

    [Fact]
    public async Task SelectedPublicCartHydratesOutsideFiltersWithoutBypassingSearchOrExposingDrafts()
    {
        await using var f = await Fixture.Create();
        f.TemplateC.StageUnpublishedForTest(f.Db); await f.Db.SaveChangesAsync();
        var page = await f.Query(new(Search: "no matching discovery", TemplateId: f.TemplateA.Id,
            SelectedItemIds: [f.APhoto.Id], FilterVendorId: f.ShopB.Id, Guests: 21));
        Assert.Equal(0, page.TotalCount); Assert.Empty(page.Builder.Templates);
        Assert.Equal(f.TemplateA.Id, page.SelectedTemplate!.Id);
        Assert.Contains(page.SelectedOptions, item => item.ServiceItemId == f.APhoto.Id);
        var privateAttempt = await f.Query(new(TemplateId: f.TemplateC.Id, Guests: 21));
        Assert.Null(privateAttempt.SelectedTemplate); Assert.DoesNotContain(privateAttempt.Builder.Templates, t => t.Id == f.TemplateC.Id);
    }

    [Theory]
    [InlineData("inactive-item")]
    [InlineData("hidden-listing")]
    [InlineData("inactive-category")]
    [InlineData("wrong-listing-vendor")]
    [InlineData("wrong-listing-category")]
    [InlineData("wrong-owner-role")]
    [InlineData("inactive-owner")]
    [InlineData("foreign-allowed-option")]
    [InlineData("unverified-shop")]
    [InlineData("inactive-template")]
    public async Task PublicEligibilityCannotBeBypassedByDirectTemplateOrSelectedIds(string mutation)
    {
        await using var f = await Fixture.Create();
        if (mutation == "inactive-item") f.APhoto.Update("Photo", 1000, "Package", 1, 1000, "HCM", "Terms", false);
        if (mutation == "hidden-listing") f.APhoto.Listing.SetActivity(false);
        if (mutation == "inactive-category") f.Db.Entry(f.Photo).Property(c => c.IsActive).CurrentValue = false;
        if (mutation == "wrong-listing-vendor") f.Db.Entry(f.APhoto.Listing).Property(l => l.VendorId).CurrentValue = f.ShopB.Id;
        if (mutation == "wrong-listing-category") f.Db.Entry(f.APhoto.Listing).Property(l => l.CategoryId).CurrentValue = f.Venue.Id;
        if (mutation == "wrong-owner-role") f.Db.Entry(f.OwnerA).Property(u => u.Role).CurrentValue = UserRole.Customer;
        if (mutation == "inactive-owner") f.OwnerA.SetStatus(false);
        if (mutation == "foreign-allowed-option") f.TemplateA.Items.First().ConfigureItems(f.APhoto.Id, $"[\"{f.BPhoto.Id}\"]");
        if (mutation == "unverified-shop") f.ShopA.SetVerified(false);
        if (mutation == "inactive-template") f.TemplateA.SetActivity(false);
        await f.Db.SaveChangesAsync();
        var page = await f.Query(new(TemplateId: f.TemplateA.Id, SelectedItemIds: [f.APhoto.Id], Guests: 21));
        Assert.Null(page.SelectedTemplate); Assert.DoesNotContain(page.Builder.Templates, t => t.Id == f.TemplateA.Id);
        var full = await new GetComboBuilderQueryHandler(f.Db).Handle(new(null, 21), default);
        Assert.DoesNotContain(full.Templates, t => t.Id == f.TemplateA.Id);
        await Assert.ThrowsAsync<DomainException>(() => ComboPricing.CalculateAsync(f.Db, new(f.TemplateA.Id, "HCM", DateTime.UtcNow.AddDays(20), 21,
            [new(f.APhoto.Id, [], []), new(f.ATable.Id, [], [])]), null, default));
    }

    [Theory]
    [InlineData("negative-price")]
    [InlineData("reversed-price")]
    [InlineData("fraction-price")]
    [InlineData("huge-price")]
    [InlineData("rating")]
    [InlineData("count")]
    [InlineData("count-range")]
    [InlineData("sort")]
    [InlineData("region-length")]
    public async Task InvalidFiltersAreRejectedInsteadOfSilentlyApplyingDifferentConditions(string invalid)
    {
        await using var f = await Fixture.Create();
        var request = invalid switch {
            "negative-price" => new GetComboCatalogPageQuery(MinPrice: -1),
            "reversed-price" => new(MinPrice: 5, MaxPrice: 1), "fraction-price" => new(MaxPrice: 1.5m),
            "huge-price" => new(MinPrice: decimal.MaxValue), "rating" => new(MinRating: 5.1m),
            "count" => new(MinServices: 13), "count-range" => new(MinServices: 3, MaxServices: 2),
            "sort" => new(Sort: "sql-injection"), _ => new(Region: new string('x', 101))
        };
        await Assert.ThrowsAsync<DomainException>(() => f.Query(request));
    }

    [Fact]
    public async Task CategoryServiceModeFiltersBeforePagingAndPreservesHydratedCart()
    {
        await using var f = await Fixture.Create();
        var page = await f.Query(new(CategoryId: f.Photo.Id, Guests: 21, Region: "HCM", City: "Hồ Chí Minh",
            MinPrice: 1000, MaxPrice: 1000, MinRating: 4.5m, SelectedItemIds: [f.BPhoto.Id], Page: int.MaxValue, PageSize: int.MaxValue));
        Assert.Equal(1, page.TotalCount); Assert.Equal(10, page.PageSize); Assert.Equal(1, page.Page);
        Assert.Equal(f.Photo.Id, page.CategoryId);
        Assert.Equal(f.APhoto.Id, Assert.Single(page.Builder.Categories.SelectMany(c => c.Options)).ServiceItemId);
        Assert.Contains(page.SelectedOptions, i => i.ServiceItemId == f.BPhoto.Id);
        Assert.Empty(page.Builder.Templates);
    }

    [Fact]
    public async Task OptionalServicesDoNotInflateIncludedCountAndDefaultBundleRulesMatchPriceEngine()
    {
        await using var f = await Fixture.Create();
        var optionalItem = f.AddItem(f.ShopA, f.Other, 9000, "Package", "HN");
        var template = f.AddTemplate("Default bundle", f.ShopA, [f.APhoto, f.ATable], null);
        var optional = ComboTemplateItem.Create(template.Id, f.Other.Id, optionalItem.ListingId, true);
        optional.ConfigureItems(optionalItem.Id, "[]"); template.Items.Add(optional);
        f.Db.BundleDiscountRules.Add(BundleDiscountRule.Create(2, .08m));
        await f.Db.SaveChangesAsync();
        var page = await f.Query(new(Search: "Default bundle", Guests: 21, Region: "Hồ Chí Minh", MinServices: 2, MaxServices: 2));
        var estimate = Assert.Single(page.Estimates!);
        Assert.Equal(2, estimate.RequiredServiceCount); Assert.Equal(1600m, estimate.Subtotal); Assert.Equal(128m, estimate.Savings);
        var price = await ComboPricing.CalculateAsync(f.Db, new(template.Id, "HCM", DateTime.UtcNow.AddDays(20), 21,
            [new(f.APhoto.Id, [], []), new(f.ATable.Id, [], [])]), null, default);
        Assert.Equal(price.Total, estimate.Total); Assert.Equal(price.DiscountAmount, estimate.Savings);
    }

    [Fact]
    public async Task BatchBoundaryKeepsExactCountAndPagingNeverReturnsAllAlbums()
    {
        await using var f = await Fixture.Create();
        for (var n = 0; n < 101; n++) f.AddTemplate($"Many {n}", f.ShopA, [f.APhoto, f.ATable], .05m);
        await f.Db.SaveChangesAsync();
        var page = await f.Query(new(Search: "Many", Guests: 21, Page: 34, PageSize: 3));
        Assert.Equal(101, page.TotalCount); Assert.Equal(34, page.Page); Assert.Equal(2, page.Builder.Templates.Count);
        Assert.Equal(2, page.Estimates!.Count); Assert.Equal(2, page.SelectedOptions.Count);
    }

    [SqlServerFact]
    public async Task SqlServerFiltersOrderingPriceAndBatchPagingUseRealProvider()
    {
        await using var f = await Fixture.Create(sql: true);
        await Combined(f); await Sorts(f);
        var page = await f.Query(new(Guests: 21, Page: int.MaxValue, PageSize: 1));
        Assert.Equal(3, page.TotalCount); Assert.Equal(3, page.Page); Assert.Single(page.Builder.Templates);
    }

    private sealed class Fixture : IAsyncDisposable
    {
        public ApplicationDbContext Db = null!;
        private SqliteConnection? connection;
        private string? databaseName;
        public Category Photo = null!, Venue = null!, Other = null!;
        public Vendor ShopA = null!, ShopB = null!;
        public User OwnerA = null!;
        public ServiceItem APhoto = null!, ATable = null!, BPhoto = null!;
        public ComboTemplate TemplateA = null!, TemplateB = null!, TemplateC = null!;
        public Task<ComboCatalogPageDto> Query(GetComboCatalogPageQuery request) => new GetComboCatalogPageQueryHandler(Db).Handle(request, default);
        public static async Task<Fixture> Create(bool sql = false)
        {
            var f = new Fixture(); var options = new DbContextOptionsBuilder<ApplicationDbContext>();
            if (sql) { f.databaseName = $"WeddingPlanTest_ComboFilters_{Guid.NewGuid():N}"; options.UseSqlServer($"Server=localhost;Database={f.databaseName};Integrated Security=true;TrustServerCertificate=true;Connection Timeout=5"); }
            else { f.connection = new SqliteConnection("Data Source=:memory:"); await f.connection.OpenAsync(); options.UseSqlite(f.connection); }
            f.Db = new(options.Options); if (sql) await f.Db.Database.MigrateAsync(); else await f.Db.Database.EnsureCreatedAsync();
            f.Photo = await f.Db.Categories.SingleAsync(c => c.Slug == "quay-chup-photo-video");
            f.Venue = await f.Db.Categories.SingleAsync(c => c.Slug == "tiem-cuoi-venues");
            f.Other = await f.Db.Categories.SingleAsync(c => c.Slug == "trang-diem-makeup");
            f.OwnerA = User.Create("Synthetic Owner A", "a@combofilters.test", "0900000000", "hash", UserRole.VendorOwner);
            var ownerB = User.Create("Synthetic Owner B", "b@combofilters.test", "0900000001", "hash", UserRole.VendorOwner);
            f.Db.Users.AddRange(f.OwnerA, ownerB);
            f.ShopA = Vendor.Create(f.OwnerA.Id, "Synthetic HCM", "synthetic-hcm", "Owner", "0900000000", "HCM", .08m);
            f.ShopB = Vendor.Create(ownerB.Id, "Synthetic Hanoi", "synthetic-hn", "Owner", "0900000001", "Hà Nội", .08m);
            f.ShopA.SetVerified(true); f.ShopA.ConfigureComboParticipation(true, .10m); f.ShopA.UpdateRating(4.7m, 10);
            f.ShopB.SetVerified(true); f.ShopB.ConfigureComboParticipation(true, .15m); f.ShopB.UpdateRating(4.9m, 10);
            f.Db.Vendors.AddRange(f.ShopA, f.ShopB);
            f.APhoto = f.AddItem(f.ShopA, f.Photo, 1000, "Package", "HCM");
            f.ATable = f.AddItem(f.ShopA, f.Venue, 200, "Table", "Hồ Chí Minh");
            f.BPhoto = f.AddItem(f.ShopB, f.Photo, 1500, "Package", "Hà Nội");
            var bGuest = f.AddItem(f.ShopB, f.Venue, 100, "Guest", "HN");
            var aPremium = f.AddItem(f.ShopA, f.Photo, 2500, "Hour", "*");
            f.TemplateA = f.AddTemplate("Package A", f.ShopA, [f.APhoto, f.ATable], .20m);
            f.TemplateB = f.AddTemplate("Package B", f.ShopB, [f.BPhoto, bGuest], .05m);
            f.TemplateC = f.AddTemplate("Package C", f.ShopA, [aPremium, f.ATable], .20m);
            await f.Db.SaveChangesAsync(); return f;
        }
        public ServiceItem AddItem(Vendor vendor, Category category, decimal price, string unit, string region)
        {
            var listing = Listing.Create(vendor.Id, category.Id, $"Synthetic {unit} {price}", $"synthetic-{Guid.NewGuid():N}", price, price, "Synthetic catalog fixture", vendor.City);
            listing.Approve(); Db.Listings.Add(listing);
            var item = ServiceItem.Create(vendor.Id, listing.Id, category.Id, listing.Title, price, unit, 1, 1000, region, "Synthetic terms");
            item.Submit(); item.Review(true, null); Db.ServiceItems.Add(item); return item;
        }
        public ComboTemplate AddTemplate(string name, Vendor vendor, List<ServiceItem> items, decimal? discount)
        {
            var template = ComboTemplate.Create(name, "Popular", "Classic", "Synthetic preset", null, 0);
            foreach (var item in items) { var slot = ComboTemplateItem.Create(template.Id, item.CategoryId, item.ListingId, false); slot.ConfigureItems(item.Id, "[]"); template.Items.Add(slot); }
            template.SetScope(vendor.Id, vendor.City, discount, true); Db.ComboTemplates.Add(template); return template;
        }
        public async ValueTask DisposeAsync()
        {
            if (databaseName != null) { Assert.StartsWith("WeddingPlanTest_ComboFilters_", databaseName); await Db.Database.EnsureDeletedAsync(); }
            await Db.DisposeAsync(); if (connection != null) await connection.DisposeAsync();
        }
    }
}

internal static class ComboCatalogFilterFixtureHelpers
{
    public static void StageUnpublishedForTest(this ComboTemplate template, ApplicationDbContext db)
    {
        db.Entry(template).Property(t => t.ModerationStatus).CurrentValue = "Draft";
        db.Entry(template).Property(t => t.IsActive).CurrentValue = false;
    }
}
