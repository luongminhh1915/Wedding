using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Wedding.Application.Common.Interfaces;
using Wedding.Application.Features.CommissionPolicies;
using Wedding.Domain.Entities;
using Wedding.Domain.Enums;
using Wedding.Domain.Exceptions;
using Wedding.Infrastructure.Persistence;

namespace Wedding.Tests;

public class CommissionAdjustmentEligibilityTests
{
    [Fact]
    public Task VendorOwnerCanRequestOnlyTheirCompletedContractOutstandingObligation_AndDecisionDoesNotTouchLedger() =>
        OwnCompletedObligationAndLedger(false);

    [SqlServerFact]
    public Task SqlServer_OwnCompletedOutstandingObligation_DecisionPreservesLedger() =>
        OwnCompletedObligationAndLedger(true);

    private static async Task OwnCompletedObligationAndLedger(bool sqlServer)
    {
        await using var fixture = await Fixture.Create(completed: true, sqlServer: sqlServer);
        var handler = new CreateCommissionAdjustmentCommandHandler(fixture.Db, new Actor(fixture.Owner.Id, "VendorOwner"));

        var request = await handler.Handle(new(fixture.Contract.Id, fixture.Commission.Id, .07m, "Documented revised terms"), default);
        Assert.Equal(CommissionAdjustmentStatus.Submitted, request.Status);
        Assert.Equal(50_000, request.OutstandingAmountSnapshot);

        var duplicate = await Assert.ThrowsAsync<DomainException>(() =>
            handler.Handle(new(fixture.Contract.Id, fixture.Commission.Id, .06m, "Second pending"), default));
        Assert.Contains("đang chờ", duplicate.Message);

        var adminHandler = new DecideCommissionAdjustmentCommandHandler(fixture.Db, new Actor(fixture.Admin.Id, "SuperAdmin"));
        await adminHandler.Handle(new(request.Id, request.Version, true, "Approved for manual reconciliation only"), default);

        var current = await fixture.Db.Commissions.SingleAsync(x => x.Id == fixture.Commission.Id);
        Assert.Equal(CommissionStatus.Unsettled, current.Status);
        Assert.Equal(30_000, current.PaidAmount);
        Assert.Equal(50_000, current.OutstandingAmount);
        Assert.Equal(80_000, current.CommissionAmount);
    }

    [Fact]
    public Task AdjustmentRejectsOtherOwnerPaidObligationAndFrozenObligation() =>
        DeniedOwnerPaidAndFrozen(false);

    [SqlServerFact]
    public Task SqlServer_AdjustmentRejectsOtherOwnerPaidObligationAndFrozenObligation() =>
        DeniedOwnerPaidAndFrozen(true);

    private static async Task DeniedOwnerPaidAndFrozen(bool sqlServer)
    {
        await using var otherOwnerFixture = await Fixture.Create(completed: true, sqlServer: sqlServer);
        var otherOwner = new CreateCommissionAdjustmentCommandHandler(otherOwnerFixture.Db,
            new Actor(otherOwnerFixture.OtherOwner.Id, "VendorOwner"));
        var foreign = await Assert.ThrowsAsync<DomainException>(() =>
            otherOwner.Handle(new(otherOwnerFixture.Contract.Id, otherOwnerFixture.Commission.Id, .07m, "Not owner"), default));
        var unknown = await Assert.ThrowsAsync<DomainException>(() =>
            otherOwner.Handle(new(Guid.NewGuid(), Guid.NewGuid(), .07m, "Unknown contract"), default));
        Assert.Equal(unknown.Message, foreign.Message);
        var otherContract = await otherOwnerFixture.Db.BookingContracts.SingleAsync(x => x.Id == otherOwnerFixture.Contract.Id);
        otherContract.MarkDisputed();
        await otherOwnerFixture.Db.SaveChangesAsync();
        var foreignDisputed = await Assert.ThrowsAsync<DomainException>(() =>
            otherOwner.Handle(new(otherOwnerFixture.Contract.Id, otherOwnerFixture.Commission.Id, .07m, "Foreign hold"), default));
        Assert.Equal(unknown.Message, foreignDisputed.Message);

        await using var paidFixture = await Fixture.Create(completed: true, fullyPaid: true, sqlServer: sqlServer);
        var paidHandler = new CreateCommissionAdjustmentCommandHandler(paidFixture.Db, new Actor(paidFixture.Owner.Id, "VendorOwner"));
        await Assert.ThrowsAsync<DomainException>(() =>
            paidHandler.Handle(new(paidFixture.Contract.Id, paidFixture.Commission.Id, .07m, "No outstanding balance"), default));

        await using var frozenFixture = await Fixture.Create(completed: true, frozen: true, sqlServer: sqlServer);
        var frozenHandler = new CreateCommissionAdjustmentCommandHandler(frozenFixture.Db, new Actor(frozenFixture.Owner.Id, "VendorOwner"));
        await Assert.ThrowsAsync<DomainException>(() =>
            frozenHandler.Handle(new(frozenFixture.Contract.Id, frozenFixture.Commission.Id, .07m, "Frozen obligation"), default));
    }

