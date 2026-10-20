using System.Text.Json;
using Microsoft.Data.SqlClient;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Wedding.Application.Common.Interfaces;
using Wedding.Application.Features.Contracts.Commands.CompleteContract;
using Wedding.Application.Features.Contracts.Commands.ConfirmContract;
using Wedding.Application.Features.Contracts.DTOs;
using Wedding.Application.Features.Contracts.Queries.GetContractDetail;
using Wedding.Application.Features.Contracts.Queries.GetCustomerContracts;
using Wedding.Application.Features.Contracts.Queries.GetVendorContracts;
using Wedding.Domain.Entities;
using Wedding.Domain.Enums;
using Wedding.Domain.Exceptions;
using Wedding.Infrastructure.Persistence;

namespace Wedding.Tests;

public class ContractCategorySnapshotTests
{
    [Theory]
    [InlineData(.12, 0)] [InlineData(.12, 1)] [InlineData(.12, 2)]
    [InlineData(0, 0)] [InlineData(0, 1)] [InlineData(0, 2)]
    public async Task CompletionWithoutOriginalRateCannotUseCurrentShopRateOrInventEightPercent(decimal shopRate, int periods)
    {
        await using var f = await Fixture.Create(originalRate: null, shopRate: shopRate);
        for (var index = 0; index < periods; index++) await f.AddCommission(index == 0 ? CommissionPeriod.Period1_Deposit : CommissionPeriod.Period2_Completion);
        var before = await State(f.Db);
        var error = await Assert.ThrowsAsync<DomainException>(() => f.Complete());
        Assert.Contains("snapshot", error.Message);
        Assert.Equal(ContractStatus.Confirmed, f.Contract.Status);
        Assert.Equal(before, await State(f.Db));
    }

    [Theory]
    [InlineData(.0)] [InlineData(.029)] [InlineData(.121)]
    public async Task CorruptOriginalRateFailsBeforeAnyCompletionOrFinancialWrite(decimal invalid)
    {
        await using var f = await Fixture.Create();
        // Simulate a corrupt historical import, not a legitimate domain operation.
        await f.Db.Database.ExecuteSqlInterpolatedAsync($"UPDATE BookingContracts SET CommissionRateSnapshot = {invalid} WHERE Id = {f.Contract.Id}");
        f.Db.ChangeTracker.Clear(); var before = await State(f.Db);
        await Assert.ThrowsAsync<DomainException>(() => f.Complete());
        Assert.Equal(before, await State(f.Db));
    }

    [Fact]
    public async Task PartialExistingPaymentRemainsUntouchedAndMissingK2UsesOriginalNotCurrentRate()
    {
        await using var f = await Fixture.Create(originalRate: .08m, shopRate: .12m);
        var first = await f.AddCommission(CommissionPeriod.Period1_Deposit, payment: 1000000m);
        var original = await CommissionState(f.Db, first.Id);
        var result = await f.Complete();
        Assert.Equal("Completed", result.Status); Assert.Equal(.08m, result.CommissionRateSnapshot);
        Assert.Null(result.CategorySnapshot); // Historical category metadata is not silently invented at completion.
        Assert.Equal(original, await CommissionState(f.Db, first.Id));
        var second = await f.Db.Commissions.AsNoTracking().SingleAsync(item => item.Period == CommissionPeriod.Period2_Completion);
        Assert.Equal(.08m, second.CommissionRate); Assert.Equal(4000000m, second.CommissionAmount);
        Assert.Equal(CommissionStatus.EscrowLocked, second.Status);
        Assert.Equal(8000000m, await f.Db.Commissions.SumAsync(item => item.CommissionAmount));
        var completed = await State(f.Db);
        await Assert.ThrowsAsync<DomainException>(() => f.Complete());
        Assert.Equal(completed, await State(f.Db));
    }

    [Fact]
    public async Task CompletePreservesBothExistingPeriodsAndDoesNotReleaseK2Early()
    {
        await using var f = await Fixture.Create();
        await f.AddCommission(CommissionPeriod.Period1_Deposit, payment: 4000000m);
        await f.AddCommission(CommissionPeriod.Period2_Completion);
        var before = await CommissionState(f.Db);
        await f.Complete();
        Assert.Equal(before, await CommissionState(f.Db));
        Assert.Equal(2, await f.Db.Commissions.CountAsync());
    }

