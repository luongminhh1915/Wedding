using System.Data;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Wedding.Application.Common.Interfaces;
using Wedding.Application.Features.CommissionPolicies;
using Wedding.Domain.Entities;
using Wedding.Domain.Enums;
using Wedding.Domain.Exceptions;
using Wedding.Infrastructure.Persistence;
using Wedding.WebApi.Controllers;

namespace Wedding.Tests;

public class CommissionPhase5SqlVerificationTests
{
    private const string Previous = "20261020123641_IndependentShopServiceComboReviews";
    private const string Current = "20261021145659_CommissionPolicyBasisAndAdjustments";
    private static readonly DateTime Start = new(2027, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void CommissionEndpointsDeclareLeastPrivilegeRoles()
    {
        var expected = new Dictionary<string, string>
        {
            [nameof(CommissionPoliciesController.Rules)] = "SuperAdmin,Finance",
            [nameof(CommissionPoliciesController.ComponentBases)] = "SuperAdmin,Finance",
            [nameof(CommissionPoliciesController.CreateRule)] = "SuperAdmin",
            [nameof(CommissionPoliciesController.SubmitRule)] = "SuperAdmin",
            [nameof(CommissionPoliciesController.ReviewRule)] = "SuperAdmin",
            [nameof(CommissionPoliciesController.CreateComponentBasis)] = "SuperAdmin",
            [nameof(CommissionPoliciesController.SubmitComponentBasis)] = "SuperAdmin",
            [nameof(CommissionPoliciesController.ReviewComponentBasis)] = "SuperAdmin",
            [nameof(CommissionPoliciesController.RequestAdjustment)] = "VendorOwner",
            [nameof(CommissionPoliciesController.Adjustments)] = "SuperAdmin",
            [nameof(CommissionPoliciesController.DecideAdjustment)] = "SuperAdmin"
        };
        foreach (var (name, roles) in expected)
        {
            var method = typeof(CommissionPoliciesController).GetMethod(name)!;
            Assert.Null(method.GetCustomAttribute<AllowAnonymousAttribute>());
            Assert.Equal(roles, Assert.Single(method.GetCustomAttributes<AuthorizeAttribute>()).Roles);
        }
    }

    [SqlServerFact]
    public async Task UpgradeIsAdditiveIdempotentAndPreservesExistingFinancialRows()
    {
        await using var owned = await CommissionSqlTestDatabase.CreateAsync(migrate: false);
        var db = owned.Db;
        var migrator = db.GetService<IMigrator>();
        await migrator.MigrateAsync(Previous);
        var seed = await SeedAsync(db, historicalUserSchema: true);
        var before = await CaptureFinanceAsync(db);
        var script = migrator.GenerateScript(Previous, Current, MigrationsSqlGenerationOptions.Idempotent);
        Assert.DoesNotContain("ALTER TABLE", script, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("UPDATE [", script, StringComparison.OrdinalIgnoreCase);
        var batches = Regex.Split(script, @"^\s*GO\s*$", RegexOptions.Multiline | RegexOptions.IgnoreCase)
            .Where(batch => !string.IsNullOrWhiteSpace(batch)).ToArray();
        for (var replay = 0; replay < 2; replay++)
            foreach (var batch in batches) await db.Database.ExecuteSqlRawAsync(batch);
        Assert.Equal(Current, (await db.Database.GetAppliedMigrationsAsync()).Last());
        Assert.False(db.Database.HasPendingModelChanges());
        Assert.Equal(before, await CaptureFinanceAsync(db));
        Assert.Empty(await db.CommissionPolicyRules.ToListAsync());
        Assert.Empty(await db.CommissionComponentBasisApprovals.ToListAsync());
        Assert.Empty(await db.CommissionAdjustmentRequests.ToListAsync());

        var rule = NewPolicy(seed.Admin.Id);
        db.CommissionPolicyRules.Add(rule);
        await db.SaveChangesAsync();
        // Test the guarded refusal only inside this fresh test-owned DB, never as a repair operation.
        owned.AssertTarget();
        var refusal = await Assert.ThrowsAsync<SqlException>(() => migrator.MigrateAsync(Previous));
        Assert.Equal(51000, refusal.Number);
        Assert.Equal(Current, (await db.Database.GetAppliedMigrationsAsync()).Last());
        Assert.Equal(1, await db.CommissionPolicyRules.CountAsync());
        Assert.Equal(before, await CaptureFinanceAsync(db));
    }

    [SqlServerFact]
    public async Task DatabaseShapeUniquenessAndAppendOnlyGuardsAreEnforced()
    {
        await using var owned = await CommissionSqlTestDatabase.CreateAsync();
        var db = owned.Db;
        var seed = await SeedAsync(db);
        var policy = NewPolicy(seed.Admin.Id);
        db.CommissionPolicyRules.Add(policy);
        var adjustment = CommissionAdjustmentRequest.Create(seed.Contract, seed.Commission, seed.Owner.Id, .07m, "Synthetic request");
        db.CommissionAdjustmentRequests.Add(adjustment);
        await db.SaveChangesAsync();
        var before = await CaptureFinanceAsync(db);
        var invalidShape = await Assert.ThrowsAsync<SqlException>(() => db.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE [CommissionPolicyRules] SET [Rate] = NULL WHERE [Id] = {policy.Id}"));
        Assert.Equal(547, invalidShape.Number);

        var duplicate = CommissionAdjustmentRequest.Create(seed.Contract, seed.Commission, seed.Owner.Id, .06m, "Duplicate pending request");
        db.CommissionAdjustmentRequests.Add(duplicate);
        var uniqueness = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        Assert.IsType<SqlException>(uniqueness.InnerException);
        Assert.Contains(((SqlException)uniqueness.InnerException!).Number, new[] { 2601, 2627 });
        db.ChangeTracker.Clear();

        var storedPolicy = await db.CommissionPolicyRules.SingleAsync(x => x.Id == policy.Id);
        db.Entry(storedPolicy).Property(x => x.Rate).CurrentValue = .09m;
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
        db.ChangeTracker.Clear();
        storedPolicy = await db.CommissionPolicyRules.SingleAsync(x => x.Id == policy.Id);
        db.CommissionPolicyRules.Remove(storedPolicy);
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
        db.ChangeTracker.Clear();
        var storedRequest = await db.CommissionAdjustmentRequests.SingleAsync(x => x.Id == adjustment.Id);
        db.Entry(storedRequest).Property(x => x.OutstandingAmountSnapshot).CurrentValue = 1m;
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
        db.ChangeTracker.Clear();
        storedRequest = await db.CommissionAdjustmentRequests.SingleAsync(x => x.Id == adjustment.Id);
        db.CommissionAdjustmentRequests.Remove(storedRequest);
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
        db.ChangeTracker.Clear();
        Assert.Equal(before, await CaptureFinanceAsync(db));
    }

    [SqlServerFact]
    public async Task ConcurrentRequestsAndDecisionsHaveOneWinnerWithoutLedgerMutation()
    {
        await using var owned = await CommissionSqlTestDatabase.CreateAsync();
        var seed = await SeedAsync(owned.Db);
        var before = await CaptureFinanceAsync(owned.Db);
        async Task<Exception?> SubmitAsync()
        {
            await using var db = owned.CreateContext();
            return await Record.ExceptionAsync(() => new CreateCommissionAdjustmentCommandHandler(db, new Actor(seed.Owner.Id, "VendorOwner"))
                .Handle(new(seed.Contract.Id, seed.Commission.Id, .07m, "Concurrent synthetic request"), default));
        }
        var submissions = await Task.WhenAll(SubmitAsync(), SubmitAsync());
        Assert.Single(submissions, error => error == null);
        Assert.IsType<DomainException>(Assert.Single(submissions, error => error != null));
        var request = await owned.Db.CommissionAdjustmentRequests.AsNoTracking().SingleAsync();
        async Task<Exception?> DecideAsync()
        {
            await using var db = owned.CreateContext();
            return await Record.ExceptionAsync(() => new DecideCommissionAdjustmentCommandHandler(db, new Actor(seed.Admin.Id, "SuperAdmin"))
                .Handle(new(request.Id, request.Version, true, "Append-only approval, no reconciliation"), default));
        }
        var decisions = await Task.WhenAll(DecideAsync(), DecideAsync());
        Assert.Single(decisions, error => error == null);
        Assert.IsType<DomainException>(Assert.Single(decisions, error => error != null));
        Assert.Equal(CommissionAdjustmentStatus.Approved,
            (await owned.Db.CommissionAdjustmentRequests.AsNoTracking().SingleAsync()).Status);
        Assert.Equal(1, await owned.Db.AuditLogs.CountAsync(x => x.Action == "CommissionAdjustment.Approved"));
        Assert.Equal(before, await CaptureFinanceAsync(owned.Db));
    }

    [SqlServerFact]
    public async Task ReadAccessRevalidatesDatabaseRoleAndNeverExposesAdjustmentsToFinanceOrVendor()
    {
        await using var owned = await CommissionSqlTestDatabase.CreateAsync();
        var db = owned.Db;
        var seed = await SeedAsync(db);
        var finance = new Actor(seed.Finance.Id, "Finance");
        Assert.Empty(await new GetCommissionPolicyRulesQueryHandler(db, finance).Handle(new(), default));
        Assert.Empty(await new GetCommissionComponentBasisQueryHandler(db, finance).Handle(new(Guid.NewGuid()), default));
        foreach (var actor in new[] { finance, new Actor(seed.Owner.Id, "VendorOwner"), new Actor(seed.Finance.Id, "SuperAdmin") })
            await Assert.ThrowsAsync<DomainException>(() => new GetCommissionAdjustmentsQueryHandler(db, actor).Handle(new(), default));
        await Assert.ThrowsAsync<DomainException>(() => new GetCommissionPolicyRulesQueryHandler(db,
            new Actor(seed.Owner.Id, "Finance")).Handle(new(), default));
        seed.Finance.SetStatus(false);
        await db.SaveChangesAsync();
        await Assert.ThrowsAsync<DomainException>(() => new GetCommissionPolicyRulesQueryHandler(db, finance).Handle(new(), default));
        await Assert.ThrowsAsync<DomainException>(() => new GetCommissionComponentBasisQueryHandler(db, finance).Handle(new(Guid.NewGuid()), default));
    }

    private static CommissionPolicyRule NewPolicy(Guid adminId) => CommissionPolicyRule.Create(CommissionRuleScope.System,
        null, null, null, null, null, .08m, CommissionBasisKind.StandardService, "Synthetic SQL v1", "Synthetic policy",
        Start, null, adminId);

    private static async Task<Seed> SeedAsync(ApplicationDbContext db, bool historicalUserSchema = false)
    {
        User AddUser(UserRole role)
        {
            var user = User.Create("Synthetic phase5 actor", $"{Guid.NewGuid():N}@phase5.example.invalid", "0900000000", "synthetic-not-a-login", role);
            if (!historicalUserSchema) db.Users.Add(user);
            return user;
        }
        var owner = AddUser(UserRole.VendorOwner);
        var admin = AddUser(UserRole.SuperAdmin);
        var finance = AddUser(UserRole.Finance);
        var customer = AddUser(UserRole.Customer);
        if (historicalUserSchema)
        {
            // This fixture intentionally stops before the Facebook identity migration.
            // Seed only historical User columns rather than letting the current EF
            // mapping write a FacebookSubject column that does not exist at this boundary.
            foreach (var user in new[] { owner, admin, finance, customer })
                await db.Database.ExecuteSqlInterpolatedAsync($"""
                    INSERT INTO [Users] ([Id], [FullName], [Email], [PhoneNumber], [PasswordHash], [Role], [IsActive], [CreatedAt])
                    VALUES ({user.Id}, {user.FullName}, {user.Email}, {user.PhoneNumber}, {user.PasswordHash}, {user.Role.ToString()}, {user.IsActive}, {user.CreatedAt});
                    """);
        }
        var vendor = Vendor.Create(owner.Id, "Synthetic phase5 vendor", $"phase5-{Guid.NewGuid():N}", "Synthetic owner", "0900000000", "HCM", .08m);
        if (historicalUserSchema)
        {
            // This migration fixture also predates Vendor.OperatingRegions (migration21).
            // Insert the original columns, retaining the exact original financial seed values.
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO [Vendors] ([Id],[UserId],[BrandName],[Slug],[ContactPerson],[Hotline],[City],
                    [CommissionRate],[RatingAvg],[TotalReviews],[IsActive],[IsVerified],[IsTopRated],
                    [AllowInCombos],[MaxComboDiscount],[CreatedAt])
                VALUES ({vendor.Id},{vendor.UserId},{vendor.BrandName},{vendor.Slug},{vendor.ContactPerson},
                    {vendor.Hotline},{vendor.City},{vendor.CommissionRate},{vendor.RatingAvg},{vendor.TotalReviews},
                    {vendor.IsActive},{vendor.IsVerified},{vendor.IsTopRated},{vendor.AllowInCombos},
                    {vendor.MaxComboDiscount},{vendor.CreatedAt});
                """);
        }
        else db.Vendors.Add(vendor);
        var category = Category.Create("Synthetic phase5 category", $"phase5-{Guid.NewGuid():N}", null, null, .08m);
        db.Categories.Add(category);
        var listing = Listing.Create(vendor.Id, category.Id, "Synthetic phase5 service", $"phase5-{Guid.NewGuid():N}", 100_000, 100_000, "Synthetic service", "HCM");
        listing.Approve();
        db.Listings.Add(listing);
        var weddingDate = DateTime.UtcNow.AddDays(-2);
        var lead = Lead.Create(customer.Id, vendor.Id, listing.Id, "0900000000", weddingDate, 100, 100_000, "Synthetic lead");
        db.Leads.Add(lead);
        var contract = BookingContract.Create(lead.Id, null, vendor.Id, customer.Id, 1_000_000, 200_000, null, weddingDate);
        contract.ConfirmByCustomer();
        contract.SnapshotCommissionRate(.08m);
        contract.CompleteContract();
        db.BookingContracts.Add(contract);
        var commission = Commission.Create(contract.Id, vendor.Id, CommissionPeriod.Period1_Deposit, .08m, 80_000, Start);
        commission.ApplyPayment(30_000, "synthetic-existing-payment");
        var settlement = CommissionSettlement.Create(vendor.Id, "2027-01", 1, 80_000, Start.AddDays(30));
        settlement.Close();
        db.CommissionSettlements.Add(settlement);
        commission.AssignSettlement(settlement.Id);
        db.Commissions.Add(commission);
        await db.SaveChangesAsync();
        return new(owner, admin, finance, contract, commission);
    }

    private static async Task<string> CaptureFinanceAsync(ApplicationDbContext db)
    {
        var connection = db.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open) await connection.OpenAsync();
        var result = new StringBuilder();
        foreach (var table in new[] { "BookingContracts", "Commissions", "CommissionSettlements", "Vendors" })
        {
            await using var command = connection.CreateCommand();
            command.CommandText = $"SELECT * FROM [{table}] ORDER BY [Id] FOR JSON PATH, INCLUDE_NULL_VALUES";
            await using var reader = await command.ExecuteReaderAsync();
            result.Append(table).Append(':');
            while (await reader.ReadAsync()) result.Append(reader.GetString(0));
        }
        return result.ToString();
    }

    private sealed record Seed(User Owner, User Admin, User Finance, BookingContract Contract, Commission Commission);
    private sealed record Actor(Guid? UserId, string? Role) : ICurrentUserService { public string? Email => null; }
}
