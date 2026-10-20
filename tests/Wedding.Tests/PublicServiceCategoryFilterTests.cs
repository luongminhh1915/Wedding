using Microsoft.Data.SqlClient;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Wedding.Application.Features.Listings.Queries.GetPublicListings;
using Wedding.Domain.Entities;
using Wedding.Domain.Enums;
using Wedding.Domain.Exceptions;
using Wedding.Infrastructure.Persistence;

namespace Wedding.Tests;

public class PublicServiceCategoryFilterTests
{
    [Fact]
    public async Task CategoryKeywordRegionPriceAndEstablishedShopRatingAreCombined()
    {
        await using var f = await Fixture.Create();
        await AssertCombined(f);
    }

    private static async Task AssertCombined(Fixture f)
    {
        f.Db.ChangeTracker.Clear();
        var result = await f.Query(new(f.PhotoCategory, "  Hà Nội  ", 10000000, 15000000, " photo ", MinRating: 4.5m));
        var item = Assert.Single(result.Items);
        Assert.Equal(f.MatchingId, item.Id); Assert.Equal(1, result.TotalCount);
        Assert.Equal("Quay phim & Chụp ảnh (Photo / Video)", item.CategoryName);
        Assert.Null(item.RejectionReason); Assert.Empty(f.Db.ChangeTracker.Entries());
    }

    [Theory]
    [InlineData(-1, 10, null)] [InlineData(0, -1, null)] [InlineData(20, 10, null)]
    [InlineData(0, 10, -1)] [InlineData(0, 10, 6)]
    public async Task InvalidRangesAreRejectedInsteadOfProducingMisleadingResults(int min, int max, int? rating)
    {
        await using var f = await Fixture.Create();
        await Assert.ThrowsAsync<DomainException>(() => f.Query(new(null, null, min, max, null, MinRating: rating)));
        Assert.Equal(5, await f.Db.Listings.CountAsync());
    }

    [Fact]
    public async Task ExcessiveTextIsRejectedAndHugePageCannotOverflowSqlOffset()
    {
        await using var f = await Fixture.Create();
        await Assert.ThrowsAsync<DomainException>(() => f.Query(new(null, null, null, null, new string('x', 201))));
        await Assert.ThrowsAsync<DomainException>(() => f.Query(new(null, new string('x', 201), null, null, null)));
        var result = await f.Query(new(f.PhotoCategory, null, null, null, null, Page: int.MaxValue, PageSize: 1));
        Assert.Equal(3, result.TotalCount); Assert.Equal(3, result.Page); Assert.Single(result.Items);
        Assert.Equal(3, result.TotalPages);
    }

    [Fact]
    public async Task PublicSortHasStableTiesAndDraftsNeverAppear()
    {
        await using var f = await Fixture.Create();
        var first = await f.Query(new(f.PhotoCategory, null, null, null, null, PageSize: 1));
        var second = await f.Query(new(f.PhotoCategory, null, null, null, null, Page: 2, PageSize: 1));
        var all = await f.Query(new(f.PhotoCategory, null, null, null, null));
        Assert.NotEqual(first.Items[0].Id, second.Items[0].Id);
        Assert.Equal(all.Items[0].Id, first.Items[0].Id); Assert.Equal(all.Items[1].Id, second.Items[0].Id);
        Assert.DoesNotContain(all.Items, item => item.Status != "Active");
        var noMatches = await f.Query(new(f.PhotoCategory, null, null, null, "does-not-exist", Page: int.MaxValue));
        Assert.Empty(noMatches.Items); Assert.Equal(1, noMatches.Page);
    }