    [Theory]
    [InlineData(UserRole.Customer)] [InlineData(UserRole.VendorOwner)] [InlineData(UserRole.Moderator)]
    [InlineData(UserRole.Finance)] [InlineData(UserRole.SuperAdmin)]
    public async Task CompletionRetainsExistingAuthorizedActorMatrix(UserRole role)
    {
        await using var f = await Fixture.Create();
        Assert.Equal("Completed", (await f.Complete(f.Actor(role))).Status);
    }

    [Theory]
    [InlineData("inactive")] [InlineData("spoofed-role")] [InlineData("foreign-customer")]
    [InlineData("foreign-owner")] [InlineData("customer-care")]
    [InlineData("reassigned-customer")] [InlineData("reassigned-owner")]
    public async Task CompletionChecksPersistedActiveIdentityRoleAndOwnershipBeforeFinancialWrites(string kind)
    {
        await using var f = await Fixture.Create();
        Current actor;
        if (kind == "inactive") { f.Customer.SetStatus(false); actor = f.Actor(UserRole.Customer); }
        else if (kind == "spoofed-role") actor = new(f.OtherCustomer.Id, "SuperAdmin");
        else if (kind == "foreign-customer") actor = new(f.OtherCustomer.Id, "Customer");
        else if (kind == "foreign-owner") actor = new(f.OtherOwner.Id, "VendorOwner");
        else if (kind == "customer-care") actor = f.Actor(UserRole.CustomerCare);
        else if (kind == "reassigned-customer")
        {
            f.Db.Entry(f.Customer).Property(item => item.Role).CurrentValue = UserRole.VendorOwner;
            actor = new(f.Customer.Id, "VendorOwner");
        }
        else
        {
            f.Db.Entry(f.Owner).Property(item => item.Role).CurrentValue = UserRole.CustomerCare;
            actor = new(f.Owner.Id, "CustomerCare");
        }
        await f.Db.SaveChangesAsync(); var before = await State(f.Db);
        await Assert.ThrowsAsync<DomainException>(() => f.Complete(actor));
        Assert.Equal(before, await State(f.Db));
    }

    [Theory]
    [InlineData(true)] [InlineData(false)]
    public async Task ConfirmationCapturesCanonicalCategoryGroupAndActualRateSource(bool inheritsCategory)
    {
        await using var f = await Fixture.Create(confirmed: false, shopRate: inheritsCategory ? 0m : .12m);
        var result = await f.Confirm();
        AssertCategory(result.CategorySnapshot, f.Category, inheritsCategory);
        Assert.Equal(inheritsCategory ? .08m : .12m, result.CommissionRateSnapshot);
        Assert.Equal(result.CommissionRateSnapshot, (await f.Db.BookingContracts.AsNoTracking().SingleAsync()).CommissionRateSnapshot);
        var rows = await f.Db.Commissions.AsNoTracking().ToListAsync(); Assert.Equal(2, rows.Count);
        Assert.All(rows, row => Assert.Equal(result.CommissionRateSnapshot, row.CommissionRate));
        Assert.Equal(100000000m * result.CommissionRateSnapshot, rows.Sum(row => row.CommissionAmount));
    }

    [Fact]
    public async Task CatalogRenamesRateAndPolicyUpdatesDoNotRewriteConfirmedCategoryOrAnyReadModel()
    {
        await using var f = await Fixture.Create(confirmed: false, shopRate: 0m);
        var original = await f.Confirm(); var snapshot = original.CategorySnapshot;
        var ledger = await CommissionState(f.Db);
        f.Category.Update("Renamed current category", null, null, .03m, 99);
        var group = await f.Db.BusinessGroups.SingleAsync(item => item.Id == f.Category.BusinessGroupId);
        f.Db.Entry(group).Property(item => item.Name).CurrentValue = "Renamed current business group";
        f.Db.Entry(group).Property(item => item.PolicyVersion).CurrentValue = "future-policy-not-applied";
        await f.Db.SaveChangesAsync(); f.Db.ChangeTracker.Clear();
        var detail = await new GetContractDetailQueryHandler(f.Db, f.Actor(UserRole.Customer)).Handle(new(f.Contract.Id), default);
        var customer = Assert.Single(await new GetCustomerContractsQueryHandler(f.Db, f.Actor(UserRole.Customer)).Handle(new(), default));
        var vendor = Assert.Single(await new GetVendorContractsQueryHandler(f.Db, f.Actor(UserRole.VendorOwner)).Handle(new(), default));
        Assert.Equal(snapshot, detail.CategorySnapshot); Assert.Equal(snapshot, customer.CategorySnapshot); Assert.Equal(snapshot, vendor.CategorySnapshot);
        Assert.Equal(.08m, detail.CommissionRateSnapshot); Assert.Equal(ledger, await CommissionState(f.Db));
    }

