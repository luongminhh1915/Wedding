using System.Text.Json;
using Microsoft.Data.SqlClient;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Wedding.Application.Common.Interfaces;
using Wedding.Application.Features.Combos;
using Wedding.Application.Features.Reviews;
using Wedding.Application.Features.Reviews.DTOs;
using Wedding.Domain.Entities;
using Wedding.Domain.Enums;
using Wedding.Domain.Exceptions;
using Wedding.Infrastructure.Persistence;

namespace Wedding.Tests;

public class IndependentReviewPolicyGuardTests
{
    private const string Original = "Synthetic approved review retained while the purchased package is temporarily unavailable.";
    private const string Proposed = "Synthetic proposed edit must remain private while the relevant transaction is disputed.";

    public static IEnumerable<object[]> HiddenPackages()
    {
        foreach (var fixedPackage in new[] { false, true })
            foreach (var reason in new[] { "template", "moderation", "future", "expired", "listing", "listing-status",
                         "service", "service-status", "vendor", "owner", "group", "category", "consent", "verification" })
                yield return [fixedPackage, reason];
        yield return [true, "source-version"];
    }

    [Theory]
    [MemberData(nameof(HiddenPackages))]
    public async Task UnavailableComboHidesItsOldPublicReviewsButPreservesCustomerHistory(bool fixedPackage, string reason)
    {
        await using var f = await Fixture.Create();
        await VerifyHiddenPackage(f, fixedPackage, reason);
    }

    private static async Task VerifyHiddenPackage(Fixture f, bool fixedPackage, string reason)
    {
        var template = fixedPackage ? f.Fixed : f.Dynamic;
        var review = (await f.AddReview("Combo", template)).Review;
        var publicBefore = await f.PublicCombo(template.Id);
        Assert.Equal(review.Id, Assert.Single(publicBefore.Reviews).Id);
        Assert.Equal(4m, publicBefore.Summary.AverageRating);
        var storedBefore = await f.StoredReview(review.Id);
        await f.Hide(template, reason);
        await Assert.ThrowsAsync<NotFoundException>(() => f.PublicCombo(template.Id));
        Assert.Equal(storedBefore, await f.StoredReview(review.Id));
        var mine = Assert.Single(await f.MyReviews(), item => item.Id == review.Id);
        Assert.Equal("Approved", mine.Status);
        Assert.Equal(Original, mine.Content);
        Assert.Equal(1, await f.Db.Reviews.CountAsync(item => item.Id == review.Id));
        Assert.Empty(await f.Db.ReviewEditRequests.Where(item => item.ReviewId == review.Id).ToListAsync());
    }

    [Theory]
    [InlineData("Service")]
    [InlineData("Shop")]
    [InlineData("ShopCombo")]
    [InlineData("Combo")]
    public async Task DisputedEvidenceHoldsPendingEditAndAllowsRejectionWithoutPublishing(string target)
    {
        await using var f = await Fixture.Create();
        await VerifyDisputeHold(f, target);
    }

    private static async Task VerifyDisputeHold(Fixture f, string target)
    {
        var (review, contracts) = await f.AddReview(target, f.Dynamic);
        await f.Edit(review.Id);
        var pending = await f.Db.ReviewEditRequests.AsNoTracking().SingleAsync(item => item.ReviewId == review.Id);
        contracts[0].MarkDisputed(); await f.Db.SaveChangesAsync();
        var original = await f.StoredReview(review.Id);
        var error = await Assert.ThrowsAsync<DomainException>(() => f.Moderate(pending.Id, true));
        Assert.Contains("tranh chấp", error.Message);
        Assert.Equal(original, await f.StoredReview(review.Id));
        var held = await f.Db.ReviewEditRequests.AsNoTracking().SingleAsync(item => item.Id == pending.Id);
        Assert.Equal(ReviewEditStatus.Pending, held.Status);
        Assert.Null(held.ReviewedAt); Assert.Null(held.ReviewedById);
        Assert.Equal(Original, held.BeforeContent); Assert.Equal(Proposed, held.AfterContent);
        var mine = Assert.Single(await f.MyReviews(), item => item.Id == review.Id);
        Assert.Equal("Pending", mine.EditStatus); Assert.Equal(Original, mine.Content);

        await f.Moderate(pending.Id, false);
        Assert.Equal(original, await f.StoredReview(review.Id));
        var rejected = await f.Db.ReviewEditRequests.AsNoTracking().SingleAsync(item => item.Id == pending.Id);
        Assert.Equal(ReviewEditStatus.Rejected, rejected.Status);
        Assert.Equal(f.Moderator.Id, rejected.ReviewedById);
        Assert.NotNull(rejected.ReviewedAt); Assert.NotNull(rejected.RejectionReason);
        Assert.Equal(Original, rejected.BeforeContent); Assert.Equal(Proposed, rejected.AfterContent);
    }