    [Fact]
    public async Task CompletedContractAllowsOutstandingInClosedSettlementButRejectsDisputedSettlement()
    {
        await using var closedFixture = await Fixture.Create(completed: true, closedSettlement: true);
        var closedHandler = new CreateCommissionAdjustmentCommandHandler(closedFixture.Db,
            new Actor(closedFixture.Owner.Id, "VendorOwner"));
        var accepted = await closedHandler.Handle(new(closedFixture.Contract.Id, closedFixture.Commission.Id, .07m, "Closed statement debt"), default);
        Assert.Equal(CommissionAdjustmentStatus.Submitted, accepted.Status);

        await using var disputedFixture = await Fixture.Create(completed: true, disputedSettlement: true);
        var disputedHandler = new CreateCommissionAdjustmentCommandHandler(disputedFixture.Db,
            new Actor(disputedFixture.Owner.Id, "VendorOwner"));
        await Assert.ThrowsAsync<DomainException>(() =>
            disputedHandler.Handle(new(disputedFixture.Contract.Id, disputedFixture.Commission.Id, .07m, "Disputed debt"), default));
    }

    [Fact]
    public async Task AdjustmentDecisionRequiresSuperAdminAndUnchangedCommissionVersion()
    {
        await using var fixture = await Fixture.Create(completed: true);
        var vendorHandler = new CreateCommissionAdjustmentCommandHandler(fixture.Db, new Actor(fixture.Owner.Id, "VendorOwner"));
        var request = await vendorHandler.Handle(new(fixture.Contract.Id, fixture.Commission.Id, .07m, "Synthetic request"), default);

        var wrongRole = new DecideCommissionAdjustmentCommandHandler(fixture.Db, new Actor(fixture.Owner.Id, "VendorOwner"));
        await Assert.ThrowsAsync<DomainException>(() =>
            wrongRole.Handle(new(request.Id, request.Version, true, "Should not decide"), default));

        var commission = await fixture.Db.Commissions.SingleAsync(x => x.Id == fixture.Commission.Id);
        commission.ApplyPayment(1_000, "synthetic-later-payment");
        await fixture.Db.SaveChangesAsync();
        var admin = new DecideCommissionAdjustmentCommandHandler(fixture.Db, new Actor(fixture.Admin.Id, "SuperAdmin"));
        await Assert.ThrowsAsync<DomainException>(() =>
            admin.Handle(new(request.Id, request.Version, true, "Stale proposal"), default));
    }

    [Fact]
    public Task AdjustmentAccessRechecksDatabaseActorsAndProtectsAdminQuery() => AccessAndQueryPrivacy(false);

    [SqlServerFact]
    public Task SqlServer_AdjustmentAccessRechecksDatabaseActorsAndProtectsAdminQuery() => AccessAndQueryPrivacy(true);