    [Theory]
    [InlineData("category-id")] [InlineData("category-name")] [InlineData("group-id")]
    [InlineData("group-name")] [InlineData("source")] [InlineData("policy")] [InlineData("rate")]
    public async Task ConfirmedSnapshotCannotBeOverwrittenEvenThroughEfEntry(string field)
    {
        await using var f = await Fixture.Create(confirmed: false, shopRate: 0m);
        await f.Confirm(); var contract = await f.Db.BookingContracts.SingleAsync();
        switch (field)
        {
            case "category-id": f.Db.Entry(contract).Property(item => item.CommissionCategoryIdSnapshot).CurrentValue = Guid.NewGuid(); break;
            case "category-name": f.Db.Entry(contract).Property(item => item.CommissionCategoryNameSnapshot).CurrentValue = "Replacement"; break;
            case "group-id": f.Db.Entry(contract).Property(item => item.CommissionBusinessGroupIdSnapshot).CurrentValue = Guid.NewGuid(); break;
            case "group-name": f.Db.Entry(contract).Property(item => item.CommissionBusinessGroupNameSnapshot).CurrentValue = "Replacement"; break;
            case "source": f.Db.Entry(contract).Property(item => item.CommissionRateSourceSnapshot).CurrentValue = "LegacyVendor"; break;
            case "policy": f.Db.Entry(contract).Property(item => item.CommissionPolicyVersionSnapshot).CurrentValue = "replacement-policy"; break;
            default: f.Db.Entry(contract).Property(item => item.CommissionRateSnapshot).CurrentValue = .03m; break;
        }
        await Assert.ThrowsAsync<InvalidOperationException>(() => f.Db.SaveChangesAsync());
        f.Db.ChangeTracker.Clear();
        Assert.Equal(.08m, (await f.Db.BookingContracts.SingleAsync()).CommissionRateSnapshot);
    }

    [Fact]
    public async Task HistoricalNullMetadataCannotBeBackfilledFromCurrentCategory()
    {
        await using var f = await Fixture.Create(); var before = await State(f.Db);
        f.Contract.SnapshotCommissionCategory(f.Category, "LegacyVendor");
        await Assert.ThrowsAsync<InvalidOperationException>(() => f.Db.SaveChangesAsync());
        f.Db.ChangeTracker.Clear(); Assert.Equal(before, await State(f.Db));
    }

    [Theory]
    [InlineData("missing-listing")] [InlineData("inactive-category")] [InlineData("inactive-group")]
    [InlineData("missing-membership")] [InlineData("foreign-membership")] [InlineData("full-service")]
    public async Task InvalidCanonicalCategoryCannotConfirmOrWriteFinancialPlannerOrSnapshotData(string condition)
    {
        await using var f = await Fixture.Create(confirmed: false, shopRate: 0m);
        if (condition == "missing-listing") f.Db.Entry(f.Lead).Property(item => item.ListingId).CurrentValue = null;
        if (condition == "inactive-category") f.Db.Entry(f.Category).Property(item => item.IsActive).CurrentValue = false;
        if (condition == "inactive-group") f.Db.Entry(f.Category.BusinessGroup!).Property(item => item.IsActive).CurrentValue = false;
        if (condition == "missing-membership") f.Db.VendorBusinessGroups.RemoveRange(await f.Db.VendorBusinessGroups.ToListAsync());
        if (condition == "foreign-membership")
        {
            f.Db.VendorBusinessGroups.RemoveRange(await f.Db.VendorBusinessGroups.ToListAsync());
            f.Db.VendorBusinessGroups.Add(VendorBusinessGroup.Create(f.Vendor.Id, BusinessGroupPolicy.PhotoBridalMakeupId));
        }
        if (condition == "full-service")
        {
            var category = await f.Db.Categories.SingleAsync(item => item.Slug == "wedding-planner");
            f.Db.Entry(f.Listing).Property(item => item.CategoryId).CurrentValue = category.Id;
            f.Db.VendorBusinessGroups.Add(VendorBusinessGroup.Create(f.Vendor.Id, BusinessGroupPolicy.FullServicePlanningId));
        }
        await f.Db.SaveChangesAsync(); f.Db.ChangeTracker.Clear(); var before = await State(f.Db);
        await Assert.ThrowsAsync<DomainException>(() => f.Confirm());
        Assert.Equal(before, await State(f.Db));
        Assert.Empty(await f.Db.BudgetItems.ToListAsync()); Assert.Empty(await f.Db.ChecklistTasks.ToListAsync());
    }

