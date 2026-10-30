using System.Security.Cryptography;
using System.Text;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Wedding.Application.Common;
using Wedding.Application.Common.Interfaces;
using Wedding.Application.Features.Contracts;
using Wedding.Application.Features.Contracts.Commands.ConfirmContract;
using Wedding.Application.Features.Planner;
using Wedding.Domain.Entities;
using Wedding.Domain.Enums;
using Wedding.Domain.Exceptions;
using Wedding.Infrastructure.Persistence;
using Wedding.Infrastructure.Services;

namespace Wedding.Tests;

public class FinanceTests
{
    [Theory]
    [InlineData(100000000, 0.08, 4000000, 4000000)]
    [InlineData(1000001, 0.03, 15000, 15000)]
    [InlineData(1000010, 0.12, 60001, 60000)]
    public void CommissionIsSplitWithoutLosingRoundedDong(decimal value, decimal rate, decimal first, decimal second)
    { var split = CommissionRules.Split(value, rate); Assert.Equal(first, split.First); Assert.Equal(second, split.Second); Assert.Equal(Math.Round(value * rate, 0, MidpointRounding.AwayFromZero), split.First + split.Second); }

    [Fact]
    public void RateSnapshotCannotChangeOrEscapeBounds()
    {
        Assert.Equal(.06m, CommissionRules.ResolveRate(.06m, 0)); Assert.Equal(.1m, CommissionRules.ResolveRate(.06m, .1m));
        Assert.Throws<DomainException>(() => CommissionRules.ResolveRate(.06m, .15m));
        var contract = Contract(); contract.SnapshotCommissionRate(.08m); contract.SnapshotCommissionRate(.1m); Assert.Equal(.08m, contract.CommissionRateSnapshot);
    }
    [Fact]
    public void K2IsNotPayableBeforeWindowAndCannotBePaidWhenFrozen()
    {
        var c = Commission.Create(Guid.NewGuid(), Guid.NewGuid(), CommissionPeriod.Period2_Completion, .08m, 4000000, DateTime.UtcNow.AddDays(20));
        var release = DateTime.UtcNow.AddDays(3); c.LockUntil(release); c.Release(DateTime.UtcNow);
        Assert.Equal(CommissionStatus.EscrowLocked, c.Status); Assert.Throws<DomainException>(() => c.MarkPaid("test"));
        c.Release(release); c.Freeze(); Assert.Throws<DomainException>(() => c.MarkPaid("test")); c.Unfreeze(release); c.ApplyPayment(1000000, "BANK1"); Assert.Equal(3000000, c.OutstandingAmount);
    }
    [Fact]
    public void HmacUsesExactBodyAndRejectsWrongSignatures()
    {
        var secret = new string('x', 40); var body = "{\"amount\":130000}";
        var signature = Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(body)));
        Assert.True(BankWebhookSignature.IsValid(secret, signature, body));
        Assert.False(BankWebhookSignature.IsValid(secret, signature, body + " "));
        Assert.False(BankWebhookSignature.IsValid(secret, "garbage", body));
        Assert.False(BankWebhookSignature.IsValid(null, signature, body));
    }
    [Fact]
    public async Task PremiumRequiresMatchingDurableBankReceiptAndDuplicateIsIdempotent()
    {
        await using var fixture = await Fixture.Create(); var svc = fixture.Service;
        var order = await svc.CreatePremiumOrderAsync(fixture.Customer.Id, default);
        Assert.False(order.IsPremium); Assert.False(fixture.Invitation.IsPremium);
        var receipt = new BankReceipt("BANK-UNIQUE-001", "TestBank", "123456", 130000, order.PaymentRef);
        var first = await svc.ReceiveAsync(receipt, null, null, null, default);
        var second = await svc.ReceiveAsync(receipt, null, null, null, default);
        Assert.Equal(first.Id, second.Id); Assert.Equal("Matched", first.Status); Assert.True(fixture.Invitation.IsPremium);
        Assert.Equal(1, await fixture.Db.PaymentTransactions.CountAsync());
        Assert.Equal(1, await fixture.Db.PremiumOrders.CountAsync(o => o.Status == "Paid"));
        await Assert.ThrowsAsync<DomainException>(() => svc.ReceiveAsync(receipt with { Amount = 140000 }, null, null, null, default));
    }
    [Fact]
    public async Task UnknownOrWrongAmountReceiptIsDurableAndDoesNotUnlockPremium()
    {
        await using var fixture = await Fixture.Create(); var order = await fixture.Service.CreatePremiumOrderAsync(fixture.Customer.Id, default);
        var response = await fixture.Service.ReceiveAsync(new("BANK-WRONG-001", "TestBank", "123456", 129000, order.PaymentRef), null, null, null, default);
        Assert.Equal("Unmatched", response.Status); Assert.False(fixture.Invitation.IsPremium); Assert.Equal(1, await fixture.Db.PaymentTransactions.CountAsync());
        var duplicate = await fixture.Service.ReceiveAsync(new("BANK-WRONG-001", "TestBank", "123456", 129000, order.PaymentRef), null, null, null, default);
        Assert.Equal(response.Id, duplicate.Id);
    }
    [Fact]
    public async Task OldClientConfirmIsDeniedWithoutChangingInvitation()
    {
        await using var fixture = await Fixture.Create();
        var handler = new ActivateInvitationPremiumCommandHandler(fixture.Db, new Current(fixture.Customer.Id), fixture.Service);
        await Assert.ThrowsAsync<DomainException>(() => handler.Handle(new(true), default)); Assert.False(fixture.Invitation.IsPremium);
    }
    [Fact]
    public async Task CancellationCreditsOnlyPaidCommissionAboveRetainedDepositLiability()
    {
        await using var fixture = await Fixture.Create();
        var vendorUser = User.Create("Vendor", "vendor@test.vn", "0900000001", "hash", UserRole.VendorOwner); fixture.Db.Users.Add(vendorUser);
        var vendor = Vendor.Create(vendorUser.Id, "Vendor", "vendor", "Vendor", "0900000001", "HCM", .08m); fixture.Db.Vendors.Add(vendor);
        var lead = Lead.Create(fixture.Customer.Id, vendor.Id, null, "0900000000", DateTime.UtcNow.AddDays(100), 100, 100000000, null); fixture.Db.Leads.Add(lead);
        var contract = BookingContract.Create(lead.Id, null, vendor.Id, fixture.Customer.Id, 100000000, 30000000, null, lead.WeddingDate); contract.ConfirmByCustomer(); contract.SnapshotCommissionRate(.08m); fixture.Db.BookingContracts.Add(contract);
        var first = Commission.Create(contract.Id, vendor.Id, CommissionPeriod.Period1_Deposit, .08m, 4000000, DateTime.UtcNow.AddDays(10)); first.MarkPaid("BANK-PAID"); fixture.Db.Commissions.Add(first);
        var second = Commission.Create(contract.Id, vendor.Id, CommissionPeriod.Period2_Completion, .08m, 4000000, DateTime.UtcNow.AddDays(110)); second.LockUntil(DateTime.UtcNow.AddDays(103)); fixture.Db.Commissions.Add(second);
        await fixture.Db.SaveChangesAsync();
        await FinancialContractService.CancelAsync(fixture.Db, contract, 10000000, "Khách hủy, NCC giữ 10 triệu theo chứng từ", default); await fixture.Db.SaveChangesAsync();
        Assert.Equal(ContractStatus.Cancelled, contract.Status); Assert.Equal(CommissionStatus.Cancelled, second.Status);
        var credit = await fixture.Db.CommissionCreditNotes.SingleAsync(); Assert.Equal(3200000, credit.Amount); Assert.Equal(800000, first.CommissionAmount);
    }
    [Fact]
    public void PendingContractCannotBypassCustomerConfirmationViaDispute()
    {
        var contract = Contract();
        Assert.Throws<DomainException>(() => contract.MarkDisputed());
        Assert.Equal(ContractStatus.PendingVerification, contract.Status); Assert.Null(contract.ConfirmedAt); Assert.Null(contract.CommissionRateSnapshot);
    }
    [Fact]
    public void ContinueDisputeRestoresCompletedRatherThanDowngradingContract()
    {
        var contract = BookingContract.Create(Guid.NewGuid(), null, Guid.NewGuid(), Guid.NewGuid(), 100000000, 30000000, null, DateTime.UtcNow.AddDays(-5));
        contract.ConfirmByCustomer(); contract.CompleteContract(); var completedAt = contract.CompletedAt;
        contract.MarkDisputed(); contract.ResolveDispute(false, "Đã xử lý và tiếp tục");
        Assert.Equal(ContractStatus.Completed, contract.Status); Assert.Equal(completedAt, contract.CompletedAt);
        var confirmed = Contract(); confirmed.ConfirmByCustomer(); confirmed.MarkDisputed(); confirmed.ResolveDispute(false, "Tiếp tục"); Assert.Equal(ContractStatus.Confirmed, confirmed.Status);
    }
    [Fact]
    public async Task OtpEmailIsDeliveredOnlyAfterDatabaseTransactionHasCommitted()
    {
        await using var f = await Fixture.Create();
        var delivery = new Delivery((target, code, ct) =>
        {
            Assert.Null(f.Db.Database.CurrentTransaction);
            Assert.Equal(1, f.Db.OtpCodes.Count()); Assert.NotEqual(code, f.Db.OtpCodes.Single().Code);
            return Task.CompletedTask;
        });
        var result = await new RequestOtpCommandHandler(f.Db, delivery).Handle(new(f.Customer.Email), default);
        Assert.True(result.Sent); Assert.False((await f.Db.OtpCodes.SingleAsync()).IsUsed);
    }
    [Fact]
    public async Task FailedOtpDeliveryInvalidatesOnlyItsGenerationNotANewerCode()
    {
        await using var f = await Fixture.Create(); Guid firstId = default; Guid newerId = default;
        var delivery = new Delivery(async (target, code, ct) =>
        {
            Assert.Null(f.Db.Database.CurrentTransaction); firstId = (await f.Db.OtpCodes.SingleAsync()).Id;
            var newer = OtpCode.Create(target, out _); newerId = newer.Id; f.Db.OtpCodes.Add(newer); await f.Db.SaveChangesAsync(ct);
            throw new DomainException("SMTP delivery failed");
        });
        await Assert.ThrowsAsync<DomainException>(() => new RequestOtpCommandHandler(f.Db, delivery).Handle(new(f.Customer.Email), default));
        Assert.True((await f.Db.OtpCodes.SingleAsync(o => o.Id == firstId)).IsUsed);
        Assert.False((await f.Db.OtpCodes.SingleAsync(o => o.Id == newerId)).IsUsed);
    }
    [Fact]
    public async Task PartnerContractKeepsBuyerOwnershipButSyncsSharedPlannerOwner()
    {
        await using var f = await Fixture.Create();
        var owner = User.Create("Wedding owner", "owner@test.vn", "0900000002", "hash", UserRole.Customer);
        var vendorUser = User.Create("Vendor", "vendor@test.vn", "0900000001", "hash", UserRole.VendorOwner); f.Db.Users.AddRange(owner, vendorUser);
        var profile = WeddingProfile.Create(owner.Id); f.Db.WeddingProfiles.Add(profile); f.Db.WeddingMembers.Add(WeddingMember.Create(profile.Id, f.Customer.Id, "Editor"));
        var vendor = Vendor.Create(vendorUser.Id, "Vendor", "vendor", "Vendor", "0900000001", "HCM", .08m); f.Db.Vendors.Add(vendor);
        var category = Category.Create("Photo", "photo", null, null, .08m); f.Db.Categories.Add(category);
        var listing = Listing.Create(vendor.Id, category.Id, "Photo", "photo", 1000, 10000, "Photo", "HCM"); f.Db.Listings.Add(listing);
        var lead = Lead.Create(f.Customer.Id, vendor.Id, listing.Id, "0900000000", DateTime.UtcNow.AddDays(100), 100, 100000000, null); lead.Accept(); f.Db.Leads.Add(lead);
        var contract = BookingContract.Create(lead.Id, null, vendor.Id, f.Customer.Id, 100000000, 30000000, null, lead.WeddingDate); f.Db.BookingContracts.Add(contract); await f.Db.SaveChangesAsync();
        var result = await new ConfirmContractCommandHandler(f.Db, new Current(f.Customer.Id)).Handle(new(contract.Id), default);
        Assert.Equal(f.Customer.Id, result.CustomerId); Assert.Equal(f.Customer.Id, contract.CustomerId);
        var budget = await f.Db.BudgetItems.SingleAsync(); var task = await f.Db.ChecklistTasks.SingleAsync();
        Assert.Equal(owner.Id, budget.CustomerId); Assert.Equal(profile.Id, budget.WeddingProfileId); Assert.Equal(contract.Id, budget.ContractId);
        Assert.Equal(owner.Id, task.CustomerId); Assert.Equal(profile.Id, task.WeddingProfileId); Assert.Equal(contract.Id, task.ContractId);
        Assert.Equal(2, await f.Db.Commissions.CountAsync());
    }
    private static BookingContract Contract() => BookingContract.Create(Guid.NewGuid(), null, Guid.NewGuid(), Guid.NewGuid(), 100000000, 30000000, null, DateTime.UtcNow.AddDays(100));
    private sealed record Current(Guid Id) : ICurrentUserService { public Guid? UserId => Id; public string? Email => "customer@test.vn"; public string? Role => "Customer"; }
    private sealed class Audit : IAuditLogger { public Task LogAsync(string action, string entityType, string? entityId, object? data = null) => Task.CompletedTask; }
    private sealed class Delivery(Func<string, string, CancellationToken, Task> send) : IOtpDelivery { public bool EmailEnabled => true; public Task SendEmailAsync(string target, string code, CancellationToken ct) => send(target, code, ct); }
    private sealed class Fixture : IAsyncDisposable
    {
        public SqliteConnection Connection = null!; public ApplicationDbContext Db = null!; public User Customer = null!; public WeddingInvitation Invitation = null!; public PaymentLedgerService Service = null!;
        public static async Task<Fixture> Create()
        {
            var f = new Fixture(); f.Connection = new SqliteConnection("Data Source=:memory:"); await f.Connection.OpenAsync(); f.Db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(f.Connection).Options); await f.Db.Database.EnsureCreatedAsync();
            f.Customer = User.Create("Customer", "customer@test.vn", "0900000000", "hash", UserRole.Customer); f.Db.Users.Add(f.Customer);
            f.Invitation = WeddingInvitation.Create(f.Customer.Id, "test-wedding", "Minh", "Anh", DateTime.UtcNow.AddDays(100), "Venue", "Address", null, null); f.Db.WeddingInvitations.Add(f.Invitation); await f.Db.SaveChangesAsync();
            var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Payments:BankCode"] = "TestBank", ["Payments:AccountNumber"] = "123456", ["Payments:AccountName"] = "Test" }).Build(); f.Service = new PaymentLedgerService(f.Db, config, new Audit()); return f;
        }
        public async ValueTask DisposeAsync() { await Db.DisposeAsync(); await Connection.DisposeAsync(); }
    }
}
