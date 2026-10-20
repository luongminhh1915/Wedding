using Microsoft.Data.SqlClient;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using Wedding.WebApi.Controllers;
using Wedding.Application.Common.Interfaces;
using Wedding.Application.Features.Reviews;
using Wedding.Application.Features.Reviews.Commands.SubmitReview;
using Wedding.Application.Features.Reviews.DTOs;
using Wedding.Application.Features.Reviews.Queries.GetEligibleContracts;
using Wedding.Application.Features.Reviews.Queries.GetListingReviews;
using Wedding.Domain.Entities;
using Wedding.Domain.Enums;
using Wedding.Domain.Exceptions;
using Wedding.Infrastructure.Persistence;

namespace Wedding.Tests;

public class CompletedReviewPolicyTests
{
    private const string Content = "Dịch vụ thực tế chu đáo, đúng giờ và các hạng mục đã được hoàn thành đầy đủ.";

    [Theory]
    [InlineData(ContractStatus.Draft)] [InlineData(ContractStatus.PendingVerification)]
    [InlineData(ContractStatus.Confirmed)] [InlineData(ContractStatus.Cancelled)] [InlineData(ContractStatus.Disputed)]
    public async Task NonCompletedTransactionsNeverUnlockReviewEvenLongAfterWedding(ContractStatus status)
    {
        await using var f = await Fixture.Create(); var contract = await f.AddContract(status);
        Assert.False(ReviewRules.IsEligible(contract, DateTime.UtcNow.AddYears(10)));
        Assert.Empty(await f.Eligible());
        await Assert.ThrowsAsync<DomainException>(() => f.Submit(contract.Id));
        await Assert.ThrowsAsync<DomainException>(() => f.Submit(null));
        Assert.Empty(await f.Db.Reviews.ToListAsync());
    }

    [Theory]
    [InlineData(true)] [InlineData(false)]
    public async Task CompletedOwnExactServiceCreatesOnlyVerifiedPendingReview(bool explicitContract)
    {
        await using var f = await Fixture.Create(); var contract = await f.AddContract(ContractStatus.Completed);
        var eligible = Assert.Single(await f.Eligible()); Assert.Equal(contract.Id, eligible.ContractId);
        Assert.Equal(f.Listing.Id, eligible.ListingId); Assert.Equal(f.Listing.Title, eligible.ListingTitle);
        var result = await f.Submit(explicitContract ? contract.Id : null);
        Assert.True(result.IsVerifiedBuyer); Assert.Equal(contract.Id, result.BookingContractId); Assert.Equal("Pending", result.Status);
        Assert.Equal(f.Customer.Id, result.CustomerId); Assert.Equal(f.Vendor.Id, result.VendorId);
        Assert.Empty(await f.Eligible());
        await Assert.ThrowsAsync<DomainException>(() => f.Submit(contract.Id));
        await Assert.ThrowsAsync<DomainException>(() => f.Submit(null));
        Assert.Single(await f.Db.Reviews.ToListAsync());
    }

    [Theory]
    [InlineData("inactive")] [InlineData("claimed-vendor")]
    [InlineData("changed-db-role")] [InlineData("staff")]
    public async Task ReviewAndEligibilityRequireActualActiveCustomerAndMatchingClaim(string condition)
    {
        await using var f = await Fixture.Create(); var contract = await f.AddContract(ContractStatus.Completed);
        var actor = f.CustomerActor;
        if (condition == "inactive") f.Customer.SetStatus(false);
        if (condition == "claimed-vendor") actor = new(f.Customer.Id, "VendorOwner");
        if (condition == "changed-db-role") f.Db.Entry(f.Customer).Property(user => user.Role).CurrentValue = UserRole.VendorOwner;
        if (condition == "staff") actor = new(f.Moderator.Id, "Moderator");
        await f.Db.SaveChangesAsync(); var auditCount = await f.Db.AuditLogs.CountAsync();
        await Assert.ThrowsAsync<DomainException>(() => f.Submit(contract.Id, actor));
        await Assert.ThrowsAsync<DomainException>(() => f.Eligible(actor));
        Assert.Empty(await f.Db.Reviews.ToListAsync()); Assert.Equal(auditCount, await f.Db.AuditLogs.CountAsync());
    }