    [Theory]
    [InlineData("inactive")] [InlineData("spoofed-role")] [InlineData("foreign-customer")]
    [InlineData("vendor-as-customer")] [InlineData("changed-role")]
    public async Task ConfirmationUsesPersistedCustomerIdentityAndNeverTrustsTheClaimedRole(string condition)
    {
        await using var f = await Fixture.Create(confirmed: false);
        var actor = f.Actor(UserRole.Customer);
        if (condition == "inactive") f.Customer.SetStatus(false);
        if (condition == "spoofed-role") actor = new(f.Customer.Id, "SuperAdmin");
        if (condition == "foreign-customer") actor = new(f.OtherCustomer.Id, "Customer");
        if (condition == "vendor-as-customer") actor = new(f.Owner.Id, "Customer");
        if (condition == "changed-role") f.Db.Entry(f.Customer).Property(item => item.Role).CurrentValue = UserRole.CustomerCare;
        await f.Db.SaveChangesAsync(); var before = await State(f.Db);
        await Assert.ThrowsAsync<DomainException>(() => new ConfirmContractCommandHandler(f.Db, actor).Handle(new(f.Contract.Id), default));
        Assert.Equal(before, await State(f.Db));
    }

    [Fact]
    public async Task LegacyUnmappedExistingCategoryStaysExplicitlyUnmappedAndKeepsOriginalShopRate()
    {
        await using var f = await Fixture.Create(confirmed: false, shopRate: .12m);
        var category = Category.Create("Legacy unmapped category", "legacy-unmapped-contract", null, null, .03m);
        f.Db.Categories.Add(category); f.Db.Entry(f.Listing).Property(item => item.CategoryId).CurrentValue = category.Id;
        await f.Db.SaveChangesAsync();
        var result = await f.Confirm();
        Assert.Equal(.12m, result.CommissionRateSnapshot); Assert.NotNull(result.CategorySnapshot);
        Assert.Equal(category.Id, result.CategorySnapshot.CategoryId); Assert.Equal(category.Name, result.CategorySnapshot.CategoryName);
        Assert.Null(result.CategorySnapshot.BusinessGroupId); Assert.Null(result.CategorySnapshot.BusinessGroupName);
        Assert.Null(result.CategorySnapshot.PolicyVersion); Assert.Equal("LegacyVendor", result.CategorySnapshot.RateSource);
    }

    [Fact]
    public async Task NewConfirmationCommandDoesNotAcceptClientSuppliedCategoryOrCommissionRate()
    {
        Assert.Equal(["ContractId"], typeof(ConfirmContractCommand).GetProperties()
            .Where(property => property.GetMethod?.IsPublic == true).Select(property => property.Name).ToArray());
        await using var f = await Fixture.Create(confirmed: false, shopRate: 0m);
        var result = await f.Confirm();
        Assert.NotNull(result.CategorySnapshot); Assert.Equal(f.Listing.CategoryId, result.CategorySnapshot.CategoryId);
        Assert.Equal(f.Category.DefaultCommissionRate, result.CommissionRateSnapshot);
    }

    [Fact]
    public async Task UnexpectedHistoricalRateOnPendingImportCannotAcquireInventedCurrentPolicyProvenance()
    {
        await using var f = await Fixture.Create(confirmed: false);
        f.Contract.SnapshotCommissionRate(.08m); await f.Db.SaveChangesAsync();
        var before = await State(f.Db);
        await Assert.ThrowsAsync<DomainException>(() => f.Confirm());
        Assert.Equal(before, await State(f.Db));
    }

