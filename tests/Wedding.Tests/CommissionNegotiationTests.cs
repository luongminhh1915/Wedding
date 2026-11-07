using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Wedding.Application.Common.Interfaces;
using Wedding.Application.Features.CommissionNegotiations;
using Wedding.Domain.Entities;
using Wedding.Domain.Enums;
using Wedding.Domain.Exceptions;
using Wedding.Infrastructure.Persistence;
using Xunit;

namespace Wedding.Tests;

public class CommissionNegotiationTests : IAsyncDisposable
{
    private readonly SqliteConnection _connection;
    private readonly ApplicationDbContext _db;
    private readonly User _superAdmin;
    private readonly User _moderator;
    private readonly User _vendorOwner;
    private readonly User _otherVendorOwner;
    private readonly Vendor _vendor;
    private readonly Vendor _otherVendor;
    private readonly Category _category;
    private readonly ServiceItem _service;

    public CommissionNegotiationTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        _db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(_connection).Options);
        _db.Database.EnsureCreatedAsync().GetAwaiter().GetResult();

        _superAdmin = User.Create("Admin", $"admin-{Guid.NewGuid():N}@test.local", "0900000000", "admin-user", UserRole.SuperAdmin);
        _moderator = User.Create("Mod", $"mod-{Guid.NewGuid():N}@test.local", "0900000002", "mod-user", UserRole.Moderator);
        _vendorOwner = User.Create("Owner", $"owner-{Guid.NewGuid():N}@test.local", "0900000001", "owner-user", UserRole.VendorOwner);
        _otherVendorOwner = User.Create("OtherOwner", $"other-{Guid.NewGuid():N}@test.local", "0900000003", "other-user", UserRole.VendorOwner);
        _db.Users.AddRange(_superAdmin, _moderator, _vendorOwner, _otherVendorOwner);

        _category = Category.Create("Test Category", $"cat-{Guid.NewGuid():N}", null, null, 0.08m);
        _db.Categories.Add(_category);

        _vendor = Vendor.Create(_vendorOwner.Id, "Test Vendor", $"vendor-{Guid.NewGuid():N}", "Owner", "0900000001", "HCM", 0.08m);
        _otherVendor = Vendor.Create(_otherVendorOwner.Id, "Other Vendor", $"vendor-other-{Guid.NewGuid():N}", "Other", "0900000003", "HN", 0.08m);
        _db.Vendors.AddRange(_vendor, _otherVendor);

        var listing = Listing.Create(_vendor.Id, _category.Id, "Test Listing", $"listing-{Guid.NewGuid():N}", 100_000, 100_000, "Desc", "HCM");
        listing.Approve();
        _db.Listings.Add(listing);

        _service = ServiceItem.Create(_vendor.Id, listing.Id, _category.Id, "Test Service", 100_000, "Package", 1, 100, "HCM", "Terms");
        _service.Submit();
        _service.Review(true, null);
        _db.ServiceItems.Add(_service);

        _db.SaveChanges();
    }

    public async ValueTask DisposeAsync()
    {
        await _db.DisposeAsync();
        await _connection.DisposeAsync();
    }

    private ApplicationDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(_connection)
            .LogTo(Console.WriteLine, Microsoft.Extensions.Logging.LogLevel.Information)
            .Options);

    private class StubActor(Guid? userId, string? role, string? email = null) : ICurrentUserService
    {
        public Guid? UserId => userId;
        public string? Email => email ?? (userId.HasValue ? $"{userId.Value:N}@test.local" : null);
        public string? Role => role;
    }

    [Fact]
    public async Task CreateNegotiation_VendorRequest_SucceedsAndCreatesInitialMessage()
    {
        var handler = new CreateCommissionNegotiationCommandHandler(_db, new StubActor(_vendorOwner.Id, nameof(UserRole.VendorOwner)));
        var command = new CreateCommissionNegotiationCommand(
            CommissionRuleScope.Vendor,
            null,
            null,
            0.05m,
            NegotiationReasonCategory.VolumeGrowth,
            "Cam kết đạt 200 đơn tiệc trong năm 2026",
            "Mở rộng thêm 2 chi nhánh tại Hà Nội và Đà Nẵng");

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.NotEqual(Guid.Empty, result.Id);
        Assert.Equal(NegotiationStatus.Submitted, result.Status);
        Assert.Equal(0.05m, result.ProposedRate);
        Assert.Equal(_vendor.Id, result.VendorId);
        Assert.Single(result.Messages);
        Assert.Equal(nameof(UserRole.VendorOwner), result.Messages[0].SenderRole);
        Assert.Equal("Cam kết doanh số hoặc số lượng hợp đồng lớn", result.ReasonCategoryName);
    }

    [Theory]
    [InlineData(0.02)]
    [InlineData(0.15)]
    public async Task CreateNegotiation_OutOfBoundRates_ThrowsDomainException(decimal rate)
    {
        var handler = new CreateCommissionNegotiationCommandHandler(_db, new StubActor(_vendorOwner.Id, nameof(UserRole.VendorOwner)));
        var command = new CreateCommissionNegotiationCommand(
            CommissionRuleScope.Vendor,
            null,
            null,
            rate,
            NegotiationReasonCategory.NewPartner,
            "Lý do thử nghiệm",
            null);

        await Assert.ThrowsAsync<DomainException>(() => handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task CreateNegotiation_DuplicatePendingForSameTarget_ThrowsDomainException()
    {
        var handler = new CreateCommissionNegotiationCommandHandler(_db, new StubActor(_vendorOwner.Id, nameof(UserRole.VendorOwner)));
        var command = new CreateCommissionNegotiationCommand(
            CommissionRuleScope.Vendor,
            null,
            null,
            0.06m,
            NegotiationReasonCategory.MarginCost,
            "Chi phí thực phẩm tăng cao",
            null);

        await handler.Handle(command, CancellationToken.None);

        // Attempt second identical active request
        await Assert.ThrowsAsync<DomainException>(() => handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task NonVendorOwner_CreateNegotiation_ThrowsDomainException()
    {
        var handler = new CreateCommissionNegotiationCommandHandler(_db, new StubActor(_moderator.Id, nameof(UserRole.Moderator)));
        var command = new CreateCommissionNegotiationCommand(
            CommissionRuleScope.Vendor, null, null, 0.05m, NegotiationReasonCategory.Other, "Lý do", null);

        await Assert.ThrowsAsync<DomainException>(() => handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task Moderator_SendCounterOffer_TransitionsStatusToCounterOffered()
    {
        // 1. Vendor creates
        var createHandler = new CreateCommissionNegotiationCommandHandler(CreateDb(), new StubActor(_vendorOwner.Id, nameof(UserRole.VendorOwner)));
        var created = await createHandler.Handle(new CreateCommissionNegotiationCommand(
            CommissionRuleScope.Vendor, null, null, 0.04m, NegotiationReasonCategory.StrategicLongTerm, "Hợp tác lâu dài", null), CancellationToken.None);

        // 2. Moderator counter-offers
        var counterHandler = new CounterOfferNegotiationCommandHandler(CreateDb(), new StubActor(_moderator.Id, nameof(UserRole.Moderator)));
        var result = await counterHandler.Handle(new CounterOfferNegotiationCommand(
            created.Id, created.Version, 0.06m, "Sàn đề xuất mức 6% phù hợp hơn cho giai đoạn đầu"), CancellationToken.None);

        Assert.Equal(NegotiationStatus.CounterOffered, result.Status);
        Assert.Equal(0.06m, result.CounterRate);
        Assert.Equal(_moderator.Id, result.AssignedModeratorId);
        Assert.Equal(2, result.Messages.Count);
        Assert.Equal(nameof(UserRole.Moderator), result.Messages[1].SenderRole);
    }

    [Fact]
    public async Task Vendor_AcceptCounterOffer_TransitionsToInDiscussionWithProposedRateUpdated()
    {
        // 1. Vendor creates & Moderator counter-offers
        var createHandler = new CreateCommissionNegotiationCommandHandler(CreateDb(), new StubActor(_vendorOwner.Id, nameof(UserRole.VendorOwner)));
        var created = await createHandler.Handle(new CreateCommissionNegotiationCommand(
            CommissionRuleScope.Vendor, null, null, 0.04m, NegotiationReasonCategory.HighValueOrder, "Gói tiệc lớn", null), CancellationToken.None);

        var counterHandler = new CounterOfferNegotiationCommandHandler(CreateDb(), new StubActor(_moderator.Id, nameof(UserRole.Moderator)));
        var countered = await counterHandler.Handle(new CounterOfferNegotiationCommand(
            created.Id, created.Version, 0.055m, "Mức 5.5% hợp lý"), CancellationToken.None);

        // 2. Vendor accepts
        var respondHandler = new RespondToCounterOfferCommandHandler(CreateDb(), new StubActor(_vendorOwner.Id, nameof(UserRole.VendorOwner)));
        var result = await respondHandler.Handle(new RespondToCounterOfferCommand(
            countered.Id, countered.Version, true, "Đồng ý mức 5.5% này"), CancellationToken.None);

        Assert.Equal(NegotiationStatus.InDiscussion, result.Status);
        Assert.Equal(0.055m, result.ProposedRate);
        Assert.Equal(3, result.Messages.Count);
    }

    [Fact]
    public async Task SuperAdmin_Approve_RecordsDecisionWithoutCreatingOrActivatingPolicy()
    {
        // 1. Vendor creates
        var createHandler = new CreateCommissionNegotiationCommandHandler(CreateDb(), new StubActor(_vendorOwner.Id, nameof(UserRole.VendorOwner)));
        var created = await createHandler.Handle(new CreateCommissionNegotiationCommand(
            CommissionRuleScope.VendorCategory, _category.Id, null, 0.06m, NegotiationReasonCategory.NewPartner, "Ưu đãi năm đầu", null), CancellationToken.None);

        // A decision is retained, but it is not a signed/active financial policy.
        var decideHandler = new DecideNegotiationCommandHandler(CreateDb(), new StubActor(_superAdmin.Id, nameof(UserRole.SuperAdmin)));
        var result = await decideHandler.Handle(new DecideNegotiationCommand(
            created.Id, created.Version, true, 0.06m, "Đồng ý đề xuất 6%, chờ quy trình ký và kích hoạt riêng"), CancellationToken.None);

        Assert.Equal(NegotiationStatus.Approved, result.Status);
        Assert.Equal(0.06m, result.ApprovedRate);
        Assert.Equal(_superAdmin.Id, result.DecidedById);
        Assert.Null(result.ResultingPolicyRuleId);

        // No policy/ledger or legacy shop rate is changed by the negotiation.
        using var verifyDb = CreateDb();
        Assert.Empty(await verifyDb.CommissionPolicyRules.ToListAsync());
        Assert.Empty(await verifyDb.Commissions.ToListAsync());
        Assert.Equal(0.08m, (await verifyDb.Vendors.SingleAsync(v => v.Id == _vendor.Id)).CommissionRate);
    }

    [Fact]
    public async Task ExplicitAutoPolicyCreationIsRejectedWithoutChangingDecisionOrFinancialData()
    {
        var create = new CreateCommissionNegotiationCommandHandler(_db, new StubActor(_vendorOwner.Id, nameof(UserRole.VendorOwner)));
        var created = await create.Handle(new(CommissionRuleScope.Vendor, null, null, 0.06m,
            NegotiationReasonCategory.Other, "Yêu cầu hợp lệ", null), default);
        var decide = new DecideNegotiationCommandHandler(CreateDb(), new StubActor(_superAdmin.Id, nameof(UserRole.SuperAdmin)));
        await Assert.ThrowsAsync<DomainException>(() => decide.Handle(new(created.Id, created.Version,
            true, 0.06m, "Không được tự tạo policy", CreatePolicyRule: true), default));
        using var verify = CreateDb();
        Assert.Equal(NegotiationStatus.Submitted, (await verify.CommissionNegotiationRequests.SingleAsync()).Status);
        Assert.Empty(await verify.CommissionPolicyRules.ToListAsync());
        Assert.Empty(await verify.Commissions.ToListAsync());
        Assert.Equal(0.08m, (await verify.Vendors.SingleAsync(v => v.Id == _vendor.Id)).CommissionRate);
    }

    [Fact]
    public async Task CategoryRequestPreservesPositiveLegacyRateInsteadOfCategoryDefault()
    {
        _category.Update("Different category rate", null, null, 0.12m, 1);
        await _db.SaveChangesAsync();
        var create = new CreateCommissionNegotiationCommandHandler(_db, new StubActor(_vendorOwner.Id, nameof(UserRole.VendorOwner)));
        var result = await create.Handle(new(CommissionRuleScope.VendorCategory, _category.Id, null,
            0.06m, NegotiationReasonCategory.Other, "Giữ đúng mức hiện tại", null), default);
        Assert.Equal(0.08m, result.CurrentRate);
    }

    [Fact]
    public async Task Moderator_CannotApprove_ThrowsDomainException()
    {
        var createHandler = new CreateCommissionNegotiationCommandHandler(CreateDb(), new StubActor(_vendorOwner.Id, nameof(UserRole.VendorOwner)));
        var created = await createHandler.Handle(new CreateCommissionNegotiationCommand(
            CommissionRuleScope.Vendor, null, null, 0.05m, NegotiationReasonCategory.Other, "Lý do", null), CancellationToken.None);

        var decideHandler = new DecideNegotiationCommandHandler(CreateDb(), new StubActor(_moderator.Id, nameof(UserRole.Moderator)));
        await Assert.ThrowsAsync<DomainException>(() => decideHandler.Handle(new DecideNegotiationCommand(
            created.Id, created.Version, true, 0.05m, "Moderator thử duyệt", true), CancellationToken.None));
    }

    [Fact]
    public async Task Vendor_CancelOwnNegotiation_Succeeds()
    {
        var createHandler = new CreateCommissionNegotiationCommandHandler(CreateDb(), new StubActor(_vendorOwner.Id, nameof(UserRole.VendorOwner)));
        var created = await createHandler.Handle(new CreateCommissionNegotiationCommand(
            CommissionRuleScope.Vendor, null, null, 0.05m, NegotiationReasonCategory.Other, "Lý do", null), CancellationToken.None);

        var cancelHandler = new CancelNegotiationCommandHandler(CreateDb(), new StubActor(_vendorOwner.Id, nameof(UserRole.VendorOwner)));
        var result = await cancelHandler.Handle(new CancelNegotiationCommand(created.Id, "Đổi kế hoạch kinh doanh"), CancellationToken.None);

        Assert.Equal(NegotiationStatus.Cancelled, result.Status);
    }

    [Fact]
    public async Task GetMyNegotiations_OnlyReturnsOwnShopRequests()
    {
        // Vendor 1 creates
        var create1 = new CreateCommissionNegotiationCommandHandler(_db, new StubActor(_vendorOwner.Id, nameof(UserRole.VendorOwner)));
        await create1.Handle(new CreateCommissionNegotiationCommand(
            CommissionRuleScope.Vendor, null, null, 0.05m, NegotiationReasonCategory.NewPartner, "Shop 1", null), CancellationToken.None);

        // Vendor 2 creates
        var create2 = new CreateCommissionNegotiationCommandHandler(_db, new StubActor(_otherVendorOwner.Id, nameof(UserRole.VendorOwner)));
        await create2.Handle(new CreateCommissionNegotiationCommand(
            CommissionRuleScope.Vendor, null, null, 0.06m, NegotiationReasonCategory.NewPartner, "Shop 2", null), CancellationToken.None);

        // Query Vendor 1
        var queryHandler = new GetMyNegotiationsQueryHandler(_db, new StubActor(_vendorOwner.Id, nameof(UserRole.VendorOwner)));
        var list = await queryHandler.Handle(new GetMyNegotiationsQuery(), CancellationToken.None);

        Assert.Single(list);
        Assert.Equal(_vendor.Id, list[0].VendorId);
    }
}