    private static async Task AccessAndQueryPrivacy(bool sqlServer)
    {
        await using var fixture = await Fixture.Create(completed: true, sqlServer: sqlServer);
        var vendorActor = new Actor(fixture.Owner.Id, "VendorOwner");
        var submitted = await new CreateCommissionAdjustmentCommandHandler(fixture.Db, vendorActor)
            .Handle(new(fixture.Contract.Id, fixture.Commission.Id, .07m, "Actor verification"), default);
        await Assert.ThrowsAsync<DomainException>(() =>
            new GetCommissionAdjustmentsQueryHandler(fixture.Db, vendorActor).Handle(new(), default));
        var staleAdminClaims = new Actor(fixture.Owner.Id, "SuperAdmin");
        await Assert.ThrowsAsync<DomainException>(() =>
            new GetCommissionAdjustmentsQueryHandler(fixture.Db, staleAdminClaims).Handle(new(), default));
        await Assert.ThrowsAsync<DomainException>(() =>
            new DecideCommissionAdjustmentCommandHandler(fixture.Db, staleAdminClaims)
                .Handle(new(submitted.Id, submitted.Version, false, "Forged role must fail"), default));
        var staleVendorClaims = new Actor(fixture.Admin.Id, "VendorOwner");
        await Assert.ThrowsAsync<DomainException>(() =>
            new CreateCommissionAdjustmentCommandHandler(fixture.Db, staleVendorClaims)
                .Handle(new(fixture.Contract.Id, fixture.Commission.Id, .06m, "Forged vendor role"), default));

        var adminActor = new Actor(fixture.Admin.Id, "SuperAdmin");
        var list = await new GetCommissionAdjustmentsQueryHandler(fixture.Db, adminActor).Handle(new(), default);
        Assert.Equal(submitted.Id, Assert.Single(list).Id);
        var admin = await fixture.Db.Users.SingleAsync(x => x.Id == fixture.Admin.Id);
        admin.SetStatus(false);
        await fixture.Db.SaveChangesAsync();
        await Assert.ThrowsAsync<DomainException>(() =>
            new GetCommissionAdjustmentsQueryHandler(fixture.Db, adminActor).Handle(new(), default));
        await Assert.ThrowsAsync<DomainException>(() =>
            new DecideCommissionAdjustmentCommandHandler(fixture.Db, adminActor)
                .Handle(new(submitted.Id, submitted.Version, false, "Inactive admin must fail"), default));
        var owner = await fixture.Db.Users.SingleAsync(x => x.Id == fixture.Owner.Id);
        owner.SetStatus(false);
        await fixture.Db.SaveChangesAsync();
        await Assert.ThrowsAsync<DomainException>(() =>
            new CreateCommissionAdjustmentCommandHandler(fixture.Db, vendorActor)
                .Handle(new(fixture.Contract.Id, fixture.Commission.Id, .06m, "Inactive owner must fail"), default));
        var unchanged = await fixture.Db.CommissionAdjustmentRequests.AsNoTracking().SingleAsync(x => x.Id == submitted.Id);
        Assert.Equal(CommissionAdjustmentStatus.Submitted, unchanged.Status);
        Assert.Equal(submitted.Version, unchanged.Version);
    }

    [Fact]
    public Task StaleRequestCanBeRejectedAfterPayment_AndReplacedWithoutChangingLedger() =>
        RejectStaleAndResubmit(false);

    [SqlServerFact]
    public Task SqlServer_StaleRequestCanBeRejectedAfterPayment_AndReplacedWithoutChangingLedger() =>
        RejectStaleAndResubmit(true);