    [Theory]
    [InlineData("Service")]
    [InlineData("Shop")]
    [InlineData("ShopCombo")]
    [InlineData("Combo")]
    public async Task UnrelatedCustomerDisputeDoesNotBlockAnotherCustomersEdit(string target)
    {
        await using var f = await Fixture.Create();
        var (review, _) = await f.AddReview(target, f.Dynamic);
        var unrelated = f.AddContract(f.First.ListingId, null, f.OtherCustomer);
        unrelated.MarkDisputed(); await f.Db.SaveChangesAsync();
        await f.Edit(review.Id);
        var pending = await f.Db.ReviewEditRequests.SingleAsync(item => item.ReviewId == review.Id);
        var result = await f.Moderate(pending.Id, true);
        Assert.Equal("Approved", result.EditStatus); Assert.Equal(Proposed, result.Content);
    }

    [Fact]
    public async Task HeldEditCanBeApprovedAfterRelevantDisputeIsResolved()
    {
        await using var f = await Fixture.Create();
        var (review, contracts) = await f.AddReview("Combo", f.Dynamic);
        await f.Edit(review.Id);
        var pending = await f.Db.ReviewEditRequests.SingleAsync(item => item.ReviewId == review.Id);
        contracts[0].MarkDisputed(); await f.Db.SaveChangesAsync();
        await Assert.ThrowsAsync<DomainException>(() => f.Moderate(pending.Id, true));
        var contract = await f.Db.BookingContracts.SingleAsync(item => item.Id == contracts[0].Id);
        contract.ResolveDispute(false, "Synthetic test-only resolution"); await f.Db.SaveChangesAsync();
        var result = await f.Moderate(pending.Id, true);
        Assert.Equal(Proposed, result.Content); Assert.Equal("Approved", result.EditStatus);
        Assert.Single(await f.Db.ReviewEditRequests.Where(item => item.ReviewId == review.Id).ToListAsync());
    }

    [SqlServerFact]
    public async Task SqlPublicDependencyGuardPreservesOldReviewAndDisputeHoldPreservesPendingHistory()
    {
        await using var f = await Fixture.Create(sql: true);
        await VerifyHiddenPackage(f, true, "group");
        var group = await f.Db.BusinessGroups.SingleAsync(item => item.Id == f.Category.BusinessGroupId);
        f.Db.Entry(group).Property(item => item.IsActive).CurrentValue = true; await f.Db.SaveChangesAsync();
        await VerifyDisputeHold(f, "ShopCombo");
    }

    private sealed record Actor(Guid? UserId, string? Role) : ICurrentUserService { public string? Email => null; }
    private sealed class Audit : IAuditLogger
    { public Task LogAsync(string action, string entityType, string? entityId, object? data = null) => Task.CompletedTask; }

