using System.Data.Common;
using System.Reflection;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Wedding.Application.Common.Interfaces;
using Wedding.Application.Features.Listings.DTOs;
using Wedding.Application.Features.Listings.Queries.GetVendorListings;
using Wedding.Domain.Entities;
using Wedding.Domain.Enums;
using Wedding.Domain.Exceptions;
using Wedding.Infrastructure.Persistence;
using Wedding.WebApi.Controllers;

namespace Wedding.Tests;

public class VendorListingPaginationTests
{
    private static readonly DateTime At = new(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task DefaultsEmptyAndBeyondLastPageRetainCorrectOwnerScopedCounts()
    {
        await using var f = await Fixture.Create();
        var empty = await f.Page(new());
        Assert.Empty(empty.Items); Assert.Equal(0, empty.TotalCount);
        Assert.Equal(1, empty.Page); Assert.Equal(10, empty.PageSize); Assert.Equal(1, empty.TotalPages);
        for (var index = 0; index < 15; index++) f.AddListing($"Own service {index}");
        for (var index = 0; index < 4; index++) f.AddListing($"Foreign service {index}", vendor: f.OtherVendor);
        await f.Db.SaveChangesAsync();
        var first = await f.Page(new());
        var second = await f.Page(new(Page: 2));
        Assert.Equal(15, first.TotalCount); Assert.Equal(2, first.TotalPages); Assert.Equal(10, first.Items.Count);
        Assert.Equal(15, second.TotalCount); Assert.Equal(2, second.Page); Assert.Equal(5, second.Items.Count);
        Assert.Empty(first.Items.Select(item => item.Id).Intersect(second.Items.Select(item => item.Id)));
        var beyond = await f.Page(new(Page: 3));
        Assert.Empty(beyond.Items); Assert.Equal(15, beyond.TotalCount); Assert.Equal(3, beyond.Page); Assert.Equal(2, beyond.TotalPages);
        var largestSize = await f.Page(new(PageSize: 50));
        Assert.Equal(15, largestSize.Items.Count); Assert.Equal(50, largestSize.PageSize); Assert.Equal(1, largestSize.TotalPages);
        var boundedLargePage = await f.Page(new(Page: int.MaxValue, PageSize: 1));
        Assert.Empty(boundedLargePage.Items); Assert.Equal(int.MaxValue, boundedLargePage.Page); Assert.Equal(15, boundedLargePage.TotalCount);
    }

    [Fact]
    public async Task KeywordAndStatusAreCombinedBeforeCountAndPagination()
    {
        await using var f = await Fixture.Create();
        await AssertCombinedFilters(f);
    }

    private static async Task AssertCombinedFilters(Fixture f)
    {
        var title = f.AddListing("Moonlight title", status: ListingStatus.Active);
        var location = f.AddListing("Location-only match", location: "Moonlight District", status: ListingStatus.Active);
        f.AddListing("Moonlight pending", status: ListingStatus.PendingApproval);
        f.AddListing("Nonmatching active", status: ListingStatus.Active);
        f.AddListing("Moonlight foreign", vendor: f.OtherVendor, status: ListingStatus.Active);
        await f.Db.SaveChangesAsync();
        f.Commands.Enabled = true; f.Commands.Statements.Clear();
        var request = new GetVendorListingsPageQuery("  mOoNlIgHt  ", ListingStatus.Active, PageSize: 1);
        var first = await f.Page(request);
        var second = await f.Page(request with { Page = 2 });
        Assert.Equal(2, first.TotalCount); Assert.Equal(2, first.TotalPages); Assert.Single(first.Items);
        Assert.Equal(2, second.TotalCount); Assert.Single(second.Items);
        Assert.Equal(new[] { title.Id, location.Id }.Order(), first.Items.Concat(second.Items).Select(item => item.Id).Order());
        Assert.All(first.Items.Concat(second.Items), item => Assert.Equal("Active", item.Status));
        Assert.Empty(first.Items.Select(item => item.Id).Intersect(second.Items.Select(item => item.Id)));
        var noStatus = await f.Page(request with { Status = null, PageSize = 10 });
        Assert.Equal(3, noStatus.TotalCount); Assert.Equal(3, noStatus.Items.Count);
        var noMatch = await f.Page(request with { Keyword = "No-such-service", Page = 4 });
        Assert.Empty(noMatch.Items); Assert.Equal(0, noMatch.TotalCount); Assert.Equal(4, noMatch.Page); Assert.Equal(1, noMatch.TotalPages);
    }

    [Theory]
    [InlineData("UniqueTitleMarker")]
    [InlineData("UniqueLocationMarker")]
    [InlineData("UniqueCategoryMarker")]
    public async Task KeywordSearchSupportsTitleLocationAndCategoryName(string keyword)
    {
        await using var f = await Fixture.Create();
        var category = Category.Create("UniqueCategoryMarker", "unique-search-category", null, null, .08m);
        f.Db.Categories.Add(category);
        var title = f.AddListing("UniqueTitleMarker");
        var location = f.AddListing("Location match", location: "UniqueLocationMarker");
        var categoryMatch = f.AddListing("Category match", category: category);
        await f.Db.SaveChangesAsync();
        var page = await f.Page(new(Keyword: keyword.ToLowerInvariant()));
        var expected = keyword == "UniqueTitleMarker" ? title.Id : keyword == "UniqueLocationMarker" ? location.Id : categoryMatch.Id;
        Assert.Equal(1, page.TotalCount); Assert.Equal(expected, Assert.Single(page.Items).Id);
        Assert.Empty((await f.Page(new(Keyword: "Description-only fixture text"))).Items);
    }

    [Theory]
    [InlineData(ListingStatus.Draft)]
    [InlineData(ListingStatus.PendingApproval)]
    [InlineData(ListingStatus.Active)]
    [InlineData(ListingStatus.Rejected)]
    [InlineData(ListingStatus.Hidden)]
    public async Task EveryDefinedStatusCanBeFilteredWithoutHidingOwnPrivateRows(ListingStatus status)
    {
        await using var f = await Fixture.Create();
        var own = new List<Listing>();
        foreach (var value in Enum.GetValues<ListingStatus>()) own.Add(f.AddListing($"Own {value}", status: value));
        f.AddListing("Foreign same status", vendor: f.OtherVendor, status: status);
        await f.Db.SaveChangesAsync();
        var page = await f.Page(new(Status: status));
        Assert.Equal(1, page.TotalCount); Assert.Equal(own.Single(item => item.Status == status).Id, Assert.Single(page.Items).Id);
        Assert.Equal(status.ToString(), page.Items[0].Status);
        if (status == ListingStatus.Rejected) Assert.Equal("Own synthetic moderation reason", page.Items[0].RejectionReason);
    }

    [Fact]
    public async Task SortingUsesUpdatedAtThenCreatedAtFallbackAndStableAscendingIdForTies()
    {
        await using var f = await Fixture.Create();
        var oldButUpdated = f.AddListing("Updated old service", createdAt: At.AddDays(-20), updatedAt: At.AddDays(3));
        var recentUnedited = f.AddListing("Recently created service", createdAt: At.AddDays(2));
        var tieB = f.AddListing("Tie B", id: Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-000000000002"), createdAt: At.AddDays(1));
        var tieA = f.AddListing("Tie A", id: Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-000000000001"), createdAt: At.AddDays(-5), updatedAt: At.AddDays(1));
        var oldest = f.AddListing("Unedited oldest", createdAt: At.AddDays(-10));
        f.Db.ListingMedias.AddRange(
            ListingMedia.Create(oldButUpdated.Id, "/uploads/listings/not-featured.webp", null, displayOrder: 0),
            ListingMedia.Create(oldButUpdated.Id, "/uploads/listings/featured-later.webp", null, displayOrder: 3, isFeatured: true),
            ListingMedia.Create(oldButUpdated.Id, "/uploads/listings/featured-first.webp", null, displayOrder: 1, isFeatured: true));
        await f.Db.SaveChangesAsync();
        var first = await f.Page(new(PageSize: 2));
        var second = await f.Page(new(Page: 2, PageSize: 2));
        var third = await f.Page(new(Page: 3, PageSize: 2));
        Assert.Equal(new[] { oldButUpdated.Id, recentUnedited.Id, tieA.Id, tieB.Id, oldest.Id },
            first.Items.Concat(second.Items).Concat(third.Items).Select(item => item.Id));
        Assert.Equal(At.AddDays(3), first.Items[0].UpdatedAt); Assert.Null(first.Items[1].UpdatedAt);
        Assert.Equal("/uploads/listings/featured-first.webp", first.Items[0].PrimaryImageUrl);
        Assert.Null(first.Items[1].PrimaryImageUrl);
        var repeated = await f.Page(new(Page: 2, PageSize: 2));
        Assert.Equal(second.Items.Select(item => item.Id), repeated.Items.Select(item => item.Id));
    }

    [Theory]
    [InlineData(0, 10)] [InlineData(-1, 10)] [InlineData(1, 0)] [InlineData(1, -1)]
    [InlineData(1, 51)] [InlineData(int.MaxValue, 50)] [InlineData(int.MaxValue, 2)]
    public async Task InvalidPagingAndOverflowingOffsetsAreRejectedInsteadOfClamped(int page, int pageSize)
    {
        await using var f = await Fixture.Create();
        await Assert.ThrowsAsync<DomainException>(() => f.Page(new(Page: page, PageSize: pageSize)));
    }

    [Theory]
    [InlineData(0)] [InlineData(-1)] [InlineData(6)] [InlineData(int.MaxValue)]
    public async Task UndefinedStatusValuesAreRejected(int status)
    {
        await using var f = await Fixture.Create();
        await Assert.ThrowsAsync<DomainException>(() => f.Page(new(Status: (ListingStatus)status)));
    }

    [Fact]
    public async Task KeywordLimitIsValidatedAfterTrimAndBlankKeywordActsAsNoFilter()
    {
        await using var f = await Fixture.Create();
        f.AddListing(new string('x', 200)); await f.Db.SaveChangesAsync();
        Assert.Equal(1, (await f.Page(new(Keyword: "   " + new string('x', 200) + "   "))).TotalCount);
        Assert.Equal(1, (await f.Page(new(Keyword: new string(' ', 220)))).TotalCount);
        Assert.Equal(1, (await f.Page(new(Keyword: "  \t\r\n "))).TotalCount);
        await Assert.ThrowsAsync<DomainException>(() => f.Page(new(Keyword: new string('x', 201))));
    }

    [Fact]
    public async Task MissingAnonymousAndForgedRoleContextsNeverReadListingCounts()
    {
        await using var f = await Fixture.Create();
        f.AddListing("Private service"); await f.Db.SaveChangesAsync();
        f.Commands.Enabled = true; f.Commands.Statements.Clear();
        foreach (var actor in new[]
        {
            new Current(null, null), new Current(null), new Current(Guid.Empty), new Current(f.Owner.Id, "Customer"),
            new Current(f.Owner.Id, "Moderator"), new Current(f.Owner.Id, "Finance"), new Current(f.Owner.Id, "CustomerCare"),
            new Current(f.Owner.Id, "SuperAdmin"), new Current(f.Owner.Id, "vendorowner")
        })
            await Assert.ThrowsAsync<DomainException>(() => new GetVendorListingsPageQueryHandler(f.Db, actor).Handle(new(), default));
        Assert.DoesNotContain(f.Commands.Statements, sql => sql.Contains("Listings", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("disabled-owner")] [InlineData("disabled-shop")] [InlineData("changed-db-role")]
    [InlineData("missing-shop")] [InlineData("unknown-actor")]
    public async Task CurrentDatabaseActorAndShopMustBeActiveAndOwned(string condition)
    {
        await using var f = await Fixture.Create();
        f.AddListing("Private service");
        var userId = f.Owner.Id;
        if (condition == "disabled-owner") f.Owner.SetStatus(false);
        if (condition == "disabled-shop") f.Db.Entry(f.Vendor).Property(vendor => vendor.IsActive).CurrentValue = false;
        if (condition == "changed-db-role") f.Db.Entry(f.Owner).Property(user => user.Role).CurrentValue = UserRole.Customer;
        if (condition == "missing-shop")
        {
            var owner = User.Create("Unmapped synthetic owner", "missing-shop@example.invalid", "0901234568", "hash", UserRole.VendorOwner);
            f.Db.Users.Add(owner); userId = owner.Id;
        }
        if (condition == "unknown-actor") userId = Guid.NewGuid();
        await f.Db.SaveChangesAsync();
        f.Commands.Enabled = true; f.Commands.Statements.Clear();
        await Assert.ThrowsAsync<DomainException>(() => new GetVendorListingsPageQueryHandler(f.Db,
            new Current(userId)).Handle(new(), default));
        Assert.DoesNotContain(f.Commands.Statements, sql => sql.Contains("Listings", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ForeignPrivateMatchesNeverContributeToItemsCountOrModerationDetails()
    {
        await using var f = await Fixture.Create();
        var own = f.AddListing("Own private draft");
        var foreign = f.AddListing("Foreign secret keyword", vendor: f.OtherVendor, status: ListingStatus.Rejected);
        foreign.Reject("Foreign private moderation evidence"); await f.Db.SaveChangesAsync();
        var all = await f.Page(new());
        Assert.Equal(1, all.TotalCount); Assert.Equal(own.Id, Assert.Single(all.Items).Id);
        var match = await f.Page(new(Keyword: "Foreign secret keyword", Status: ListingStatus.Rejected));
        Assert.Equal(0, match.TotalCount); Assert.Empty(match.Items);
        var json = JsonSerializer.Serialize(all);
        Assert.DoesNotContain(foreign.Id.ToString(), json); Assert.DoesNotContain(f.OtherVendor.BrandName, json);
        Assert.DoesNotContain("Foreign private moderation evidence", json);
        var other = await new GetVendorListingsPageQueryHandler(f.Db, new Current(f.OtherOwner.Id)).Handle(new(), default);
        Assert.Equal(foreign.Id, Assert.Single(other.Items).Id); Assert.Equal(1, other.TotalCount);
    }

    [Fact]
    public async Task LegacyArrayEndpointRetainsAllOwnRowsAndExposesOptionalUpdatedAtWithoutPagingShape()
    {
        await using var f = await Fixture.Create();
        var own = f.AddListing("Own updated service", createdAt: At.AddDays(-1), updatedAt: At);
        f.AddListing("Own second service", status: ListingStatus.Hidden);
        f.AddListing("Foreign service", vendor: f.OtherVendor); await f.Db.SaveChangesAsync();
        var legacy = await new GetVendorListingsQueryHandler(f.Db, new Current(f.Owner.Id)).Handle(new(), default);
        Assert.IsType<List<ListingSummaryDto>>(legacy); Assert.Equal(2, legacy.Count);
        Assert.Equal(At, legacy.Single(item => item.Id == own.Id).UpdatedAt);
        using var document = JsonDocument.Parse(JsonSerializer.Serialize(legacy, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
        Assert.Equal(JsonValueKind.Array, document.RootElement.ValueKind);
        var paged = await f.Page(new(PageSize: 1));
        Assert.Equal(2, paged.TotalCount); Assert.Single(paged.Items);
        Assert.DoesNotContain(typeof(GetVendorListingsPageQuery).GetProperties(), property => property.Name is "VendorId" or "UserId");
    }

    [Theory]
    [InlineData(null, false)] [InlineData("Customer", false)] [InlineData("Moderator", false)]
    [InlineData("Finance", false)] [InlineData("CustomerCare", false)] [InlineData("SuperAdmin", false)]
    [InlineData("VendorOwner", true)]
    public async Task PageEndpointRequiresAuthenticatedVendorOwner(string? role, bool allowed)
    {
        using var services = new ServiceCollection().AddLogging().AddAuthorizationCore().BuildServiceProvider();
        var method = typeof(ListingsController).GetMethod(nameof(ListingsController.GetMyListingsPage))!;
        Assert.Equal("my-listings/page", method.GetCustomAttribute<HttpGetAttribute>()?.Template);
        Assert.Empty(method.GetCustomAttributes<AllowAnonymousAttribute>());
        var data = typeof(ListingsController).GetCustomAttributes<AuthorizeAttribute>()
            .Concat(method.GetCustomAttributes<AuthorizeAttribute>()).Cast<IAuthorizeData>();
        var policy = await AuthorizationPolicy.CombineAsync(services.GetRequiredService<IAuthorizationPolicyProvider>(), data);
        Assert.NotNull(policy);
        var principal = role == null ? new ClaimsPrincipal(new ClaimsIdentity()) :
            new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Role, role)], "SyntheticAuthentication"));
        Assert.Equal(allowed, (await services.GetRequiredService<IAuthorizationService>().AuthorizeAsync(principal, null, policy)).Succeeded);
    }

    [SqlServerFact]
    public async Task SqlServerFiltersCountsAndPagesInSqlRatherThanLoadingWholeCatalog()
    {
        await using var f = await Fixture.Create(sql: true);
        await AssertCombinedFilters(f);
        Assert.Contains(f.Commands.Statements, sql => sql.Contains("COUNT(*)", StringComparison.OrdinalIgnoreCase) &&
            sql.Contains("[VendorId]", StringComparison.Ordinal) && sql.Contains("[Status]", StringComparison.Ordinal));
        Assert.Contains(f.Commands.Statements, sql => sql.Contains("OFFSET", StringComparison.OrdinalIgnoreCase) &&
            sql.Contains("FETCH NEXT", StringComparison.OrdinalIgnoreCase) && sql.Contains("LOWER", StringComparison.OrdinalIgnoreCase) &&
            sql.Contains("[VendorId]", StringComparison.Ordinal) && sql.Contains("[Status]", StringComparison.Ordinal));
        var legacy = await new GetVendorListingsQueryHandler(f.Db, new Current(f.Owner.Id)).Handle(new(), default);
        Assert.Equal(4, legacy.Count);
    }

    private sealed record Current(Guid? UserId, string? Role = "VendorOwner") : ICurrentUserService { public string? Email => null; }

    private sealed class CommandCapture : DbCommandInterceptor
    {
        public bool Enabled { get; set; }
        public List<string> Statements { get; } = [];
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,
            CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            if (Enabled) Statements.Add(command.CommandText); // Never capture credential/PII parameter values.
            return ValueTask.FromResult(result);
        }
    }

    private sealed class Fixture(ApplicationDbContext db, SqliteConnection? connection, string? databaseName,
        CommandCapture commands) : IAsyncDisposable
    {
        public ApplicationDbContext Db { get; } = db;
        public CommandCapture Commands { get; } = commands;
        public User Owner { get; private set; } = null!;
        public User OtherOwner { get; private set; } = null!;
        public Vendor Vendor { get; private set; } = null!;
        public Vendor OtherVendor { get; private set; } = null!;
        public Category Category { get; private set; } = null!;
        public Task<VendorListingsPageDto> Page(GetVendorListingsPageQuery request) =>
            new GetVendorListingsPageQueryHandler(Db, new Current(Owner.Id)).Handle(request, default);

        public static async Task<Fixture> Create(bool sql = false)
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>();
            SqliteConnection? connection = null; string? databaseName = null;
            if (sql)
            {
                databaseName = $"WeddingPlanTest_VendorListings_{Guid.NewGuid():N}";
                var source = Environment.GetEnvironmentVariable("WEDDING_TEST_SQLSERVER");
                var builder = new SqlConnectionStringBuilder(string.IsNullOrWhiteSpace(source) || source == "1"
                    ? "Server=localhost;Integrated Security=true;TrustServerCertificate=true;Connection Timeout=5" : source)
                    { InitialCatalog = databaseName };
                options.UseSqlServer(builder.ConnectionString);
            }
            else
            {
                connection = new SqliteConnection("Data Source=:memory:"); await connection.OpenAsync(); options.UseSqlite(connection);
            }
            var commands = new CommandCapture(); options.AddInterceptors(commands);
            var f = new Fixture(new ApplicationDbContext(options.Options), connection, databaseName, commands);
            try
            {
                if (sql) await f.Db.Database.MigrateAsync(); else await f.Db.Database.EnsureCreatedAsync();
                f.Owner = User.Create("Synthetic shop owner", "owner@vendor-page.example.invalid", "0901234567", "hash", UserRole.VendorOwner);
                f.OtherOwner = User.Create("Other synthetic owner", "other@vendor-page.example.invalid", "0901234568", "hash", UserRole.VendorOwner);
                f.Db.Users.AddRange(f.Owner, f.OtherOwner);
                f.Vendor = Vendor.Create(f.Owner.Id, "Own synthetic shop", "own-vendor-page-shop", "Owner", "0901234567", "Hà Nội", .08m);
                f.OtherVendor = Vendor.Create(f.OtherOwner.Id, "Foreign synthetic shop", "foreign-vendor-page-shop", "Other", "0901234568", "Hà Nội", .08m);
                f.Db.Vendors.AddRange(f.Vendor, f.OtherVendor);
                f.Category = Category.Create("Synthetic service category", "vendor-page-fixture-category", null, null, .08m);
                f.Db.Categories.Add(f.Category); await f.Db.SaveChangesAsync(); return f;
            }
            catch { await f.DisposeAsync(); throw; }
        }

        public Listing AddListing(string title, Vendor? vendor = null, Category? category = null, string location = "Hà Nội",
            ListingStatus status = ListingStatus.Draft, DateTime? createdAt = null, DateTime? updatedAt = null, Guid? id = null)
        {
            var listing = Listing.Create((vendor ?? Vendor).Id, (category ?? Category).Id, title, $"synthetic-page-service-{Guid.NewGuid():N}",
                1000000, 2000000, "Description-only fixture text", location);
            if (id.HasValue) Db.Entry(listing).Property(item => item.Id).CurrentValue = id.Value;
            if (status == ListingStatus.Rejected) listing.Reject("Own synthetic moderation reason");
            else if (status != ListingStatus.Draft) listing.SetStatus(status);
            Db.Entry(listing).Property(item => item.CreatedAt).CurrentValue = createdAt ?? At;
            Db.Entry(listing).Property(item => item.UpdatedAt).CurrentValue = updatedAt;
            Db.Listings.Add(listing); return listing;
        }

        public async ValueTask DisposeAsync()
        {
            if (databaseName != null)
            {
                Assert.StartsWith("WeddingPlanTest_VendorListings_", databaseName);
                await Db.Database.EnsureDeletedAsync();
            }
            await Db.DisposeAsync(); if (connection != null) await connection.DisposeAsync();
        }
    }
}