    private static async Task RejectStaleAndResubmit(bool sqlServer)
    {
        await using var fixture = await Fixture.Create(completed: true, sqlServer: sqlServer);
        var vendor = new CreateCommissionAdjustmentCommandHandler(fixture.Db, new Actor(fixture.Owner.Id, "VendorOwner"));
        var submitted = await vendor.Handle(new(fixture.Contract.Id, fixture.Commission.Id, .07m, "Original proposal"), default);
        var commission = await fixture.Db.Commissions.SingleAsync(x => x.Id == fixture.Commission.Id);
        commission.ApplyPayment(1_000, "synthetic-later-payment");
        await fixture.Db.SaveChangesAsync();

        var admin = new DecideCommissionAdjustmentCommandHandler(fixture.Db, new Actor(fixture.Admin.Id, "SuperAdmin"));
        await Assert.ThrowsAsync<DomainException>(() =>
            admin.Handle(new(submitted.Id, submitted.Version, true, "Must not approve stale proposal"), default));
        var vendorDecision = new DecideCommissionAdjustmentCommandHandler(fixture.Db, new Actor(fixture.Owner.Id, "VendorOwner"));
        await Assert.ThrowsAsync<DomainException>(() =>
            vendorDecision.Handle(new(submitted.Id, submitted.Version, false, "Vendor cannot reject"), default));
        await Assert.ThrowsAsync<DomainException>(() =>
            admin.Handle(new(submitted.Id, Guid.NewGuid(), false, "Stale request version cannot reject"), default));
        var rejected = await admin.Handle(new(submitted.Id, submitted.Version, false, "Replaced after payment"), default);
        Assert.Equal(CommissionAdjustmentStatus.Rejected, rejected.Status);
        Assert.Equal(fixture.Admin.Id, rejected.DecidedById);
        Assert.NotNull(rejected.DecidedAt);
        Assert.Equal(submitted.CommissionVersionSnapshot, rejected.CommissionVersionSnapshot);
        Assert.Equal(submitted.OutstandingAmountSnapshot, rejected.OutstandingAmountSnapshot);
        await Assert.ThrowsAsync<DomainException>(() =>
            admin.Handle(new(rejected.Id, rejected.Version, false, "Cannot decide twice"), default));

        var replacement = await vendor.Handle(new(fixture.Contract.Id, fixture.Commission.Id, .06m, "Updated proposal"), default);
        Assert.NotEqual(submitted.Id, replacement.Id);
        Assert.Equal(49_000, replacement.OutstandingAmountSnapshot);
        var current = await fixture.Db.Commissions.AsNoTracking().SingleAsync(x => x.Id == fixture.Commission.Id);
        Assert.Equal(31_000, current.PaidAmount);
        Assert.Equal(80_000, current.CommissionAmount);
        Assert.Equal(.08m, current.CommissionRate);
        Assert.Equal("synthetic-later-payment", current.PaymentReferenceCode);
        Assert.Equal(CommissionStatus.Unsettled, current.Status);
        Assert.Equal(current.Version, replacement.CommissionVersionSnapshot);
        Assert.Equal(1, await fixture.Db.AuditLogs.CountAsync(x =>
            x.Action == "CommissionAdjustment.Rejected" && x.EntityId == submitted.Id.ToString()));
    }

    [Theory]
    [InlineData("Frozen")]
    [InlineData("EscrowLocked")]
    [InlineData("ContractDisputed")]
    [InlineData("SettlementDisputed")]
    public Task HeldRequestCanBeRejected_ButApprovalStaysBlockedUntilResolved(string hold) =>
        RejectHeldAndResubmit(hold, false);

    [SqlServerFact]
    public async Task SqlServer_HeldRequestsCanBeRejected_ButApprovalStaysBlockedUntilResolved()
    {
        foreach (var hold in new[] { "Frozen", "EscrowLocked", "ContractDisputed", "SettlementDisputed" })
            await RejectHeldAndResubmit(hold, true);
    }

