using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using Wedding.Application.Common.Interfaces;
using Wedding.Application.Features.Reviews.Commands.ApproveReview;
using Wedding.Application.Features.Reviews.Commands.RejectReview;
using Wedding.Application.Features.Reviews.Commands.SubmitReview;
using Wedding.Application.Features.Reviews.DTOs;
using Wedding.Application.Features.Reviews;
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
        Assert.Throws<DomainException>(() => Review.Create(null, r.ListingId!.Value, r.CustomerId, r.VendorId, 5, "Ngắn", null));
        Assert.Throws<DomainException>(() => Review.Create(null, r.ListingId!.Value, r.CustomerId, r.VendorId, 5, Content, "[\"https://a/1\",\"https://a/2\",\"https://a/3\",\"https://a/4\",\"https://a/5\",\"https://a/6\"]"));
        Assert.Throws<DomainException>(() => Review.Create(null, r.ListingId!.Value, r.CustomerId, r.VendorId, 5, Content, "[\"javascript:alert(1)\"]"));
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
    public async Task ServiceReviewsDoNotChangeDirectShopRating()
    {
        await using var f = await Fixture.Create();
        var review = Review.Create(null, f.Listing.Id, f.Customer.Id, f.Vendor.Id, 5, Content, null); f.Db.Reviews.Add(review); await f.Db.SaveChangesAsync();
        await new ApproveReviewCommandHandler(f.Db).Handle(new(review.Id), default);
        Assert.Equal(0, f.Vendor.TotalReviews); Assert.Equal(0, f.Vendor.RatingAvg);
        await new RejectReviewCommandHandler(f.Db).Handle(new(review.Id, "Ảnh không thuộc khách, đã kiểm tra chứng cứ"), default);
        Assert.Equal(0, f.Vendor.TotalReviews); Assert.Equal(0, f.Vendor.RatingAvg); Assert.False(f.Vendor.IsTopRated);
    }
    [Fact]
    public async Task ApprovedShopReviewAloneUpdatesVendorRating()
    {
        await using var f = await Fixture.Create();
        var contract = await f.AddContract(); contract.CompleteContract(); await f.Db.SaveChangesAsync();
        var review = Review.CreateShop(contract.Id, null, f.Customer.Id, f.Vendor.Id, 4, Content, null);
        f.Db.Reviews.Add(review); await f.Db.SaveChangesAsync();
        await new ApproveReviewCommandHandler(f.Db).Handle(new(review.Id), default);
        Assert.Equal(1, f.Vendor.TotalReviews); Assert.Equal(4, f.Vendor.RatingAvg);
        await new RejectReviewCommandHandler(f.Db).Handle(new(review.Id, "Không hợp lệ"), default);
        Assert.Equal(0, f.Vendor.TotalReviews); Assert.Equal(0, f.Vendor.RatingAvg);
    }
    [Fact]
    public async Task ShopAndServiceReviewsCanShareOneCompletedContractButCannotDuplicateTarget()
    {
        await using var f = await Fixture.Create(); var contract = await f.AddContract();
        contract.CompleteContract(); await f.Db.SaveChangesAsync();
        await new SubmitReviewCommandHandler(f.Db, new Current(f.Customer.Id))
            .Handle(new(contract.Id, f.Listing.Id, 5, Content), default);
        var shopHandler = new SubmitShopReviewCommandHandler(f.Db, new Current(f.Customer.Id));
        var shop = await shopHandler.Handle(new(contract.Id, null, 4, Content), default);
        Assert.Equal("Shop", shop.TargetType);
        Assert.Equal(2, await f.Db.Reviews.CountAsync());
        await Assert.ThrowsAsync<DomainException>(() => shopHandler.Handle(new(contract.Id, null, 3, Content), default));
        await Assert.ThrowsAsync<DomainException>(() => new SubmitShopReviewCommandHandler(f.Db, new Current(f.Owner.Id))
            .Handle(new(contract.Id, null, 3, Content), default));
    }
    [Fact]
    public async Task ApprovedReviewEditStaysPrivateUntilApprovalAndCanOnlyBeApprovedOnce()
    {
        await using var f = await Fixture.Create();
        var review = Review.Create(null, f.Listing.Id, f.Customer.Id, f.Vendor.Id, 5, Content, null);
        review.Approve(); f.Db.Reviews.Add(review); await f.Db.SaveChangesAsync();
        var editHandler = new SubmitReviewEditRequestCommandHandler(f.Db, new Current(f.Customer.Id));
        var pending = await editHandler.Handle(new(review.Id, 3, "Bản cập nhật mô tả trải nghiệm sau khi hoàn tất dịch vụ."), default);
        Assert.Equal(Content, pending.Content); Assert.Equal(5, pending.Rating); Assert.Equal("Pending", pending.EditStatus);
        var moderator = new Current(f.Moderator.Id, "Moderator");
        var moderation = new ModerateReviewEditRequestCommandHandler(f.Db, moderator);
        var approved = await moderation.Handle(new((await f.Db.ReviewEditRequests.SingleAsync()).Id, true), default);
        Assert.Equal("Bản cập nhật mô tả trải nghiệm sau khi hoàn tất dịch vụ.", approved.Content);
        Assert.Equal(3, approved.Rating); Assert.False(approved.CanEdit);
        await Assert.ThrowsAsync<DomainException>(() => editHandler.Handle(
            new(review.Id, 2, "Không được yêu cầu sửa thêm sau khi đã duyệt một lần."), default));
        var history = await f.Db.ReviewEditRequests.AsNoTracking().SingleAsync();
        Assert.Equal(Content, history.BeforeContent); Assert.Equal(5, history.BeforeRating);
        Assert.Equal(ReviewEditStatus.Approved, history.Status);
        Assert.Equal(f.Moderator.Id, history.ReviewedById);
    }
    [Fact]
    public async Task RejectedReviewEditLeavesApprovedVersionAndAllowsOnlyThreeAttempts()
    {
        await using var f = await Fixture.Create();
        var review = Review.Create(null, f.Listing.Id, f.Customer.Id, f.Vendor.Id, 5, Content, null);
        review.Approve(); f.Db.Reviews.Add(review); await f.Db.SaveChangesAsync();
        var customer = new SubmitReviewEditRequestCommandHandler(f.Db, new Current(f.Customer.Id));
        var moderator = new ModerateReviewEditRequestCommandHandler(f.Db, new Current(f.Moderator.Id, "Moderator"));
        for (var attempt = 1; attempt <= 3; attempt++)
        {
            var saved = await customer.Handle(new(review.Id, 4, $"Lần gửi chỉnh sửa số {attempt} với nhận xét đầy đủ và hợp lệ."), default);
            var edit = await f.Db.ReviewEditRequests.SingleAsync(row => row.AttemptNumber == attempt);
            Assert.Equal(Content, saved.Content);
            Assert.Equal(ReviewEditStatus.Pending, edit.Status);
            await moderator.Handle(new(edit.Id, false, "Vui lòng cung cấp thêm thông tin xác thực."), default);
        }
        await Assert.ThrowsAsync<DomainException>(() => customer.Handle(
            new(review.Id, 4, "Lần gửi thứ tư vượt quá giới hạn đã được hệ thống cho phép."), default));
        Assert.Equal(Content, review.Content); Assert.Equal(5, review.Rating);
        Assert.Equal(3, await f.Db.ReviewEditRequests.CountAsync());
    }
    [Fact]
    public async Task ComboReviewsRequireEveryCompletedComponentAndDeduplicateShopPerComboOrder()
    {
        await using var f = await Fixture.Create();
        var template = ComboTemplate.Create("Ảnh cưới & trang điểm", "Popular", "Classic", null, null, 1);
        template.SetScope(f.Vendor.Id, "HCM", 0, true);
        template.Publish(f.Vendor.Id, "HCM", 0, "{}", null, null, true, null);
        f.Db.ComboTemplates.Add(template);
        var revision = ComboTemplateRevision.Create(template.Id, 1, "{\"immutable\":true}",
            f.Owner.Id, "VendorOwner", true);
        revision.Review(true, f.Moderator.Id, "Moderator", "Đã duyệt phiên bản combo.");
        f.Db.ComboTemplateRevisions.Add(revision);
        var legacyOrder = ComboOrder.Create(f.Customer.Id, template.Id, DateTime.UtcNow.AddDays(-2), 100, null,
            [(f.Listing.Id, f.Vendor.Id, f.Category.Id, f.Listing.Title, 1000000)]);
        legacyOrder.CaptureTemplateVersion(null, JsonSerializer.Serialize(new
        {
            schemaVersion = 1, templateId = template.Id, revisionId = revision.Id,
            revisionSequence = revision.Sequence, snapshotKind = "ApprovedRevision",
            revisionSnapshotJson = revision.SnapshotJson
        }));
        var unrelatedRevision = ComboTemplateRevision.Create(template.Id, 2, "{\"different\":true}",
            f.Owner.Id, "VendorOwner", true);
        Assert.False(ReviewTransactionEvidence.HasMatchingTemplateSnapshot(legacyOrder, unrelatedRevision));
        var snapshot = JsonSerializer.Serialize(new
        {
            schemaVersion = 1, templateId = template.Id, revisionId = revision.Id,
            revisionSequence = revision.Sequence, snapshotKind = "ApprovedRevision",
            revisionSnapshotJson = revision.SnapshotJson
        });
        var order = ComboOrder.Create(f.Customer.Id, template.Id, DateTime.UtcNow.AddDays(-2), 100, null,
            [(f.Listing.Id, f.Vendor.Id, f.Category.Id, f.Listing.Title, 1000000)]);
        order.SetPricing(0, "HCM", false);
        order.CaptureTemplateVersion(revision.Id, snapshot);
        f.Db.ComboOrders.Add(order);
        var lead = Lead.Create(f.Customer.Id, f.Vendor.Id, f.Listing.Id, "0900000000",
            DateTime.UtcNow.AddDays(-2), 100, 1000000, null);
        lead.AttachSource("Combo", order.Id);
        f.Db.Leads.Add(lead);
        var item = Assert.Single(order.Items); item.AttachLead(lead.Id);
        var contract = BookingContract.Create(lead.Id, null, f.Vendor.Id, f.Customer.Id,
            1000000, 100000, null, lead.WeddingDate);
        contract.ConfirmByCustomer(); f.Db.BookingContracts.Add(contract);
        await f.Db.SaveChangesAsync();
        var shopHandler = new SubmitShopReviewCommandHandler(f.Db, new Current(f.Customer.Id));
        var comboHandler = new SubmitComboReviewCommandHandler(f.Db, new Current(f.Customer.Id));
        await Assert.ThrowsAsync<DomainException>(() => shopHandler.Handle(new(null, order.Id, 5, Content), default));
        await Assert.ThrowsAsync<DomainException>(() => comboHandler.Handle(new(order.Id, 5, Content), default));
        contract = await f.Db.BookingContracts.SingleAsync(candidate => candidate.Id == contract.Id);
        contract.CompleteContract(); await f.Db.SaveChangesAsync();
        Assert.Equal(ContractStatus.Completed, contract.Status);
        Assert.Equal(order.Id, (await f.Db.Leads.SingleAsync()).ComboOrderId);
        Assert.Equal(lead.Id, item.LeadId);
        var savedContract = await f.Db.BookingContracts.SingleAsync();
        Assert.Equal(lead.Id, savedContract.LeadId);
        Assert.Equal(f.Customer.Id, savedContract.CustomerId);
        Assert.Equal(f.Vendor.Id, savedContract.VendorId);
        Assert.Equal(ContractStatus.Completed, savedContract.Status);
        Assert.Equal(1, await f.Db.BookingContracts.CountAsync(candidate =>
            candidate.LeadId == lead.Id && candidate.CustomerId == f.Customer.Id &&
            candidate.Status == ContractStatus.Completed && candidate.VendorId == f.Vendor.Id));
        Assert.Equal("Shop", (await shopHandler.Handle(new(null, order.Id, 4, Content), default)).TargetType);
        Assert.Equal("Combo", (await comboHandler.Handle(new(order.Id, 5, Content), default)).TargetType);
        await Assert.ThrowsAsync<DomainException>(() => shopHandler.Handle(new(null, order.Id, 4, Content), default));
        await Assert.ThrowsAsync<DomainException>(() => comboHandler.Handle(new(order.Id, 4, Content), default));
    }

    private sealed record Current(Guid Id, string ActorRole = "Customer") : ICurrentUserService
    { public Guid? UserId => Id; public string? Email => "customer@test.vn"; public string? Role => ActorRole; }
    private sealed class Fixture : IAsyncDisposable
    {
        public SqliteConnection Connection = null!; public ApplicationDbContext Db = null!; public User Customer = null!; public User Owner = null!; public User Moderator = null!; public Vendor Vendor = null!; public Listing Listing = null!; public Category Category = null!;
        public static async Task<Fixture> Create()
        {
            var f = new Fixture(); f.Connection = new("Data Source=:memory:"); await f.Connection.OpenAsync(); f.Db = new(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(f.Connection).Options); await f.Db.Database.EnsureCreatedAsync();
            f.Customer = User.Create("Customer", "customer@test.vn", "0900000000", "hash", UserRole.Customer);
            f.Owner = User.Create("Owner", "vendor@test.vn", "0900000001", "hash", UserRole.VendorOwner);
            f.Moderator = User.Create("Moderator", "moderator@test.vn", "0900000002", "hash", UserRole.Moderator);
            f.Db.Users.AddRange(f.Customer, f.Owner, f.Moderator);
            f.Vendor = Vendor.Create(f.Owner.Id, "Vendor", "vendor", "Owner", "0900000001", "HCM", .08m); f.Db.Vendors.Add(f.Vendor);
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