    [Fact]
    public async Task SqlServerExecutesCombinedFiltersAndPriceSortAgainstDisposableSchema()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("WEDDING_TEST_SQLSERVER"))) return;
        await using var f = await Fixture.Create(sql: true);
        await AssertCombined(f);
        var asc = await f.Query(new(f.PhotoCategory, null, null, null, null, SortBy: "price_asc"));
        Assert.Equal(asc.Items.OrderBy(item => item.MinPrice).Select(item => item.MinPrice), asc.Items.Select(item => item.MinPrice));
        var ascAgain = await f.Query(new(f.PhotoCategory, null, null, null, null, SortBy: "price_asc"));
        // SQL Server uniqueidentifier tie ordering differs from Guid.CompareTo in .NET.
        Assert.Equal(asc.Items.Select(item => item.Id), ascAgain.Items.Select(item => item.Id));
        var desc = await f.Query(new(f.PhotoCategory, null, null, null, null, SortBy: "price_desc"));
        Assert.Equal(desc.Items.OrderByDescending(item => item.MinPrice).Select(item => item.MinPrice), desc.Items.Select(item => item.MinPrice));
        var descAgain = await f.Query(new(f.PhotoCategory, null, null, null, null, SortBy: "price_desc"));
        Assert.Equal(desc.Items.Select(item => item.Id), descAgain.Items.Select(item => item.Id));
    }

    private sealed class Fixture(ApplicationDbContext db, SqliteConnection? connection, string? databaseName) : IAsyncDisposable
    {
        public ApplicationDbContext Db { get; } = db;
        public Guid PhotoCategory { get; } = Guid.Parse("11111111-1111-1111-1111-111111111103");
        public Guid MatchingId { get; private set; }
        public Task<PublicListingsResult> Query(GetPublicListingsQuery query) => new GetPublicListingsQueryHandler(Db).Handle(query, default);

        public static async Task<Fixture> Create(bool sql = false)
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>();
            SqliteConnection? connection = null; string? name = null;
            if (sql)
            {
                name = $"WeddingPlanTest_ServiceFilters_{Guid.NewGuid():N}";
                var source = Environment.GetEnvironmentVariable("WEDDING_TEST_SQLSERVER");
                var builder = new SqlConnectionStringBuilder(source == "1" || string.IsNullOrWhiteSpace(source)
                    ? "Server=localhost;Integrated Security=true;TrustServerCertificate=true;Connection Timeout=5" : source) { InitialCatalog = name };
                options.UseSqlServer(builder.ConnectionString);
            }
            else { connection = new SqliteConnection("Data Source=:memory:"); await connection.OpenAsync(); options.UseSqlite(connection); }
            var f = new Fixture(new ApplicationDbContext(options.Options), connection, name);
            try
            {
                if (sql) await f.Db.Database.MigrateAsync(); else await f.Db.Database.EnsureCreatedAsync();
                var owner = User.Create("Synthetic filter owner", "owner@filters.example.invalid", "0900000001", "hash", UserRole.VendorOwner);
                var newOwner = User.Create("Synthetic new owner", "new@filters.example.invalid", "0900000002", "hash", UserRole.VendorOwner);
                f.Db.Users.AddRange(owner, newOwner);
                var vendor = Vendor.Create(owner.Id, "Photo Filter Studio", "filter-studio", "Owner", "0900000001", "Hà Nội", .10m);
                var newVendor = Vendor.Create(newOwner.Id, "Photo New Studio", "filter-new-studio", "Owner", "0900000002", "Hà Nội", .10m);
                vendor.UpdateRating(4.7m, 5); newVendor.UpdateRating(5m, 2);
                f.Db.Vendors.AddRange(vendor, newVendor);
                Listing Add(string slug, Guid category, Vendor shop, string location, decimal min, decimal max, bool active = true)
                {
                    var item = Listing.Create(shop.Id, category, "Photo " + slug, slug, min, max, "Synthetic filter-only service", location);
                    if (active) item.SetStatus(ListingStatus.Active);
                    f.Db.Entry(item).Property(row => row.CreatedAt).CurrentValue = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
                    f.Db.Listings.Add(item); return item;
                }
                f.MatchingId = Add("matching-photo", f.PhotoCategory, vendor, "Hà Nội", 10000000, 20000000).Id;
                Add("wrong-category", Guid.Parse("11111111-1111-1111-1111-111111111102"), vendor, "Hà Nội", 10000000, 20000000);
                Add("wrong-region", f.PhotoCategory, vendor, "Hồ Chí Minh", 8000000, 12000000);
                Add("unestablished-rating", f.PhotoCategory, newVendor, "Hà Nội", 10000000, 20000000);
                Add("private-draft", f.PhotoCategory, vendor, "Hà Nội", 10000000, 20000000, false);
                await f.Db.SaveChangesAsync(); return f;
            }
            catch { await f.DisposeAsync(); throw; }
        }

        public async ValueTask DisposeAsync()
        {
            if (databaseName != null)
            {
                Assert.StartsWith("WeddingPlanTest_ServiceFilters_", databaseName);
                await Db.Database.EnsureDeletedAsync();
            }
            await Db.DisposeAsync(); if (connection != null) await connection.DisposeAsync();
        }
    }
}