    private static async Task RejectHeldAndResubmit(string hold, bool sqlServer)
    {
        await using var fixture = await Fixture.Create(completed: true, closedSettlement: hold == "SettlementDisputed", sqlServer: sqlServer);
        var vendor = new CreateCommissionAdjustmentCommandHandler(fixture.Db, new Actor(fixture.Owner.Id, "VendorOwner"));
        var submitted = await vendor.Handle(new(fixture.Contract.Id, fixture.Commission.Id, .07m, "Proposal before hold"), default);
        var commission = await fixture.Db.Commissions.SingleAsync(x => x.Id == fixture.Commission.Id);
        var contract = await fixture.Db.BookingContracts.SingleAsync(x => x.Id == fixture.Contract.Id);
        switch (hold)
        {
            case "Frozen": commission.Freeze(); break;
            case "EscrowLocked": commission.LockUntil(DateTime.UtcNow.AddDays(1)); break;
            case "ContractDisputed": contract.MarkDisputed(); break;
            case "SettlementDisputed":
                (await fixture.Db.CommissionSettlements.SingleAsync(x => x.Id == commission.SettlementId))
                    .Dispute("Synthetic hold");
                break;
        }
        await fixture.Db.SaveChangesAsync();
        var ledgerBefore = await LedgerSnapshot.Read(fixture.Db, fixture.Commission.Id);

        var admin = new DecideCommissionAdjustmentCommandHandler(fixture.Db, new Actor(fixture.Admin.Id, "SuperAdmin"));
        await Assert.ThrowsAsync<DomainException>(() =>
            admin.Handle(new(submitted.Id, submitted.Version, true, "Hold must prevent approval"), default));
        var rejected = await admin.Handle(new(submitted.Id, submitted.Version, false, "Close request while hold remains"), default);
        Assert.Equal(CommissionAdjustmentStatus.Rejected, rejected.Status);
        Assert.Equal(ledgerBefore, await LedgerSnapshot.Read(fixture.Db, fixture.Commission.Id));

        await Assert.ThrowsAsync<DomainException>(() =>
            vendor.Handle(new(fixture.Contract.Id, fixture.Commission.Id, .06m, "Hold still active"), default));
        commission = await fixture.Db.Commissions.SingleAsync(x => x.Id == fixture.Commission.Id);
        contract = await fixture.Db.BookingContracts.SingleAsync(x => x.Id == fixture.Contract.Id);
        switch (hold)
        {
            case "Frozen": commission.Unfreeze(DateTime.UtcNow); break;
            case "EscrowLocked": commission.Release(commission.ReleaseAt!.Value); break;
            case "ContractDisputed": contract.ResolveDispute(false, "Synthetic resolution"); break;
            case "SettlementDisputed":
                (await fixture.Db.CommissionSettlements.SingleAsync(x => x.Id == commission.SettlementId))
                    .ResolveDispute();
                break;
        }
        await fixture.Db.SaveChangesAsync();
        var replacement = await vendor.Handle(new(fixture.Contract.Id, fixture.Commission.Id, .06m, "After hold resolved"), default);
        Assert.Equal(CommissionAdjustmentStatus.Submitted, replacement.Status);
        Assert.NotEqual(submitted.Id, replacement.Id);
    }

    private sealed record LedgerSnapshot(decimal Amount, decimal Rate, decimal PaidAmount, CommissionStatus Status,
        string? Reference, Guid Version, ContractStatus ContractStatus, SettlementStatus? SettlementStatus,
        decimal? SettlementTotal, decimal? SettlementPaid, string? SettlementReference, Guid? SettlementVersion)
    {
        public static async Task<LedgerSnapshot> Read(ApplicationDbContext db, Guid commissionId)
        {
            var commission = await db.Commissions.AsNoTracking().SingleAsync(x => x.Id == commissionId);
            var contract = await db.BookingContracts.AsNoTracking().SingleAsync(x => x.Id == commission.ContractId);
            var settlement = commission.SettlementId.HasValue
                ? await db.CommissionSettlements.AsNoTracking().SingleAsync(x => x.Id == commission.SettlementId)
                : null;
            return new(commission.CommissionAmount, commission.CommissionRate, commission.PaidAmount, commission.Status,
                commission.PaymentReferenceCode, commission.Version, contract.Status, settlement?.Status,
                settlement?.TotalAmount, settlement?.PaidAmount, settlement?.PaymentReference, settlement?.Version);
        }
    }

    private sealed record Actor(Guid? UserId, string? Role) : ICurrentUserService
    {
        public string? Email => null;
    }

    private sealed class Fixture(SqliteConnection? connection, ApplicationDbContext db, CommissionSqlTestDatabase? sql = null) : IAsyncDisposable
    {
        public ApplicationDbContext Db => db;
        public User Owner = null!;
        public User OtherOwner = null!;
        public User Admin = null!;
        public Vendor OtherVendor = null!;
        public BookingContract Contract = null!;
        public Commission Commission = null!;