    private sealed class Fixture(ApplicationDbContext db, SqliteConnection? sqlite, string? databaseName) : IAsyncDisposable
    {
        private const string Prefix = "WeddingPlanTest_ReviewPolicy_";
        public ApplicationDbContext Db => db;
        public User Customer { get; private set; } = null!;
        public User OtherCustomer { get; private set; } = null!;
        public User Moderator { get; private set; } = null!;
        public User Owner { get; private set; } = null!;
        public Vendor Vendor { get; private set; } = null!;
        public Category Category { get; private set; } = null!;
        public ServiceItem First { get; private set; } = null!;
        public ServiceItem Second { get; private set; } = null!;
        public ComboTemplate Fixed { get; private set; } = null!;
        public ComboTemplate Dynamic { get; private set; } = null!;

        public Task<ReviewPageDto> PublicCombo(Guid id) => new GetIndependentReviewsQueryHandler(db).Handle(new("Combo", id), default);
        public Task<List<ReviewDto>> MyReviews() => new GetMyReviewsQueryHandler(db, new Actor(Customer.Id, "Customer")).Handle(new(), default);
        public Task<ReviewDto> Edit(Guid id) => new SubmitReviewEditRequestCommandHandler(db, new Actor(Customer.Id, "Customer"))
            .Handle(new(id, 2, Proposed), default);
        public Task<ReviewDto> Moderate(Guid id, bool approve) => new ModerateReviewEditRequestCommandHandler(db, new Actor(Moderator.Id, "Moderator"))
            .Handle(new(id, approve, approve ? null : "Synthetic rejection preserves the original approved review."), default);
        public async Task<string> StoredReview(Guid id) => JsonSerializer.Serialize(await db.Reviews.AsNoTracking()
            .Where(item => item.Id == id).Select(item => new { item.Id, item.TargetType, item.ComboOrderId, item.ComboTemplateId,
                item.BookingContractId, item.ListingId, item.CustomerId, item.VendorId, item.Rating, item.Content, item.PhotosJson,
                item.QualityRating, item.ServiceRating, item.PunctualityRating, item.ValueRating, item.Status,
                item.VendorReply, item.VendorRepliedAt, item.CreatedAt, item.UpdatedAt }).SingleAsync());

