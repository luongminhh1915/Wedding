using System.Text.Json;
using Microsoft.Data.SqlClient;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Wedding.Application.Common.Interfaces;
using Wedding.Application.Features.Reviews;
using Wedding.Application.Features.Reviews.DTOs;
using Wedding.Domain.Entities;
using Wedding.Domain.Enums;
using Wedding.Domain.Exceptions;
using Wedding.Infrastructure.Persistence;

namespace Wedding.Tests;

public class IndependentReviewRepairTests
{
    private const string OriginalContent = "Synthetic completed service review with photos and all four original criteria.";
    private const string UpdatedContent = "Synthetic updated experience with a changed comment and overall rating only.";
    private const string OriginalPhotos = "[\"https://review.example.invalid/original.jpg\"]";
    private const string ReplacementPhotos = "[\"https://review.example.invalid/replacement.jpg\"]";
    private static readonly JsonSerializerOptions WebJson = new(JsonSerializerDefaults.Web);

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, false)]
    [InlineData(true, true)]
    [InlineData(false, true)]
    public async Task TextAndRatingEditsPreserveOmittedOrNullOptionalFields(bool approve, bool explicitNull)
    {
        await using var fixture = await Fixture.Create();
        await VerifyPreservedOptionals(fixture, approve, explicitNull);
    }

    private static async Task VerifyPreservedOptionals(Fixture fixture, bool approve, bool explicitNull)
    {
        var review = await fixture.AddApprovedReview();
        var payload = new Dictionary<string, object?>
        {
            ["reviewId"] = review.Id, ["rating"] = 2, ["content"] = UpdatedContent
        };
        if (explicitNull)
            foreach (var name in new[] { "photosJson", "qualityRating", "serviceRating", "punctualityRating", "valueRating" })
                payload[name] = null;
        var command = JsonSerializer.Deserialize<SubmitReviewEditRequestCommand>(
            JsonSerializer.Serialize(payload, WebJson), WebJson)!;
        var pending = await fixture.Edit(command);
        Assert.Equal(1, pending.EditAttemptCount);
        Assert.Equal(OriginalContent, pending.Content);
        Assert.Equal(5, pending.Rating);
        AssertOriginalOptionals(pending);
        var before = await fixture.PublicServiceReviews();
        Assert.Equal(OriginalPhotos, before.Reviews.Single(item => item.Id == review.Id).PhotosJson);
        Assert.Equal(4m, before.Summary.AverageRating);

        var history = await fixture.Db.ReviewEditRequests.AsNoTracking().SingleAsync(item => item.ReviewId == review.Id);
        Assert.Equal(OriginalPhotos, history.BeforePhotosJson);
        Assert.Equal(OriginalPhotos, history.AfterPhotosJson);
        Assert.Equal(fixture.Customer.Id, history.RequestedById);
        Assert.Equal(new int?[] { 5, 4, 3, 4 }, new[]
        {
            history.AfterQualityRating, history.AfterServiceRating,
            history.AfterPunctualityRating, history.AfterValueRating
        });
        var comparison = Assert.Single(await fixture.PendingEdits());
        Assert.Equal(OriginalPhotos, comparison.BeforePhotosJson);
        Assert.Equal(OriginalPhotos, comparison.AfterPhotosJson);
        Assert.Equal(5, comparison.BeforeQualityRating);
        Assert.Equal(5, comparison.AfterQualityRating);

        var result = await fixture.Moderate(history.Id, approve);
        AssertOriginalOptionals(result);
        Assert.Equal(approve ? UpdatedContent : OriginalContent, result.Content);
        Assert.Equal(approve ? 2 : 5, result.Rating);
        var after = await fixture.PublicServiceReviews();
        Assert.Equal(OriginalPhotos, after.Reviews.Single(item => item.Id == review.Id).PhotosJson);
        // Keeping criteria does not silently replace their established scoring with the headline rating.
        Assert.Equal(before.Summary.TotalReviews, after.Summary.TotalReviews);
        Assert.Equal(4m, after.Summary.AverageRating);
        history = await fixture.Db.ReviewEditRequests.AsNoTracking().SingleAsync(item => item.ReviewId == review.Id);
        Assert.Equal(approve ? ReviewEditStatus.Approved : ReviewEditStatus.Rejected, history.Status);
        Assert.Equal(fixture.Moderator.Id, history.ReviewedById);
        Assert.NotNull(history.ReviewedAt);
        Assert.Equal(OriginalPhotos, history.BeforePhotosJson);
        Assert.Equal(OriginalPhotos, history.AfterPhotosJson);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, false)]
    [InlineData(true, true)]
    [InlineData(false, true)]
    public async Task ExplicitPhotoAndCriterionChangesRemainPrivateUntilApproved(bool approve, bool clearPhotos)
    {
        await using var fixture = await Fixture.Create();
        await VerifyExplicitChanges(fixture, approve, clearPhotos);
    }

    private static async Task VerifyExplicitChanges(Fixture fixture, bool approve, bool clearPhotos)
    {
        var review = await fixture.AddApprovedReview();
        // An explicit valid empty JSON array is the existing way to clear photo references.
        var proposedPhotos = clearPhotos ? "[]" : ReplacementPhotos;
        await fixture.Edit(new(review.Id, 3, UpdatedContent, proposedPhotos, 2, 3, 4, 5));
        var comparison = Assert.Single(await fixture.PendingEdits());
        Assert.Equal(OriginalPhotos, comparison.BeforePhotosJson);
        Assert.Equal(proposedPhotos, comparison.AfterPhotosJson);
        Assert.Equal(new int?[] { 5, 4, 3, 4 }, new[]
        {
            comparison.BeforeQualityRating, comparison.BeforeServiceRating,
            comparison.BeforePunctualityRating, comparison.BeforeValueRating
        });
        Assert.Equal(new int?[] { 2, 3, 4, 5 }, new[]
        {
            comparison.AfterQualityRating, comparison.AfterServiceRating,
            comparison.AfterPunctualityRating, comparison.AfterValueRating
        });
        using (var json = JsonDocument.Parse(JsonSerializer.Serialize(comparison, WebJson)))
        {
            Assert.Equal(proposedPhotos, json.RootElement.GetProperty("afterPhotosJson").GetString());
            Assert.Equal(2, json.RootElement.GetProperty("afterQualityRating").GetInt32());
            Assert.Equal(3, json.RootElement.GetProperty("afterServiceRating").GetInt32());
            Assert.Equal(4, json.RootElement.GetProperty("afterPunctualityRating").GetInt32());
            Assert.Equal(5, json.RootElement.GetProperty("afterValueRating").GetInt32());
        }
        Assert.Equal(OriginalPhotos, (await fixture.PublicServiceReviews()).Reviews.Single(item => item.Id == review.Id).PhotosJson);
        var result = await fixture.Moderate(comparison.Id, approve);
        Assert.Equal(approve ? proposedPhotos : OriginalPhotos, result.PhotosJson);
        Assert.Equal(approve ? new int?[] { 2, 3, 4, 5 } : new int?[] { 5, 4, 3, 4 }, new[]
        {
            result.QualityRating, result.ServiceRating, result.PunctualityRating, result.ValueRating
        });
        var history = await fixture.Db.ReviewEditRequests.AsNoTracking().SingleAsync(item => item.ReviewId == review.Id);
        Assert.Equal(OriginalPhotos, history.BeforePhotosJson);
        Assert.Equal(proposedPhotos, history.AfterPhotosJson);
    }

    [Fact]
    public async Task PartialCriterionChangeKeepsTheOtherExistingScores()
    {
        await using var fixture = await Fixture.Create();
        var review = await fixture.AddApprovedReview();
        await fixture.Edit(new(review.Id, 3, UpdatedContent, QualityRating: 1));
        var comparison = Assert.Single(await fixture.PendingEdits());
        Assert.Equal(new int?[] { 1, 4, 3, 4 }, new[]
        {
            comparison.AfterQualityRating, comparison.AfterServiceRating,
            comparison.AfterPunctualityRating, comparison.AfterValueRating
        });
        var result = await fixture.Moderate(comparison.Id, true);
        Assert.Equal(OriginalPhotos, result.PhotosJson);
        Assert.Equal(3m, result.AverageRating);
    }

    [Fact]
    public async Task PartialNewCriteriaStillFailExistingCompleteSetValidation()
    {
        await using var fixture = await Fixture.Create();
        var review = await fixture.AddApprovedReview(withCriteria: false);
        await Assert.ThrowsAsync<DomainException>(() => fixture.Edit(new(review.Id, 3, UpdatedContent, QualityRating: 1)));
        Assert.Empty(await fixture.Db.ReviewEditRequests.ToListAsync());
        var stored = await fixture.Db.Reviews.AsNoTracking().SingleAsync();
        Assert.Null(stored.QualityRating);
        Assert.Equal(OriginalPhotos, stored.PhotosJson);
    }

    [Fact]
    public async Task ResponseAttemptCountMatchesPersistedHistoryAcrossAllThreeSubmissions()
    {
        await using var fixture = await Fixture.Create();
        var review = await fixture.AddApprovedReview();
        for (var attempt = 1; attempt <= 3; attempt++)
        {
            var pending = await fixture.Edit(new(review.Id, 2, UpdatedContent));
            Assert.Equal(attempt, pending.EditAttemptCount);
            Assert.Equal(attempt, await fixture.Db.ReviewEditRequests.CountAsync());
            var comparison = Assert.Single(await fixture.PendingEdits());
            Assert.Equal(attempt, comparison.AttemptNumber);
            await fixture.Moderate(comparison.Id, false);
        }
        await Assert.ThrowsAsync<DomainException>(() => fixture.Edit(new(review.Id, 2, UpdatedContent)));
        Assert.Equal(3, await fixture.Db.ReviewEditRequests.CountAsync());
    }

    [Fact]
    public async Task CustomComboWithTwoCompletedComponentsOffersServicesAndOneShopButNoCombo()
    {
        await using var fixture = await Fixture.Create();
        await VerifyCustomCombo(fixture, secondCompleted: true);
    }

    [Fact]
    public async Task IncompleteCustomComboKeepsCompletedServiceButDefersShopUntilEveryComponentCompletes()
    {
        await using var fixture = await Fixture.Create();
        await VerifyCustomCombo(fixture, secondCompleted: false);
    }

    private static async Task VerifyCustomCombo(Fixture fixture, bool secondCompleted)
    {
        var (order, contracts) = await fixture.AddCustomCombo(secondCompleted);
        var targets = (await fixture.EligibleTargets()).Where(target => target.ComboOrderId == order.Id).ToList();
        Assert.DoesNotContain(targets, target => target.TargetType == "Combo");
        if (!secondCompleted)
        {
            var service = Assert.Single(targets);
            Assert.Equal("Service", service.TargetType);
            Assert.Equal(contracts[0].Id, service.BookingContractId);
            await Assert.ThrowsAsync<DomainException>(() => fixture.SubmitShop(order.Id));
            await Assert.ThrowsAsync<DomainException>(() => new SubmitComboReviewCommandHandler(fixture.Db, fixture.CustomerActor)
                .Handle(new(order.Id, 4, OriginalContent), default));
            var unfinished = await fixture.Db.BookingContracts.SingleAsync(contract => contract.Id == contracts[1].Id);
            unfinished.CompleteContract();
            await fixture.Db.SaveChangesAsync();
            targets = (await fixture.EligibleTargets()).Where(target => target.ComboOrderId == order.Id).ToList();
        }
        Assert.Equal(2, targets.Count(target => target.TargetType == "Service"));
        var shop = Assert.Single(targets, target => target.TargetType == "Shop");
        Assert.Equal(order.Id, shop.TransactionId);
        Assert.Equal(order.Id, shop.ComboOrderId);
        Assert.Equal(fixture.Vendor.Id, shop.TargetId);
        Assert.Null(shop.BookingContractId);

        // Neither component is a second independent Shop transaction.
        foreach (var contract in contracts)
            await Assert.ThrowsAsync<DomainException>(() => new SubmitShopReviewCommandHandler(fixture.Db, fixture.CustomerActor)
                .Handle(new(contract.Id, null, 4, OriginalContent), default));
        var review = await fixture.SubmitShop(order.Id);
        Assert.Equal(order.Id, review.ComboOrderId);
        Assert.Null(review.BookingContractId);
        Assert.DoesNotContain(await fixture.EligibleTargets(), target => target.ComboOrderId == order.Id && target.TargetType == "Shop");
        Assert.Equal(2, (await fixture.EligibleTargets()).Count(target => target.ComboOrderId == order.Id && target.TargetType == "Service"));
        await Assert.ThrowsAsync<DomainException>(() => fixture.SubmitShop(order.Id));
        await Assert.ThrowsAsync<DomainException>(() => new SubmitComboReviewCommandHandler(fixture.Db, fixture.CustomerActor)
            .Handle(new(order.Id, 4, OriginalContent), default));
        Assert.Single(await fixture.Db.Reviews.AsNoTracking().Where(review => review.ComboOrderId == order.Id).ToListAsync());
    }

    [SqlServerFact]
    public async Task SqlRepairsPreserveOptionalEvidenceExposeProposalsAndDiscoverCompletedCustomCombo()
    {
        // Real SQL opt-in is additive coverage, never a replacement for a failed SQL test.
        await using var fixture = await Fixture.Create(sql: true);
        await VerifyPreservedOptionals(fixture, approve: false, explicitNull: true);
        await VerifyExplicitChanges(fixture, approve: true, clearPhotos: true);
        await VerifyCustomCombo(fixture, secondCompleted: false);
    }

    private static void AssertOriginalOptionals(ReviewDto review)
    {
        Assert.Equal(OriginalPhotos, review.PhotosJson);
        Assert.Equal(new int?[] { 5, 4, 3, 4 }, new[]
        {
            review.QualityRating, review.ServiceRating, review.PunctualityRating, review.ValueRating
        });
    }

    private sealed record Current(Guid? UserId, string? Role) : ICurrentUserService
    {
        public string? Email => null;
    }

    private sealed class Fixture(ApplicationDbContext db, SqliteConnection? sqlite, string? databaseName) : IAsyncDisposable
    {
        private const string DatabasePrefix = "WeddingPlanTest_ReviewRepair_";
        public ApplicationDbContext Db { get; } = db;
        public User Customer { get; private set; } = null!;
        public User Moderator { get; private set; } = null!;
        public Vendor Vendor { get; private set; } = null!;
        public Listing Listing { get; private set; } = null!;
        public Listing SecondListing { get; private set; } = null!;
        public Current CustomerActor => new(Customer.Id, "Customer");

        public Task<ReviewDto> Edit(SubmitReviewEditRequestCommand command) =>
            new SubmitReviewEditRequestCommandHandler(Db, CustomerActor).Handle(command, default);
        public Task<ReviewDto> Moderate(Guid id, bool approve) =>
            new ModerateReviewEditRequestCommandHandler(Db, new Current(Moderator.Id, "Moderator"))
                .Handle(new(id, approve, approve ? null : "Synthetic moderator rejection retains the approved version."), default);
        public Task<List<PendingReviewEditDto>> PendingEdits() =>
            new GetPendingReviewEditsQueryHandler(Db, new Current(Moderator.Id, "Moderator")).Handle(new(), default);
        public Task<ReviewPageDto> PublicServiceReviews() =>
            new GetIndependentReviewsQueryHandler(Db).Handle(new("Service", Listing.Id), default);
        public Task<List<EligibleReviewTargetDto>> EligibleTargets() =>
            new GetEligibleReviewTargetsQueryHandler(Db, CustomerActor).Handle(new(), default);
        public Task<ReviewDto> SubmitShop(Guid orderId) =>
            new SubmitShopReviewCommandHandler(Db, CustomerActor).Handle(new(null, orderId, 4, OriginalContent), default);

        public static async Task<Fixture> Create(bool sql = false)
        {
            SqliteConnection? sqlite = null;
            string? name = null;
            var options = new DbContextOptionsBuilder<ApplicationDbContext>();
            if (sql)
            {
                // Opt-in is a switch only: never use an environment-provided server/database.
                Assert.False(string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("WEDDING_TEST_SQLSERVER")));
                name = DatabasePrefix + Guid.NewGuid().ToString("N");
                var target = new SqlConnectionStringBuilder
                {
                    DataSource = "localhost", InitialCatalog = name, IntegratedSecurity = true,
                    TrustServerCertificate = true, ConnectTimeout = 5
                };
                var master = new SqlConnectionStringBuilder(target.ConnectionString) { InitialCatalog = "master" };
                await using var connection = new SqlConnection(master.ConnectionString);
                await connection.OpenAsync();
                await using (var exists = connection.CreateCommand())
                {
                    exists.CommandText = "SELECT DB_ID(@database)";
                    exists.Parameters.AddWithValue("@database", name);
                    Assert.Equal(DBNull.Value, await exists.ExecuteScalarAsync());
                }
                await using (var create = connection.CreateCommand())
                {
                    create.CommandText = $"CREATE DATABASE [{name}]";
                    await create.ExecuteNonQueryAsync();
                }
                options.UseSqlServer(target.ConnectionString);
            }
            else
            {
                sqlite = new("Data Source=:memory:");
                await sqlite.OpenAsync();
                options.UseSqlite(sqlite);
            }
            var fixture = new Fixture(new ApplicationDbContext(options.Options), sqlite, name);
            try
            {
                if (sql) { fixture.AssertOwnedTarget(); await fixture.Db.Database.MigrateAsync(); }
                else await fixture.Db.Database.EnsureCreatedAsync();
                var owner = User.Create("Synthetic review repair owner", $"{Guid.NewGuid():N}@review-repair.example.invalid", "0900000001", "synthetic-no-login", UserRole.VendorOwner);
                fixture.Customer = User.Create("Synthetic review repair customer", $"{Guid.NewGuid():N}@review-repair.example.invalid", "0900000002", "synthetic-no-login", UserRole.Customer);
                fixture.Moderator = User.Create("Synthetic review repair moderator", $"{Guid.NewGuid():N}@review-repair.example.invalid", "0900000003", "synthetic-no-login", UserRole.Moderator);
                fixture.Db.Users.AddRange(owner, fixture.Customer, fixture.Moderator);
                fixture.Vendor = Vendor.Create(owner.Id, "Synthetic review repair shop", $"review-repair-{Guid.NewGuid():N}", "Synthetic owner", "0900000001", "HCM", .08m);
                fixture.Db.Vendors.Add(fixture.Vendor);
                var firstCategory = Category.Create("Synthetic first review category", $"repair-{Guid.NewGuid():N}", null, null, .08m);
                var secondCategory = Category.Create("Synthetic second review category", $"repair-{Guid.NewGuid():N}", null, null, .08m);
                fixture.Db.Categories.AddRange(firstCategory, secondCategory);
                fixture.Listing = Listing.Create(fixture.Vendor.Id, firstCategory.Id, "Synthetic first service", $"repair-{Guid.NewGuid():N}", 1_000_000, 1_000_000, "Synthetic first completed service", "HCM");
                fixture.SecondListing = Listing.Create(fixture.Vendor.Id, secondCategory.Id, "Synthetic second service", $"repair-{Guid.NewGuid():N}", 1_000_000, 1_000_000, "Synthetic second completed service", "HCM");
                fixture.Listing.Approve(); fixture.SecondListing.Approve();
                fixture.Db.Listings.AddRange(fixture.Listing, fixture.SecondListing);
                await fixture.Db.SaveChangesAsync();
                return fixture;
            }
            catch { await fixture.DisposeAsync(); throw; }
        }

        public async Task<Review> AddApprovedReview(bool withCriteria = true)
        {
            var contract = AddContract(Listing, order: null, complete: true);
            var review = Review.Create(contract.Id, Listing.Id, Customer.Id, Vendor.Id, 5,
                OriginalContent, OriginalPhotos, true, withCriteria ? 5 : null,
                withCriteria ? 4 : null, withCriteria ? 3 : null, withCriteria ? 4 : null);
            review.Approve(); Db.Reviews.Add(review);
            await Db.SaveChangesAsync();
            return review;
        }

        public async Task<(ComboOrder Order, List<BookingContract> Contracts)> AddCustomCombo(bool secondCompleted)
        {
            var order = ComboOrder.Create(Customer.Id, null, DateTime.UtcNow.AddDays(-10), 100, null,
                [(Listing.Id, Vendor.Id, Listing.CategoryId, Listing.Title, 1_000_000m),
                 (SecondListing.Id, Vendor.Id, SecondListing.CategoryId, SecondListing.Title, 1_000_000m)]);
            order.SetPricing(0, "HCM", false);
            Db.ComboOrders.Add(order);
            var contracts = new List<BookingContract>
            {
                AddContract(Listing, order, complete: true), AddContract(SecondListing, order, secondCompleted)
            };
            await Db.SaveChangesAsync();
            return (order, contracts);
        }

        private BookingContract AddContract(Listing listing, ComboOrder? order, bool complete)
        {
            var lead = Lead.Create(Customer.Id, Vendor.Id, listing.Id, Customer.PhoneNumber,
                DateTime.UtcNow.AddDays(-10), 100, 1_000_000m, null);
            if (order != null)
            {
                lead.AttachSource("Combo", order.Id);
                order.Items.Single(item => item.ListingId == listing.Id).AttachLead(lead.Id);
            }
            Db.Leads.Add(lead);
            var contract = BookingContract.Create(lead.Id, null, Vendor.Id, Customer.Id,
                1_000_000m, 200_000m, null, lead.WeddingDate);
            contract.ConfirmByCustomer();
            if (complete) contract.CompleteContract();
            Db.Entry(contract).Property(item => item.ContractCode).CurrentValue = $"HD-REPAIR-{Guid.NewGuid():N}";
            Db.BookingContracts.Add(contract);
            return contract;
        }

        private void AssertOwnedTarget()
        {
            Assert.NotNull(databaseName);
            Assert.StartsWith(DatabasePrefix, databaseName);
            Assert.True(Guid.TryParseExact(databaseName[DatabasePrefix.Length..], "N", out _));
            var target = new SqlConnectionStringBuilder(Db.Database.GetConnectionString());
            Assert.Equal("localhost", target.DataSource, ignoreCase: true);
            Assert.True(target.IntegratedSecurity);
            Assert.Empty(target.AttachDBFilename);
            Assert.Equal(databaseName, target.InitialCatalog);
        }

        public async ValueTask DisposeAsync()
        {
            try
            {
                if (databaseName != null) { AssertOwnedTarget(); await Db.Database.EnsureDeletedAsync(); }
            }
            finally
            {
                await Db.DisposeAsync();
                if (sqlite != null) await sqlite.DisposeAsync();
            }
        }
    }
}
