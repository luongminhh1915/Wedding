using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Wedding.Application.Common.Interfaces;
using Wedding.Application.Features.Reviews;
using Wedding.Application.Features.Reviews.Commands.ApproveReview;
using Wedding.Application.Features.Reviews.Commands.RejectReview;
using Wedding.Application.Features.Reviews.Commands.SubmitReview;
using Wedding.Application.Features.Reviews.DTOs;
using Wedding.Domain.Entities;
using Wedding.Domain.Enums;
using Wedding.Domain.Exceptions;
using Wedding.Infrastructure.Persistence;

namespace Wedding.Tests;

public class ReviewTests
{
    private const string Content = "Dịch vụ rất chu đáo và đúng giờ, chúng tôi đã có trải nghiệm cưới tốt đẹp.";
    [Fact]
    public void FourCriteriaAreStoredWithoutRoundingAwayQuarterStars()
    {
        var r = Review.Create(null, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 4, Content, null, false, 5, 4, 4, 4);
        Assert.Equal(4.25m, r.AverageRating); Assert.Equal(5, r.QualityRating);
        Assert.Throws<DomainException>(() => Review.Create(null, r.ListingId, r.CustomerId, r.VendorId, 5, "Ngắn", null));
        Assert.Throws<DomainException>(() => Review.Create(null, r.ListingId, r.CustomerId, r.VendorId, 5, Content, "[\"https://a/1\",\"https://a/2\",\"https://a/3\",\"https://a/4\",\"https://a/5\",\"https://a/6\"]"));
        Assert.Throws<DomainException>(() => Review.Create(null, r.ListingId, r.CustomerId, r.VendorId, 5, Content, "[\"javascript:alert(1)\"]"));
    }
    [Fact]
    public void VerifiedReviewsCarryTwiceTheWeightAndTopRatedNeedsFifty()
    {
        var verified = Review.Create(null, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 5, Content, null, true, 5, 5, 5, 5); verified.Approve();
        var regular = Review.Create(null, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 2, Content, null, false, 2, 2, 2, 2); regular.Approve();
        var result = ReviewRules.Calculate(new[] { verified, regular }); Assert.Equal(4m, result.Rating); Assert.Equal(2, result.Count);
        var vendor = Vendor.Create(Guid.NewGuid(), "Vendor", "vendor", "Vendor", "0900000000", "HCM", .08m); vendor.UpdateRating(4.8m, 49); Assert.False(vendor.IsTopRated); vendor.UpdateRating(4.8m, 50); Assert.True(vendor.IsTopRated);
    }
    [Fact]
    public async Task DisputedContractCannotBeBypassedByOmittingContractId()
    {
        await using var f = await Fixture.Create(); var contract = await f.AddContract(); contract.MarkDisputed(); await f.Db.SaveChangesAsync();
        var handler = new SubmitReviewCommandHandler(f.Db, new Current(f.Customer.Id));
        await Assert.ThrowsAsync<DomainException>(() => handler.Handle(new(null, f.Listing.Id, 5, Content), default));
        Assert.Empty(await f.Db.Reviews.ToListAsync());
    }
    [Fact]
    public async Task VerifiedContractMustMatchExactListingAndVendor()
    {
        await using var f = await Fixture.Create(); var contract = await f.AddContract();
        contract.CompleteContract(); await f.Db.SaveChangesAsync();
        var other = Listing.Create(f.Vendor.Id, f.Category.Id, "Other", "other", 1, 2, "Other", "HCM"); other.Approve(); f.Db.Listings.Add(other); await f.Db.SaveChangesAsync();
        var handler = new SubmitReviewCommandHandler(f.Db, new Current(f.Customer.Id));
        await Assert.ThrowsAsync<DomainException>(() => handler.Handle(new(contract.Id, other.Id, 5, Content), default));
    }
    [Fact]
    public async Task CompletedContractIsVerifiedAndDuplicateIsRejected()
    {
        await using var f = await Fixture.Create(); var contract = await f.AddContract();
        contract.CompleteContract(); await f.Db.SaveChangesAsync();
        var handler = new SubmitReviewCommandHandler(f.Db, new Current(f.Customer.Id));
        var result = await handler.Handle(new(contract.Id, f.Listing.Id, 4, Content, null, 5, 4, 4, 4), default);
        Assert.True(result.IsVerifiedBuyer); Assert.Equal(4.25m, result.AverageRating);
        await Assert.ThrowsAsync<DomainException>(() => handler.Handle(new(null, f.Listing.Id, 5, Content), default));
    }
    [Fact]
    public async Task ConfirmedAfterThreeDaysCannotBeReviewedBeforeActualCompletion()
    {
        await using var f = await Fixture.Create(); var contract = await f.AddContract();
        var handler = new SubmitReviewCommandHandler(f.Db, new Current(f.Customer.Id));
        await Assert.ThrowsAsync<DomainException>(() => handler.Handle(new(contract.Id, f.Listing.Id, 5, Content), default));
        await Assert.ThrowsAsync<DomainException>(() => handler.Handle(new(null, f.Listing.Id, 5, Content), default));
        Assert.Empty(await f.Db.Reviews.ToListAsync());
    }
    [Fact]
    public async Task RejectingApprovedReviewRefreshesVendorScoreAndCount()
    {
        await using var f = await Fixture.Create();
        var review = Review.Create(null, f.Listing.Id, f.Customer.Id, f.Vendor.Id, 5, Content, null); f.Db.Reviews.Add(review); await f.Db.SaveChangesAsync();
        await new ApproveReviewCommandHandler(f.Db).Handle(new(review.Id), default);
        Assert.Equal(1, f.Vendor.TotalReviews); Assert.Equal(5, f.Vendor.RatingAvg);
        await new RejectReviewCommandHandler(f.Db).Handle(new(review.Id, "Ảnh không thuộc khách, đã kiểm tra chứng cứ"), default);
        Assert.Equal(0, f.Vendor.TotalReviews); Assert.Equal(0, f.Vendor.RatingAvg); Assert.False(f.Vendor.IsTopRated);
    }
    private sealed record Current(Guid Id) : ICurrentUserService { public Guid? UserId => Id; public string? Email => "customer@test.vn"; public string? Role => "Customer"; }
    private sealed class Fixture : IAsyncDisposable
    {
        public SqliteConnection Connection = null!; public ApplicationDbContext Db = null!; public User Customer = null!; public Vendor Vendor = null!; public Listing Listing = null!; public Category Category = null!;
        public static async Task<Fixture> Create()
        {
            var f = new Fixture(); f.Connection = new("Data Source=:memory:"); await f.Connection.OpenAsync(); f.Db = new(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(f.Connection).Options); await f.Db.Database.EnsureCreatedAsync();
            f.Customer = User.Create("Customer", "customer@test.vn", "0900000000", "hash", UserRole.Customer);
            var owner = User.Create("Owner", "vendor@test.vn", "0900000001", "hash", UserRole.VendorOwner); f.Db.Users.AddRange(f.Customer, owner);
            f.Vendor = Vendor.Create(owner.Id, "Vendor", "vendor", "Owner", "0900000001", "HCM", .08m); f.Db.Vendors.Add(f.Vendor);
            f.Category = Category.Create("Photo", "photo", null, null, .08m); f.Db.Categories.Add(f.Category);
            f.Listing = Listing.Create(f.Vendor.Id, f.Category.Id, "Photo", "photo", 1000, 10000, "Photo", "HCM"); f.Listing.Approve(); f.Db.Listings.Add(f.Listing); await f.Db.SaveChangesAsync(); return f;
        }
        public async Task<BookingContract> AddContract()
        {
            var lead = Lead.Create(Customer.Id, Vendor.Id, Listing.Id, "0900000000", DateTime.UtcNow.AddDays(-5), 100, 100000000, null); Db.Leads.Add(lead);
            var contract = BookingContract.Create(lead.Id, null, Vendor.Id, Customer.Id, 100000000, 30000000, null, lead.WeddingDate); contract.ConfirmByCustomer(); Db.BookingContracts.Add(contract); await Db.SaveChangesAsync(); return contract;
        }
        public async ValueTask DisposeAsync() { await Db.DisposeAsync(); await Connection.DisposeAsync(); }
    }
}