        public static async Task<Fixture> Create(bool sql = false)
        {
            SqliteConnection? sqlite = null; string? name = null;
            var options = new DbContextOptionsBuilder<ApplicationDbContext>();
            if (sql)
            {
                Assert.False(string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("WEDDING_TEST_SQLSERVER")));
                name = Prefix + Guid.NewGuid().ToString("N");
                var target = new SqlConnectionStringBuilder { DataSource = "localhost", InitialCatalog = name,
                    IntegratedSecurity = true, TrustServerCertificate = true, ConnectTimeout = 5 };
                await using var master = new SqlConnection(new SqlConnectionStringBuilder(target.ConnectionString) { InitialCatalog = "master" }.ConnectionString);
                await master.OpenAsync();
                await using (var exists = master.CreateCommand())
                {
                    exists.CommandText = "SELECT DB_ID(@database)"; exists.Parameters.AddWithValue("@database", name);
                    Assert.Equal(DBNull.Value, await exists.ExecuteScalarAsync());
                }
                await using (var create = master.CreateCommand())
                { create.CommandText = $"CREATE DATABASE [{name}]"; await create.ExecuteNonQueryAsync(); }
                options.UseSqlServer(target.ConnectionString);
            }
            else { sqlite = new("Data Source=:memory:"); await sqlite.OpenAsync(); options.UseSqlite(sqlite); }
            var f = new Fixture(new ApplicationDbContext(options.Options), sqlite, name);
            try
            {
                if (sql) { f.AssertOwnedTarget(); await f.Db.Database.MigrateAsync(); }
                else await f.Db.Database.EnsureCreatedAsync();
                User User(UserRole role)
                {
                    var user = Wedding.Domain.Entities.User.Create("Synthetic review policy actor", $"{Guid.NewGuid():N}@review-policy.example.invalid", "0900000000", "synthetic-no-login", role);
                    f.Db.Users.Add(user); return user;
                }
                f.Owner = User(UserRole.VendorOwner); f.Customer = User(UserRole.Customer);
                f.OtherCustomer = User(UserRole.Customer); f.Moderator = User(UserRole.Moderator);
                f.Vendor = Vendor.Create(f.Owner.Id, "Synthetic review policy shop", "review-policy-" + Guid.NewGuid().ToString("N"), "Owner", "0900000000", "HCM", .08m);
                f.Vendor.SetVerified(true); f.Vendor.ConfigureComboParticipation(true, .10m); f.Db.Vendors.Add(f.Vendor);
                f.Category = await f.Db.Categories.Include(item => item.BusinessGroup).SingleAsync(item => item.Slug == "quay-chup-photo-video");
                ServiceItem Service(string name, decimal price)
                {
                    var listing = Listing.Create(f.Vendor.Id, f.Category.Id, name, "review-policy-" + Guid.NewGuid().ToString("N"), price, price, "Synthetic approved source", "HCM");
                    listing.Approve(); f.Db.Listings.Add(listing);
                    var service = ServiceItem.Create(f.Vendor.Id, listing.Id, f.Category.Id, name, price, "Package", 1, 1000, "HCM", "Synthetic conditions");
                    service.Submit(); service.Review(true, null); listing.AttachPricingServiceItem(service.Id); f.Db.ServiceItems.Add(service); return service;
                }
                f.First = Service("First review source", 1_000_000); f.Second = Service("Second review source", 2_000_000);
                await f.Db.SaveChangesAsync();
                ComboTemplateItemDto Slot(ServiceItem service) => new(service.CategoryId, service.ListingId, false, service.Id, []);
                var saved = await new SaveComboTemplateCommandHandler(f.Db, new Audit(), new Actor(f.Owner.Id, "VendorOwner"))
                    .Handle(new(null, "Synthetic fixed review package", "Popular", "Classic", "Synthetic package", null, true, 0,
                        [Slot(f.First), Slot(f.Second)], "HCM", PricingMode: "FixedPrice", ComboPrice: 2_500_000), default);
                await new ReviewComboTemplateCommandHandler(f.Db, new Audit(), new Actor(f.Moderator.Id, "Moderator"))
                    .Handle(new(saved.Id, true, RevisionId: saved.LatestRevision!.Id), default);
                f.Fixed = await f.Db.ComboTemplates.Include(item => item.Items).SingleAsync(item => item.Id == saved.Id);
                f.Dynamic = ComboTemplate.Create("Synthetic dynamic review package", "Popular", "Classic", "Synthetic package", null, 0);
                f.Dynamic.SetScope(f.Vendor.Id, "HCM", 0m, true);
                var slot = ComboTemplateItem.Create(f.Dynamic.Id, f.Category.Id, f.First.ListingId, false);
                slot.ConfigureItems(f.First.Id, "[]"); f.Dynamic.Items.Add(slot); f.Db.ComboTemplates.Add(f.Dynamic);
                await f.Db.SaveChangesAsync(); return f;
            }
            catch { await f.DisposeAsync(); throw; }
        }

        public BookingContract AddContract(Guid listingId, ComboOrder? order = null, User? customer = null)
        {
            customer ??= Customer;
            var lead = Lead.Create(customer.Id, Vendor.Id, listingId, "0900000000", DateTime.UtcNow.AddDays(-10), 100, 1_000_000, "Synthetic review evidence");
            if (order != null) { lead.AttachSource("Combo", order.Id); order.Items.Single(item => item.ListingId == listingId).AttachLead(lead.Id); }
            db.Leads.Add(lead);
            var contract = BookingContract.Create(lead.Id, null, Vendor.Id, customer.Id, 1_000_000, 0, null, lead.WeddingDate);
            contract.ConfirmByCustomer(); contract.CompleteContract();
            db.Entry(contract).Property(item => item.ContractCode).CurrentValue = "HD-POLICY-" + Guid.NewGuid().ToString("N");
            db.BookingContracts.Add(contract); return contract;
        }

