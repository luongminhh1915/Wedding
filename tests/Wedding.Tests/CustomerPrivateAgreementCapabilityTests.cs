using System.Text.Json;
using Microsoft.Data.SqlClient;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Wedding.Application.Common.Interfaces;
using Wedding.Application.Features.CustomerOrders;
using Wedding.Application.Features.PrivateServiceAgreements;
using Wedding.Application.Features.Quotes;
using Wedding.Domain.Entities;
using Wedding.Domain.Enums;
using Wedding.Domain.Exceptions;
using Wedding.Infrastructure.Persistence;

namespace Wedding.Tests;

public class CustomerPrivateAgreementCapabilityTests
{
    [Theory]
    [InlineData("PendingCustomer", true)]
    [InlineData("Accepted", true)]
    [InlineData("Rejected", false)]
    [InlineData("Withdrawn", false)]
    public async Task QuoteReadGuardMatchesCommandButStillAllowsCustomerToReject(string status, bool blocked)
    {
        await using var f = await Fixture.Create();
        await f.AddProposal(status, DateTime.UtcNow.AddHours(-1));
        var before = await f.Snapshot();
        var capabilities = (await f.Item("quote", f.Quote.Id)).Capabilities;
        Assert.True(capabilities.CanRespondQuote);
        Assert.Equal(!blocked, capabilities.CanAcceptQuote);
        Assert.Equal(blocked, capabilities.BlockedReason != null);
        Assert.Equal(before, await f.Snapshot());
        Assert.Empty(await CustomerOrderJourney.LoadAsync(f.Db, f.OtherCustomer.Id, default));

        if (blocked)
        {
            var exception = await Assert.ThrowsAsync<DomainException>(() =>
                PrivateAgreementContractHandoff.EnsureReviewedAsync(f.Db, f.Lead, f.Quote.CreatedAt, default));
            Assert.Equal(exception.Message, capabilities.BlockedReason);
            await Assert.ThrowsAsync<DomainException>(() => new RespondQuoteCommandHandler(f.Db, f.CustomerActor)
                .Handle(new(f.Quote.Id, true, null), default));
        }
        else await PrivateAgreementContractHandoff.EnsureReviewedAsync(f.Db, f.Lead, f.Quote.CreatedAt, default);

        await new RespondQuoteCommandHandler(f.Db, f.CustomerActor).Handle(new(f.Quote.Id, false, "Please reissue this quote"), default);
        Assert.Equal(QuoteStatus.Rejected, (await f.Db.Quotes.SingleAsync()).Status);
        await f.AssertNoFinancialWrites();
    }