        public static async Task<Fixture> Create(bool completed, bool fullyPaid = false, bool frozen = false,
            bool closedSettlement = false, bool disputedSettlement = false, bool sqlServer = false)
        {
            Fixture fixture;
            if (sqlServer)
            {
                var sql = await CommissionSqlTestDatabase.CreateAsync();
                fixture = new Fixture(null, sql.Db, sql);
            }
            else
            {
                var connection = new SqliteConnection("Data Source=:memory:");
                await connection.OpenAsync();
                var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options;
                var sqliteDb = new ApplicationDbContext(options);
                await sqliteDb.Database.EnsureCreatedAsync();
                fixture = new Fixture(connection, sqliteDb);
            }
            var db = fixture.Db;
            try
            {
                fixture.Owner = AddUser(db, UserRole.VendorOwner);
                fixture.OtherOwner = AddUser(db, UserRole.VendorOwner);
                fixture.Admin = AddUser(db, UserRole.SuperAdmin);
                var customer = AddUser(db, UserRole.Customer);
                var vendor = Vendor.Create(fixture.Owner.Id, "Adjustment test vendor", $"vendor-{Guid.NewGuid():N}", "Owner", "0900000000", "HCM", .08m);
                db.Vendors.Add(vendor);
                fixture.OtherVendor = Vendor.Create(fixture.OtherOwner.Id, "Other test vendor", $"vendor-{Guid.NewGuid():N}", "Other owner", "0900000001", "HCM", .08m);
                db.Vendors.Add(fixture.OtherVendor);
                var category = Category.Create("Adjustment test category", $"category-{Guid.NewGuid():N}", null, null, .08m);
                db.Categories.Add(category);
                var listing = Listing.Create(vendor.Id, category.Id, "Test service", $"service-{Guid.NewGuid():N}", 100_000, 100_000, "Test", "HCM");
                listing.Approve();
                db.Listings.Add(listing);
                var lead = Lead.Create(customer.Id, vendor.Id, listing.Id, "0901111111", DateTime.UtcNow.AddDays(-2), 100, 100_000, "Test");
                db.Leads.Add(lead);
                fixture.Contract = BookingContract.Create(lead.Id, null, vendor.Id, customer.Id, 1_000_000, 200_000, null, DateTime.UtcNow.AddDays(-2));
                fixture.Contract.ConfirmByCustomer();
                if (completed) fixture.Contract.CompleteContract();
                db.BookingContracts.Add(fixture.Contract);
                fixture.Commission = Commission.Create(fixture.Contract.Id, vendor.Id, CommissionPeriod.Period1_Deposit, .08m, 80_000, DateTime.UtcNow);
                if (fullyPaid) fixture.Commission.ApplyPayment(80_000, "synthetic-test");
                else if (frozen) fixture.Commission.Freeze();
                else fixture.Commission.ApplyPayment(30_000, "synthetic-test");
                db.Commissions.Add(fixture.Commission);
                if (closedSettlement || disputedSettlement)
                {
                    var settlement = CommissionSettlement.Create(vendor.Id, "2026-10", 1, 80_000, DateTime.UtcNow.AddDays(30));
                    settlement.Close();
                    if (disputedSettlement) settlement.Dispute("Synthetic dispute");
                    db.CommissionSettlements.Add(settlement);
                    fixture.Commission.AssignSettlement(settlement.Id);
                }
                await db.SaveChangesAsync();
                return fixture;
            }
            catch
            {
                await fixture.DisposeAsync();
                throw;
            }
        }

        private static User AddUser(ApplicationDbContext db, UserRole role)
        {
            var user = User.Create("Synthetic test actor", $"{Guid.NewGuid():N}@commission-test.example.invalid",
                "0900000000", "not-a-real-login", role);
            db.Users.Add(user);
            return user;
        }

        public async ValueTask DisposeAsync()
        {
            if (sql != null)
            {
                await sql.DisposeAsync();
                return;
            }
            await db.DisposeAsync();
            await connection!.DisposeAsync();
        }
    }
}