        public async Task<(Review Review, List<BookingContract> Contracts)> AddReview(string target, ComboTemplate template)
        {
            ComboOrder? order = null; var contracts = new List<BookingContract>();
            if (target is "Combo" or "ShopCombo")
            {
                var services = template.PricingMode == "FixedPrice" ? new[] { First, Second } : [First];
                order = ComboOrder.Create(Customer.Id, template.Id, DateTime.UtcNow.AddDays(-10), 100, "Synthetic review policy fixture",
                    services.Select(item => (item.ListingId, item.VendorId, item.CategoryId, item.Name, item.BasePrice)).ToList(), template.PricingMode == "FixedPrice");
                db.ComboOrders.Add(order);
                foreach (var service in services) contracts.Add(AddContract(service.ListingId, order));
            }
            else contracts.Add(AddContract(First.ListingId));
            var review = target switch
            {
                "Service" => Review.Create(contracts[0].Id, First.ListingId, Customer.Id, Vendor.Id, 4, Original, "[]", true),
                "Shop" => Review.CreateShop(contracts[0].Id, null, Customer.Id, Vendor.Id, 4, Original, "[]"),
                "ShopCombo" => Review.CreateShop(null, order!.Id, Customer.Id, Vendor.Id, 4, Original, "[]"),
                _ => Review.CreateCombo(order!.Id, template.Id, Customer.Id, Vendor.Id, 4, Original, "[]")
            };
            review.Approve(); db.Reviews.Add(review); await db.SaveChangesAsync(); return (review, contracts);
        }

        public async Task Hide(ComboTemplate template, string reason)
        {
            switch (reason)
            {
                case "template": template.SetActivity(false); break;
                case "moderation": db.Entry(template).Property(item => item.ModerationStatus).CurrentValue = "Hidden"; break;
                case "future": db.Entry(template).Property(item => item.ValidFromUtc).CurrentValue = DateTime.UtcNow.AddDays(1); break;
                case "expired": db.Entry(template).Property(item => item.ValidUntilUtc).CurrentValue = DateTime.UtcNow.AddDays(-1); break;
                case "listing": First.Listing.SetActivity(false); break;
                case "listing-status": db.Entry(First.Listing).Property(item => item.Status).CurrentValue = ListingStatus.Hidden; break;
                case "service": db.Entry(First).Property(item => item.IsActive).CurrentValue = false; break;
                case "service-status": db.Entry(First).Property(item => item.ModerationStatus).CurrentValue = "Rejected"; break;
                case "vendor": db.Entry(Vendor).Property(item => item.IsActive).CurrentValue = false; break;
                case "owner": db.Entry(Owner).Property(item => item.IsActive).CurrentValue = false; break;
                case "group": db.Entry(Category.BusinessGroup!).Property(item => item.IsActive).CurrentValue = false; break;
                case "category": db.Entry(Category).Property(item => item.IsActive).CurrentValue = false; break;
                case "consent": Vendor.ConfigureComboParticipation(false, .10m); break;
                case "verification": Vendor.SetVerified(false); break;
                case "source-version": db.Entry(First.Listing).Property(item => item.Description).CurrentValue = "Changed source requires renewed fixed package approval"; break;
                default: throw new InvalidOperationException("Unknown synthetic hidden case.");
            }
            await db.SaveChangesAsync();
        }

        private void AssertOwnedTarget()
        {
            Assert.NotNull(databaseName); Assert.StartsWith(Prefix, databaseName);
            Assert.True(Guid.TryParseExact(databaseName[Prefix.Length..], "N", out _));
            var target = new SqlConnectionStringBuilder(db.Database.GetConnectionString());
            Assert.Equal("localhost", target.DataSource, ignoreCase: true); Assert.True(target.IntegratedSecurity);
            Assert.Empty(target.AttachDBFilename); Assert.Equal(databaseName, target.InitialCatalog);
        }
        public async ValueTask DisposeAsync()
        {
            try { if (databaseName != null) { AssertOwnedTarget(); await db.Database.EnsureDeletedAsync(); } }
            finally { await db.DisposeAsync(); if (sqlite != null) await sqlite.DisposeAsync(); }
        }
    }
}