    [Theory]
    [InlineData("PendingCustomer", true)]
    [InlineData("Accepted", true)]
    [InlineData("Rejected", false)]
    [InlineData("Withdrawn", false)]
    public async Task ContractReadGuardOnlyBlocksConfirmationNotRequestingCorrections(string status, bool blocked)
    {
        await using var f = await Fixture.Create(contract: true);
        await f.AddProposal(status, DateTime.UtcNow.AddHours(-1));
        var before = await f.Snapshot();
        var capabilities = (await f.Item("contract", f.Contract!.Id)).Capabilities;
        Assert.Equal(!blocked, capabilities.CanConfirm);
        Assert.True(capabilities.CanRequestChanges);
        Assert.True(capabilities.CanStopPending);
        Assert.True(capabilities.CanRequestChange);
        Assert.Equal(blocked, capabilities.BlockedReason != null);
        Assert.Equal(before, await f.Snapshot());
        await f.AssertNoFinancialWrites();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ReissuedContractUsesLinkedQuoteOrVerificationWindowNotOldDraftCreation(bool linkQuote)
    {
        await using var f = await Fixture.Create(contract: true, linkQuote: linkQuote, issuedAt: DateTime.UtcNow);
        await f.AddProposal("Accepted", DateTime.UtcNow.AddHours(-1));
        Assert.True(f.Contract!.CreatedAt < f.Quote.CreatedAt.AddDays(-1));
        var before = await f.Snapshot();
        var capabilities = (await f.Item("contract", f.Contract.Id)).Capabilities;
        Assert.True(capabilities.CanConfirm);
        Assert.Null(capabilities.BlockedReason);
        var issuedAt = linkQuote ? f.Quote.CreatedAt : f.Contract.VerificationDeadline.AddHours(-72);
        await PrivateAgreementContractHandoff.EnsureReviewedAsync(f.Db, f.Lead, issuedAt, default);
        Assert.Equal(before, await f.Snapshot());
    }

    [Fact]
    public async Task AcceptedCustomOrderIsScopedByOrderCustomerVendorAndKindNotPublicTemplate()
    {
        await using var f = await Fixture.Create();
        var sameTime = DateTime.UtcNow.AddHours(-1);
        var foreign = new[]
        {
            f.Proposal("PendingCustomer", sameTime, kind: "contract"),
            f.Proposal("PendingCustomer", sameTime, target: Guid.NewGuid()),
            f.Proposal("PendingCustomer", sameTime, customer: f.OtherCustomer.Id),
            f.Proposal("PendingCustomer", sameTime, vendor: Guid.NewGuid()),
        };
        Assert.Null(PrivateAgreementContractHandoff.ReviewBlockReason(f.Lead, f.Quote.CreatedAt, foreign));
        var standalone = Lead.Create(f.Customer.Id, f.Vendor.Id, f.Listing.Id, "0901234567", DateTime.UtcNow.AddDays(10), 100, 1_000_000, "Standalone selection");
        Assert.Null(PrivateAgreementContractHandoff.ReviewBlockReason(standalone, f.Quote.CreatedAt, [f.Proposal("PendingCustomer", sameTime)]));

        // No template/revision evidence is needed or inferred for the customer's custom order.
        var accepted = await f.AddProposal("Accepted", sameTime);
        var before = await f.Snapshot();
        var scoped = await CustomerOrderJourney.LoadAsync(f.Db, f.Customer.Id, default, [f.Order.Id], [f.Lead.Id]);
        var quote = Assert.Single(scoped, x => x.Kind == "quote");
        Assert.False(quote.Capabilities.CanAcceptQuote);
        Assert.True(quote.Capabilities.CanRespondQuote);
        Assert.Equal(f.Order.Id, quote.ComboOrderId);
        Assert.Contains("NCC cần gửi", quote.Capabilities.BlockedReason);
        Assert.Null(PrivateAgreementContractHandoff.ReviewBlockReason(f.Lead, accepted.RespondedAt!.Value, [accepted]));
        Assert.Equal(before, await f.Snapshot());
    }

    [Fact]
    public async Task SupersededQuoteCannotBecomeAcceptableAndConfirmedContractKeepsItsHistory()
    {
        await using var f = await Fixture.Create(contract: true);
        f.Contract!.ConfirmByCustomer();
        f.Contract.SnapshotCommissionRate(.08m);
        var state = CustomerOrderState.Create(f.Customer.Id, "quote", f.Quote.Id);
        state.Supersede(Guid.NewGuid());
        f.Db.CustomerOrderStates.Add(state);
        await f.Db.SaveChangesAsync();
        await f.AddProposal("PendingCustomer", DateTime.UtcNow.AddHours(-1));
        var before = await f.Snapshot();
        var quote = await f.Item("quote", f.Quote.Id);
        Assert.False(quote.Capabilities.CanRespondQuote);
        Assert.False(quote.Capabilities.CanAcceptQuote);
        var contract = await f.Item("contract", f.Contract.Id);
        Assert.False(contract.Capabilities.CanConfirm);
        Assert.DoesNotContain("đề xuất nội dung", contract.Capabilities.BlockedReason);
        Assert.Equal(before, await f.Snapshot());
        Assert.Equal(.08m, (await f.Db.BookingContracts.SingleAsync()).CommissionRateSnapshot);
    }

    [SqlServerFact]
    public async Task SqlProjectionRefreshesPendingAcceptedAndReissuedDocumentsWithoutChangingSnapshots()
    {
        // This test never accepts an application connection string. It owns a fresh local GUID database.
        var database = $"WeddingPlanTest_PrivateCapabilities_{Guid.NewGuid():N}";
        var builder = new SqlConnectionStringBuilder("Server=localhost;Integrated Security=true;TrustServerCertificate=true;Connection Timeout=5")
        { InitialCatalog = database };
        var owned = false;
        await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlServer(builder.ConnectionString).Options);
        try
        {
            await using (var master = new SqlConnection(new SqlConnectionStringBuilder(builder.ConnectionString) { InitialCatalog = "master" }.ConnectionString))
            {
                await master.OpenAsync();
                await using var command = master.CreateCommand();
                command.CommandText = "SELECT COUNT(*) FROM sys.databases WHERE name = @database";
                command.Parameters.AddWithValue("@database", database);
                Assert.Equal(0, Convert.ToInt32(await command.ExecuteScalarAsync()));
            }
            owned = true;
            await db.Database.MigrateAsync();
            await using var f = await Fixture.Create(existing: db);
            var pending = await f.AddProposal("PendingCustomer", DateTime.UtcNow.AddHours(-1));
            Assert.False((await f.Item("quote", f.Quote.Id)).Capabilities.CanAcceptQuote);
            Assert.True((await f.Item("quote", f.Quote.Id)).Capabilities.CanRespondQuote);
            pending.Respond(f.Customer.Id, false, "Keep original scope");
            await db.SaveChangesAsync();
            Assert.True((await f.Item("quote", f.Quote.Id)).Capabilities.CanAcceptQuote);
            await f.AddProposal("Accepted", DateTime.UtcNow.AddMinutes(-10));
            var original = await f.Snapshot();
            Assert.False((await f.Item("quote", f.Quote.Id)).Capabilities.CanAcceptQuote);
            Assert.Equal(original, await f.Snapshot());
            f.Quote.Supersede();
            var fresh = Quote.Create(f.Lead.Id, f.Vendor.Id, f.Customer.Id, f.Quote.ItemsJson, f.Quote.TotalAmount, f.Quote.DepositAmount, "Reissued after agreed content", 7, 2);
            db.Quotes.Add(fresh);
            await db.SaveChangesAsync();
            Assert.True((await f.Item("quote", fresh.Id)).Capabilities.CanAcceptQuote);
            var contract = BookingContract.Create(f.Lead.Id, null, f.Vendor.Id, f.Customer.Id, fresh.TotalAmount, fresh.DepositAmount, null, DateTime.UtcNow.AddDays(10));
            contract.LinkQuote(fresh.Id); db.BookingContracts.Add(contract);
            await db.SaveChangesAsync();
            var before = await f.Snapshot();
            Assert.True((await f.Item("contract", contract.Id)).Capabilities.CanConfirm);
            await f.AddProposal("PendingCustomer", DateTime.UtcNow);
            var pendingSnapshot = await f.Snapshot();
            Assert.False((await f.Item("contract", contract.Id)).Capabilities.CanConfirm);
            Assert.True((await f.Item("contract", contract.Id)).Capabilities.CanRequestChanges);
            Assert.Equal(pendingSnapshot, await f.Snapshot());
            Assert.NotEqual(before, pendingSnapshot);
            await f.AssertNoFinancialWrites();
        }
        finally
        {
            if (owned)
            {
                Assert.Equal("localhost", builder.DataSource);
                Assert.StartsWith("WeddingPlanTest_PrivateCapabilities_", database);
                Assert.True(Guid.TryParseExact(database["WeddingPlanTest_PrivateCapabilities_".Length..], "N", out _));
                Assert.Equal(database, db.Database.GetDbConnection().Database);
                await db.Database.EnsureDeletedAsync();
            }
        }
    }