    [SqlServerFact]
    public async Task SqlMigrationAddsOnlyNullableMetadataAndPreservesOldFinancialRowsThenSnapshotsNewConfirmation()
    {
        await using var f = await Fixture.Create(sql: true, originalRate: .08m, shopRate: .12m, legacySchema: true);
        var old = await f.Db.BookingContracts.AsNoTracking().SingleAsync();
        Assert.Null(old.CommissionCategoryIdSnapshot); Assert.Null(old.CommissionRateSourceSnapshot);
        Assert.Equal(.08m, old.CommissionRateSnapshot); Assert.Equal(ContractStatus.Confirmed, old.Status);
        await f.AddCommission(CommissionPeriod.Period1_Deposit, payment: 1000000m);
        var before = await CommissionState(f.Db); await f.Complete();
        Assert.Equal(before, await CommissionState(f.Db, (await f.Db.Commissions.SingleAsync(item => item.Period == CommissionPeriod.Period1_Deposit)).Id));
        Assert.Null((await f.Db.BookingContracts.AsNoTracking().SingleAsync()).CommissionCategoryIdSnapshot);

        var lead = Lead.Create(f.Customer.Id, f.Vendor.Id, f.Listing.Id, "0901234569", DateTime.UtcNow.AddDays(100), 100, 50000000m, null);
        lead.Accept(); f.Db.Leads.Add(lead);
        var next = BookingContract.Create(lead.Id, null, f.Vendor.Id, f.Customer.Id, 50000000m, 10000000m, null, lead.WeddingDate);
        f.Db.BookingContracts.Add(next);
        f.Db.Entry(next).Property(item => item.ContractCode).CurrentValue = $"HD-SYNTHETIC-{Guid.NewGuid():N}";
        await f.Db.SaveChangesAsync();
        var result = await new ConfirmContractCommandHandler(f.Db, f.Actor(UserRole.Customer)).Handle(new(next.Id), default);
        AssertCategory(result.CategorySnapshot, f.Category, inheritsCategory: false);
        f.Db.ChangeTracker.Clear();
        var reloaded = await new GetContractDetailQueryHandler(f.Db, f.Actor(UserRole.Customer)).Handle(new(next.Id), default);
        Assert.Equal(result.CategorySnapshot, reloaded.CategorySnapshot);
        Assert.Equal(.12m, reloaded.CommissionRateSnapshot); Assert.Equal(.08m, old.CommissionRateSnapshot);
        Assert.Empty(await f.Db.Database.GetPendingMigrationsAsync()); Assert.False(f.Db.Database.HasPendingModelChanges());
    }

    private static void AssertCategory(ContractCategorySnapshotDto? snapshot, Category category, bool inheritsCategory)
    {
        Assert.NotNull(snapshot); Assert.Equal(category.Id, snapshot.CategoryId); Assert.Equal(category.Name, snapshot.CategoryName);
        Assert.Equal(category.BusinessGroupId, snapshot.BusinessGroupId); Assert.Equal(category.BusinessGroup!.Name, snapshot.BusinessGroupName);
        Assert.Equal(inheritsCategory ? "CategoryDefault" : "LegacyVendor", snapshot.RateSource);
        Assert.Equal(inheritsCategory ? category.BusinessGroup.PolicyVersion : null, snapshot.PolicyVersion);
    }

    private static async Task<string> CommissionState(ApplicationDbContext db, Guid? onlyId = null) => JsonSerializer.Serialize(await db.Commissions.AsNoTracking()
        .Where(item => !onlyId.HasValue || item.Id == onlyId).OrderBy(item => item.Id)
        .Select(item => new { item.Id, item.ContractId, item.VendorId, item.Period, item.CommissionRate, item.CommissionAmount,
            item.Status, item.PaidAmount, item.PaidAt, item.PaymentReferenceCode, item.VietQrPayload, item.SettlementId,
            item.ReleaseAt, item.DueDate, item.Version, item.CreatedAt, item.UpdatedAt }).ToListAsync());