    [Fact]
    public async Task ForeignCustomerCannotUseCompletedTransactionOrReceiveItsEligibilityDetails()
    {
        await using var f = await Fixture.Create(); var contract = await f.AddContract(ContractStatus.Completed);
        var foreign = new Current(f.OtherCustomer.Id, "Customer");
        Assert.Empty(await f.Eligible(foreign));
        await Assert.ThrowsAsync<DomainException>(() => f.Submit(contract.Id, foreign));
        Assert.Empty(await f.Db.Reviews.ToListAsync());
    }

    [Theory]
    [InlineData(true)] [InlineData(false)]
    public async Task MissingOrEmptyIdentityCannotReadEligibilityOrSubmit(bool missing)
    {
        await using var f = await Fixture.Create(); var actor = new Current(missing ? null : Guid.Empty, "Customer");
        await Assert.ThrowsAsync<DomainException>(() => f.Eligible(actor));
        await Assert.ThrowsAsync<DomainException>(() => f.Submit(null, actor));
        Assert.Empty(await f.Db.Reviews.ToListAsync());
    }

    [Fact]
    public void ReviewApiAndEligibilityAreCustomerOnlyAndClientCannotSetVerificationFlag()
    {
        foreach (var name in new[] { nameof(ReviewsController.SubmitReview), nameof(ReviewsController.GetEligibleContracts) })
        {
            var method = typeof(ReviewsController).GetMethod(name)!;
            var authorize = Assert.Single(method.GetCustomAttributes(typeof(AuthorizeAttribute), true).Cast<AuthorizeAttribute>());
            Assert.Equal("Customer", authorize.Roles);
        }
        Assert.DoesNotContain(typeof(SubmitReviewCommand).GetProperties(), property => property.Name == "IsVerifiedBuyer");
    }

    [Fact]
    public async Task DisputeForSameCustomerAndServiceBlocksOtherCompletedContractButForeignDisputeDoesNot()
    {
        await using var f = await Fixture.Create(); var completed = await f.AddContract(ContractStatus.Completed);
        await f.AddContract(ContractStatus.Disputed, f.OtherCustomer);
        Assert.Single(await f.Eligible());
        await f.AddContract(ContractStatus.Disputed);
        Assert.Empty(await f.Eligible());
        await Assert.ThrowsAsync<DomainException>(() => f.Submit(completed.Id));
        await Assert.ThrowsAsync<DomainException>(() => f.Submit(null));
    }

    [Theory]
    [InlineData("hidden-listing")] [InlineData("inactive-listing")]
    [InlineData("inactive-vendor")] [InlineData("inactive-owner")]
    public async Task NonPublicServiceIsNeitherDiscoverableAsEligibleNorReviewable(string condition)
    {
        await using var f = await Fixture.Create(); var contract = await f.AddContract(ContractStatus.Completed);
        if (condition == "hidden-listing") f.Listing.SetStatus(ListingStatus.Hidden);
        if (condition == "inactive-listing") f.Listing.SetActivity(false);
        if (condition == "inactive-vendor") f.Db.Entry(f.Vendor).Property(vendor => vendor.IsActive).CurrentValue = false;
        if (condition == "inactive-owner") f.Owner.SetStatus(false);
        await f.Db.SaveChangesAsync(); f.Db.ChangeTracker.Clear();
        Assert.Empty(await f.Eligible());
        await Assert.ThrowsAsync<NotFoundException>(() => f.Submit(contract.Id));
        Assert.Empty(await f.Db.Reviews.ToListAsync());
    }