    private sealed record Actor(Guid? UserId, string? Role) : ICurrentUserService { public string? Email => null; }
    private sealed class Fixture(ApplicationDbContext db, SqliteConnection? sqlite) : IAsyncDisposable
    {
        public ApplicationDbContext Db => db;
        public User Owner = null!, Customer = null!, OtherCustomer = null!;
        public Vendor Vendor = null!;
        public Listing Listing = null!;
        public ComboOrder Order = null!;
        public Lead Lead = null!;
        public Quote Quote = null!;
        public BookingContract? Contract;
        public Actor CustomerActor => new(Customer.Id, "Customer");
        public async Task<OrderJourneyItemDto> Item(string kind, Guid id) => (await CustomerOrderJourney.LoadAsync(db, Customer.Id, default)).Single(x => x.Kind == kind && x.Id == id);
        public PrivateServiceAgreement Proposal(string status, DateTime responseTime, string kind = "combo", Guid? target = null, Guid? customer = null, Guid? vendor = null, int sequence = 1)
        {
            var proposal = PrivateServiceAgreement.Create(kind, target ?? Order.Id, vendor ?? Vendor.Id, customer ?? Customer.Id, sequence, Owner.Id,
                new string('a', 64), 1_000_000, "[{\"Name\":\"Private replacement\",\"Quantity\":1,\"Details\":\"Private detail\"}]", "Agreed in chat", "No price change");
            if (status is "Accepted" or "Rejected") proposal.Respond(customer ?? Customer.Id, status == "Accepted", "Customer decision");
            if (status == "Withdrawn") proposal.Withdraw(Owner.Id);
            if (status != "PendingCustomer") db.Entry(proposal).Property(x => x.RespondedAt).CurrentValue = responseTime;
            return proposal;
        }
        public async Task<PrivateServiceAgreement> AddProposal(string status, DateTime responseTime)
        {
            var sequence = await db.PrivateServiceAgreements.Where(x => x.Kind == "combo" && x.TargetId == Order.Id).CountAsync() + 1;
            var proposal = Proposal(status, responseTime, sequence: sequence);
            db.PrivateServiceAgreements.Add(proposal); await db.SaveChangesAsync(); return proposal;
        }
        public async Task<string> Snapshot() => JsonSerializer.Serialize(new
        {
            Orders = await db.ComboOrders.AsNoTracking().Select(x => new { x.Id, x.Status, x.Total, x.Subtotal, x.DiscountAmount, x.ComboTemplateSnapshotJson }).ToListAsync(),
            Items = await db.ComboOrderItems.AsNoTracking().Select(x => new { x.Title, x.Quantity, x.Price, x.DiscountShare, x.ComponentSnapshotJson, x.SelectionJson }).ToListAsync(),
            Quotes = await db.Quotes.AsNoTracking().Select(x => new { x.Id, x.Status, x.ItemsJson, x.TotalAmount, x.DepositAmount, x.CreatedAt }).ToListAsync(),
            Contracts = await db.BookingContracts.AsNoTracking().Select(x => new { x.Id, x.Status, x.ContractValue, x.DepositAmount, x.CommissionRateSnapshot, x.CommissionCategoryIdSnapshot, x.Version, x.VerificationDeadline }).ToListAsync(),
            Proposals = await db.PrivateServiceAgreements.AsNoTracking().Select(x => new { x.Id, x.Kind, x.TargetId, x.Status, x.OriginalVersion, x.OriginalTotal, x.ItemsJson, x.RespondedAt }).ToListAsync(),
            AuditCount = await db.AuditLogs.CountAsync(),
        });
        public async Task AssertNoFinancialWrites()
        {
            Assert.Empty(await db.Commissions.ToListAsync());
            Assert.Empty(await db.PaymentTransactions.ToListAsync());
            Assert.Empty(await db.AdvancePayments.ToListAsync());
            Assert.Empty(await db.AuditLogs.Where(x => x.EntityType == nameof(Commission) || x.EntityType == nameof(PaymentTransaction) || x.EntityType == nameof(AdvancePayment)).ToListAsync());
        }
        public static async Task<Fixture> Create(bool contract = false, bool linkQuote = true, DateTime? issuedAt = null, ApplicationDbContext? existing = null)
        {
            SqliteConnection? sqlite = null;
            if (existing == null)
            {
                sqlite = new("Data Source=:memory:"); await sqlite.OpenAsync();
                existing = new(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(sqlite).Options);
                await existing.Database.EnsureCreatedAsync();
            }
            var f = new Fixture(existing, sqlite); var db = existing;
            User Add(UserRole role)
            {
                var user = User.Create("Synthetic capability account", $"{Guid.NewGuid():N}@private-capabilities.example.invalid", "0901234567", "not-a-real-login", role);
                db.Users.Add(user); return user;
            }
            f.Owner = Add(UserRole.VendorOwner); f.Customer = Add(UserRole.Customer); f.OtherCustomer = Add(UserRole.Customer);
            f.Vendor = Vendor.Create(f.Owner.Id, "Synthetic capability vendor", "capabilities-" + Guid.NewGuid().ToString("N"), "Owner", "0901234567", "HCM", .08m); db.Vendors.Add(f.Vendor);
            var category = await db.Categories.FirstAsync();
            f.Listing = Listing.Create(f.Vendor.Id, category.Id, "Original service", "capabilities-" + Guid.NewGuid().ToString("N"), 1_000_000, 1_000_000, "Original scope", "HCM"); f.Listing.Approve(); db.Listings.Add(f.Listing);
            f.Order = ComboOrder.Create(f.Customer.Id, null, DateTime.UtcNow.AddDays(10), 100, "Custom order", [(f.Listing.Id, f.Vendor.Id, category.Id, "Original title", 1_000_000)]); db.ComboOrders.Add(f.Order);
            f.Lead = Lead.Create(f.Customer.Id, f.Vendor.Id, f.Listing.Id, "0901234567", DateTime.UtcNow.AddDays(10), 100, 1_000_000, "Original lead"); f.Lead.Accept();
            f.Lead.AttachSource("Combo", f.Order.Id); f.Order.Items.Single().AttachLead(f.Lead.Id); db.Leads.Add(f.Lead);
            f.Quote = Quote.Create(f.Lead.Id, f.Vendor.Id, f.Customer.Id, "[{\"Name\":\"Original line\",\"Quantity\":1,\"UnitPrice\":1000000}]", 1_000_000, 200_000, "Original terms", 7, 1);
            db.Quotes.Add(f.Quote); db.Entry(f.Quote).Property(x => x.CreatedAt).CurrentValue = issuedAt ?? DateTime.UtcNow.AddHours(-2);
            if (contract)
            {
                f.Contract = BookingContract.Create(f.Lead.Id, null, f.Vendor.Id, f.Customer.Id, 1_000_000, 200_000, null, DateTime.UtcNow.AddDays(10));
                if (linkQuote) f.Contract.LinkQuote(f.Quote.Id);
                db.BookingContracts.Add(f.Contract);
                db.Entry(f.Contract).Property(x => x.CreatedAt).CurrentValue = DateTime.UtcNow.AddDays(-3);
                db.Entry(f.Contract).Property(x => x.VerificationDeadline).CurrentValue = (issuedAt ?? f.Quote.CreatedAt).AddHours(72);
            }
            await db.SaveChangesAsync(); return f;
        }
        public async ValueTask DisposeAsync() { if (sqlite != null) { await db.DisposeAsync(); await sqlite.DisposeAsync(); } }
    }
}