    private static async Task<string> State(ApplicationDbContext db) => JsonSerializer.Serialize(new
    {
        Contracts = await db.BookingContracts.AsNoTracking().OrderBy(item => item.Id).Select(item => new { item.Id, item.Status,
            item.ContractValue, item.DepositAmount, item.ConfirmedAt, item.CompletedAt, item.Version, item.UpdatedAt,
            item.CommissionRateSnapshot, item.CommissionCategoryIdSnapshot, item.CommissionCategoryNameSnapshot,
            item.CommissionBusinessGroupIdSnapshot, item.CommissionBusinessGroupNameSnapshot, item.CommissionRateSourceSnapshot,
            item.CommissionPolicyVersionSnapshot }).ToListAsync(),
        Commissions = await CommissionState(db), Audits = await db.AuditLogs.CountAsync(),
        Budgets = await db.BudgetItems.CountAsync(), Tasks = await db.ChecklistTasks.CountAsync()
    });

    private sealed record Current(Guid? UserId, string? Role) : ICurrentUserService { public string? Email => null; }

    private sealed class Fixture(ApplicationDbContext db, SqliteConnection? connection, string? databaseName) : IAsyncDisposable
    {
        public ApplicationDbContext Db { get; } = db;
        public User Owner { get; private set; } = null!;
        public User Customer { get; private set; } = null!;
        public User OtherCustomer { get; private set; } = null!;
        public User OtherOwner { get; private set; } = null!;
        private readonly Dictionary<UserRole, User> staff = [];
        public Vendor Vendor { get; private set; } = null!;
        public Category Category { get; private set; } = null!;
        public Listing Listing { get; private set; } = null!;
        public Lead Lead { get; private set; } = null!;
        public BookingContract Contract { get; private set; } = null!;
        public Current Actor(UserRole role) => new(role == UserRole.Customer ? Customer.Id : role == UserRole.VendorOwner ? Owner.Id : staff[role].Id, role.ToString());
        public Task<ContractDto> Complete(Current? actor = null) => new CompleteContractCommandHandler(Db, actor ?? Actor(UserRole.Customer)).Handle(new(Contract.Id), default);
        public Task<ContractDto> Confirm() => new ConfirmContractCommandHandler(Db, Actor(UserRole.Customer)).Handle(new(Contract.Id), default);