    [Fact]
    public async Task HistoricalConsultationReviewIsNotDeletedUpgradedReweightedOrDuplicated()
    {
        await using var f = await Fixture.Create();
        var historical = Review.Create(null, f.Listing.Id, f.Customer.Id, f.Vendor.Id, 2, Content, null, false); historical.Approve();
        var completed = await f.AddContract(ContractStatus.Completed, f.OtherCustomer);
        var verified = Review.Create(completed.Id, f.Listing.Id, f.OtherCustomer.Id, f.Vendor.Id, 5, Content, null, true); verified.Approve();
        f.Db.Reviews.AddRange(historical, verified); await f.Db.SaveChangesAsync();
        await ReviewRules.RefreshVendorAsync(f.Db, f.Vendor.Id, default); await f.Db.SaveChangesAsync();
        var previousVersion = historical.UpdatedAt; var previousTime = historical.CreatedAt;
        Assert.Equal(4m, f.Vendor.RatingAvg); Assert.Equal(2, f.Vendor.TotalReviews);
        await f.AddContract(ContractStatus.Completed);
        Assert.Empty(await f.Eligible());
        await Assert.ThrowsAsync<DomainException>(() => f.Submit(null));
        f.Db.ChangeTracker.Clear();
        var old = await f.Db.Reviews.AsNoTracking().SingleAsync(review => review.Id == historical.Id);
        Assert.False(old.IsVerifiedBuyer); Assert.Null(old.BookingContractId); Assert.Equal(ReviewStatus.Approved, old.Status);
        Assert.Equal(Content, old.Content); Assert.Equal(previousTime, old.CreatedAt); Assert.Equal(previousVersion, old.UpdatedAt);
        var vendor = await f.Db.Vendors.AsNoTracking().SingleAsync(); Assert.Equal(4m, vendor.RatingAvg); Assert.Equal(2, vendor.TotalReviews);
        var reviews = await new GetListingReviewsQueryHandler(f.Db).Handle(new(ListingId: f.Listing.Id), default);
        Assert.Equal(2, reviews.Count); Assert.Contains(reviews, review => review.Id == historical.Id && !review.IsVerifiedBuyer);
    }

    [SqlServerFact]
    public async Task SqlEligibleQueryAndWriteRequireCompletionAndPreserveStoredHistoricalFlags()
    {
        await using var f = await Fixture.Create(sql: true);
        await f.AddContract(ContractStatus.Confirmed);
        Assert.Empty(await f.Eligible()); await Assert.ThrowsAsync<DomainException>(() => f.Submit(null));
        var completed = await f.AddContract(ContractStatus.Completed);
        var eligible = Assert.Single(await f.Eligible()); Assert.Equal(completed.Id, eligible.ContractId);
        var submitted = await f.Submit(completed.Id); Assert.True(submitted.IsVerifiedBuyer);
        f.Db.ChangeTracker.Clear();
        var stored = await f.Db.Reviews.AsNoTracking().SingleAsync(); Assert.True(stored.IsVerifiedBuyer);
        Assert.Equal(completed.Id, stored.BookingContractId); Assert.Equal(ReviewStatus.Pending, stored.Status);
        Assert.Empty(await f.Eligible()); await Assert.ThrowsAsync<DomainException>(() => f.Submit(null));
        Assert.Empty(await f.Db.Database.GetPendingMigrationsAsync());
    }

    private sealed record Current(Guid? UserId, string? Role) : ICurrentUserService { public string? Email => null; }
    private sealed class Fixture(ApplicationDbContext db, SqliteConnection? connection, string? databaseName) : IAsyncDisposable
    {
        public ApplicationDbContext Db { get; } = db;
        public User Customer { get; private set; } = null!;
        public User OtherCustomer { get; private set; } = null!;
        public User Owner { get; private set; } = null!;
        public User Moderator { get; private set; } = null!;
        public Vendor Vendor { get; private set; } = null!;
        public Listing Listing { get; private set; } = null!;
        public Current CustomerActor => new(Customer.Id, "Customer");
        public Task<List<EligibleContractDto>> Eligible(Current? actor = null) => new GetEligibleContractsQueryHandler(Db, actor ?? CustomerActor).Handle(new(), default);
        public Task<ReviewDto> Submit(Guid? contract, Current? actor = null) => new SubmitReviewCommandHandler(Db, actor ?? CustomerActor).Handle(new(contract, Listing.Id, 5, Content), default);