        public static async Task<Fixture> Create(decimal? originalRate = .08m, decimal shopRate = .12m,
            bool confirmed = true, bool sql = false, bool legacySchema = false)
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>(); SqliteConnection? connection = null; string? database = null;
            if (sql)
            {
                database = $"WeddingPlanTest_ContractCategory_{Guid.NewGuid():N}";
                var source = Environment.GetEnvironmentVariable("WEDDING_TEST_SQLSERVER");
                var builder = new SqlConnectionStringBuilder(string.IsNullOrWhiteSpace(source) || source == "1"
                    ? "Server=localhost;Integrated Security=true;TrustServerCertificate=true;Connection Timeout=5" : source) { InitialCatalog = database };
                options.UseSqlServer(builder.ConnectionString);
            }
            else { connection = new SqliteConnection("Data Source=:memory:"); await connection.OpenAsync(); options.UseSqlite(connection); }
            var f = new Fixture(new ApplicationDbContext(options.Options), connection, database);
            try
            {
                if (sql) await f.Db.GetService<IMigrator>().MigrateAsync(legacySchema ? "20261019115608_PublicationRevisions" : null);
                else await f.Db.Database.EnsureCreatedAsync();
                f.Owner = User.Create("Synthetic owner", "owner@contract-category.example.invalid", "0901234567", "hash", UserRole.VendorOwner);
                f.Customer = User.Create("Synthetic customer", "customer@contract-category.example.invalid", "0901234569", "hash", UserRole.Customer);
                f.OtherCustomer = User.Create("Other customer", "other@contract-category.example.invalid", "0901234570", "hash", UserRole.Customer);
                f.OtherOwner = User.Create("Other owner", "other-owner@contract-category.example.invalid", "0901234571", "hash", UserRole.VendorOwner);
                f.Db.Users.AddRange(f.Owner, f.Customer, f.OtherCustomer, f.OtherOwner);
                foreach (var role in new[] { UserRole.SuperAdmin, UserRole.Moderator, UserRole.Finance, UserRole.CustomerCare })
                {
                    var actor = User.Create($"Synthetic {role}", $"{role}@contract-category.example.invalid", "0901234572", "hash", role);
                    f.staff[role] = actor; f.Db.Users.Add(actor);
                }
                f.Vendor = shopRate == 0m ? Vendor.CreateWithCategoryDefaults(f.Owner.Id, "Synthetic shop", "contract-category", "Owner", "0901234567", "Hà Nội", BusinessGroupPolicy.InitialVersion)
                    : Vendor.Create(f.Owner.Id, "Synthetic shop", "contract-category", "Owner", "0901234567", "Hà Nội", shopRate);
                f.Db.Vendors.Add(f.Vendor);
                if (shopRate == 0m) f.Db.VendorBusinessGroups.Add(VendorBusinessGroup.Create(f.Vendor.Id, BusinessGroupPolicy.DecorInvitationsId));
                f.Category = await f.Db.Categories.Include(item => item.BusinessGroup).SingleAsync(item => item.Slug == "trang-tri-decor");
                f.Listing = Listing.Create(f.Vendor.Id, f.Category.Id, "Synthetic decor service", "contract-category-listing", 1000000m, 100000000m, "Synthetic description", "Hà Nội");
                f.Db.Listings.Add(f.Listing);
                // Yesterday is completable while K2 is unambiguously still locked, including after 17:00 UTC.
                f.Lead = Lead.Create(f.Customer.Id, f.Vendor.Id, f.Listing.Id, "0901234569", DateTime.UtcNow.Date.AddDays(-1), 100, 100000000m, null);
                f.Lead.Accept(); f.Db.Leads.Add(f.Lead); await f.Db.SaveChangesAsync();
                f.Contract = BookingContract.Create(f.Lead.Id, null, f.Vendor.Id, f.Customer.Id, 100000000m, 30000000m, null, f.Lead.WeddingDate);
                if (confirmed) { f.Contract.ConfirmByCustomer(); if (originalRate.HasValue) f.Contract.SnapshotCommissionRate(originalRate.Value); }
                if (legacySchema)
                {
                    var c = f.Contract;
                    await f.Db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO BookingContracts (Id,ContractCode,LeadId,VendorId,CustomerId,ContractValue,DepositAmount,WeddingDate,Status,VerificationDeadline,ConfirmedAt,CommissionRateSnapshot,CancellationPolicySnapshot,RevisionCount,Version,AppointmentDepositCredit,CreatedAt) VALUES ({c.Id},{c.ContractCode},{c.LeadId},{c.VendorId},{c.CustomerId},{c.ContractValue},{c.DepositAmount},{c.WeddingDate},{c.Status.ToString()},{c.VerificationDeadline},{c.ConfirmedAt},{c.CommissionRateSnapshot},{c.CancellationPolicySnapshot},{c.RevisionCount},{c.Version},{c.AppointmentDepositCredit},{c.CreatedAt})");
                    await f.Db.Database.MigrateAsync(); f.Db.ChangeTracker.Clear();
                    f.Contract = await f.Db.BookingContracts.SingleAsync();
                }
                else { f.Db.BookingContracts.Add(f.Contract); await f.Db.SaveChangesAsync(); }
                return f;
            }
            catch { await f.DisposeAsync(); throw; }
        }

        public async Task<Commission> AddCommission(CommissionPeriod period, decimal payment = 0m)
        {
            var item = Commission.Create(Contract.Id, Vendor.Id, period, .08m, 4000000m, DateTime.UtcNow.Date.AddDays(20));
            if (period == CommissionPeriod.Period2_Completion) item.LockUntil(Contract.WeddingDate!.Value.Date.AddDays(3).AddHours(-7));
            if (payment > 0m) item.ApplyPayment(payment, "SYNTHETIC-BANK-REFERENCE");
            Db.Commissions.Add(item); await Db.SaveChangesAsync(); return item;
        }

        public async ValueTask DisposeAsync()
        {
            if (databaseName != null) { Assert.StartsWith("WeddingPlanTest_ContractCategory_", databaseName); await Db.Database.EnsureDeletedAsync(); }
            await Db.DisposeAsync(); if (connection != null) await connection.DisposeAsync();
        }
    }
}