        public static async Task<Fixture> Create(bool sql = false)
        {
            SqliteConnection? connection = null; string? database = null;
            var options = new DbContextOptionsBuilder<ApplicationDbContext>();
            if (sql)
            {
                database = $"WeddingPlanTest_CompletedReview_{Guid.NewGuid():N}";
                var source = Environment.GetEnvironmentVariable("WEDDING_TEST_SQLSERVER");
                var builder = new SqlConnectionStringBuilder(string.IsNullOrWhiteSpace(source) || source == "1"
                    ? "Server=localhost;Integrated Security=true;TrustServerCertificate=true;Connection Timeout=5" : source) { InitialCatalog = database };
                options.UseSqlServer(builder.ConnectionString);
            }
            else { connection = new SqliteConnection("Data Source=:memory:"); await connection.OpenAsync(); options.UseSqlite(connection); }
            var f = new Fixture(new ApplicationDbContext(options.Options), connection, database);
            try
            {
                if (sql) await f.Db.Database.MigrateAsync(); else await f.Db.Database.EnsureCreatedAsync();
                f.Customer = User.Create("Synthetic used-service customer", "customer@completed-review.example.invalid", "0901234567", "hash", UserRole.Customer);
                f.OtherCustomer = User.Create("Other synthetic customer", "other@completed-review.example.invalid", "0901234568", "hash", UserRole.Customer);
                f.Owner = User.Create("Synthetic shop owner", "owner@completed-review.example.invalid", "0901234569", "hash", UserRole.VendorOwner);
                f.Moderator = User.Create("Synthetic moderator", "mod@completed-review.example.invalid", "0901234570", "hash", UserRole.Moderator);
                f.Db.Users.AddRange(f.Customer, f.OtherCustomer, f.Owner, f.Moderator);
                f.Vendor = Vendor.Create(f.Owner.Id, "Synthetic review shop", "completed-review-shop", "Owner", "0901234569", "Hà Nội", .08m);
                f.Db.Vendors.Add(f.Vendor);
                var category = await f.Db.Categories.SingleAsync(item => item.Slug == "trang-tri-decor");
                f.Listing = Listing.Create(f.Vendor.Id, category.Id, "Synthetic completed service", "completed-review-listing", 1000m, 100000m, "Synthetic description", "Hà Nội");
                f.Listing.Approve(); f.Db.Listings.Add(f.Listing); await f.Db.SaveChangesAsync(); return f;
            }
            catch { await f.DisposeAsync(); throw; }
        }

        public async Task<BookingContract> AddContract(ContractStatus status, User? customer = null)
        {
            customer ??= Customer;
            var lead = Lead.Create(customer.Id, Vendor.Id, Listing.Id, customer.PhoneNumber, DateTime.UtcNow.AddDays(-10), 100, 100000000m, null);
            Db.Leads.Add(lead);
            var contract = BookingContract.Create(lead.Id, null, Vendor.Id, customer.Id, 100000000m, 30000000m, null, lead.WeddingDate);
            contract.ConfirmByCustomer(); contract.SnapshotCommissionRate(.08m);
            if (status == ContractStatus.Completed) contract.CompleteContract();
            else if (status == ContractStatus.Disputed) contract.MarkDisputed();
            Db.BookingContracts.Add(contract);
            Db.Entry(contract).Property(item => item.ContractCode).CurrentValue = $"HD-REVIEW-{Guid.NewGuid():N}";
            Db.Entry(contract).Property(item => item.Status).CurrentValue = status;
            await Db.SaveChangesAsync(); return contract;
        }

        public async ValueTask DisposeAsync()
        {
            if (databaseName != null) { Assert.StartsWith("WeddingPlanTest_CompletedReview_", databaseName); await Db.Database.EnsureDeletedAsync(); }
            await Db.DisposeAsync(); if (connection != null) await connection.DisposeAsync();
        }
    }
}
