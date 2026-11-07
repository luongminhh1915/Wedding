using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Wedding.Application.Common.Interfaces;
using Wedding.Application.Features.CommissionNegotiations;
using Wedding.Application.Features.CommissionPolicies;
using Wedding.Domain.Entities;
using Wedding.Domain.Enums;

namespace Wedding.Tests;

public sealed class CommissionNegotiationMigrationSqlTests
{
    private const string Previous = "20261029152219_AdminVendorContactAndOperatingAreas";
    private const string Current = "20261101152242_CommissionNegotiationFoundation";

    [SqlServerFact]
    public async Task AdditiveFoundationUpgradeReplayAndDecisionPreserveOriginalIdentityAndFinancialPolicy()
    {
        await using var owned = await CommissionSqlTestDatabase.CreateAsync(migrate: false);
        owned.AssertTarget();
        var db = owned.Db;
        var migrator = db.GetService<IMigrator>();
        await migrator.MigrateAsync(Previous);
        var owner = User.Create("Owned synthetic Vendor", "owner@negotiation.example.invalid", "0901234567", "synthetic-hash", UserRole.VendorOwner);
        var admin = User.Create("Owned synthetic Admin", "admin@negotiation.example.invalid", "0901234568", "synthetic-hash", UserRole.SuperAdmin);
        db.Users.AddRange(owner, admin);
        var vendor = Vendor.Create(owner.Id, "Owned synthetic shop", "owned-negotiation-shop", "Synthetic owner", "0901234567", "Hà Nội", .11m);
        db.Vendors.Add(vendor);
        await db.SaveChangesAsync();
        var identityBefore = await Identity(db, owner.Id);
        var policyBefore = await VendorPolicy(db, vendor.Id);
        var policiesBefore = await db.CommissionPolicyRules.CountAsync();
        var ledgersBefore = await db.Commissions.CountAsync();
        var oldMigrations = (await db.Database.GetAppliedMigrationsAsync()).Count();

        var script = migrator.GenerateScript(Previous, Current, MigrationsSqlGenerationOptions.Idempotent);
        Assert.Contains("CREATE TABLE [CommissionNegotiationRequests]", script);
        Assert.Contains("CREATE TABLE [CommissionNegotiationMessages]", script);
        Assert.DoesNotContain("DROP ", script, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("UPDATE ", script, StringComparison.OrdinalIgnoreCase);
        await migrator.MigrateAsync(Current);
        Assert.False(db.Database.HasPendingModelChanges());
        Assert.Equal(oldMigrations + 1, (await db.Database.GetAppliedMigrationsAsync()).Count());
        Assert.Equal(identityBefore, await Identity(db, owner.Id));
        Assert.Equal(policyBefore, await VendorPolicy(db, vendor.Id));

        var category = await db.Categories.FirstAsync(c => c.IsActive);
        var create = new CreateCommissionNegotiationCommandHandler(db, new Actor(owner.Id, nameof(UserRole.VendorOwner)));
        var request = await create.Handle(new(CommissionRuleScope.VendorCategory, category.Id, null, .07m,
            NegotiationReasonCategory.Other, "Synthetic negotiated proposal", null), default);
        Assert.Equal(.11m, request.CurrentRate);
        using (var decisionDb = owned.CreateContext())
        {
            var decide = new DecideNegotiationCommandHandler(decisionDb, new Actor(admin.Id, nameof(UserRole.SuperAdmin)));
            var result = await decide.Handle(new(request.Id, request.Version, true, .07m, "Decision only, not activation"), default);
            Assert.Equal(NegotiationStatus.Approved, result.Status);
            Assert.Null(result.ResultingPolicyRuleId);
        }
        db.ChangeTracker.Clear();
        var retainedMessages = await db.CommissionNegotiationMessages.CountAsync();
        Assert.Equal(2, retainedMessages);
        await ExecuteScript(db, script);
        Assert.Equal(retainedMessages, await db.CommissionNegotiationMessages.CountAsync());
        Assert.Single(await db.Database.GetAppliedMigrationsAsync(), m => m == Current);
        Assert.Equal(identityBefore, await Identity(db, owner.Id));
        Assert.Equal(policyBefore, await VendorPolicy(db, vendor.Id));
        Assert.Equal(policiesBefore, await db.CommissionPolicyRules.CountAsync());
        Assert.Equal(ledgersBefore, await db.Commissions.CountAsync());

        var failure = await Assert.ThrowsAsync<SqlException>(() => migrator.MigrateAsync(Previous));
        Assert.Equal(51000, failure.Number);
        Assert.Single(await db.CommissionNegotiationRequests.ToListAsync());
        Assert.Equal(retainedMessages, await db.CommissionNegotiationMessages.CountAsync());
        Assert.Contains(Current, await db.Database.GetAppliedMigrationsAsync());
    }

    [SqlServerFact]
    public async Task EffectiveResolverRemainsCompatibleWithDeployedSchema21WithoutNegotiationTables()
    {
        await using var owned = await CommissionSqlTestDatabase.CreateAsync(migrate: false);
        owned.AssertTarget();
        await owned.Db.GetService<IMigrator>().MigrateAsync(Previous);
        var first = await CommissionRateResolver.ResolveRateAsync(owned.Db, Guid.NewGuid(), Guid.NewGuid(),
            null, null, .11m, .08m, DateTime.UtcNow);
        var second = await CommissionRateResolver.ResolveRateAsync(owned.Db, Guid.NewGuid(), Guid.NewGuid(),
            null, null, 0m, .08m, DateTime.UtcNow);
        Assert.Equal(.11m, first.Rate);
        Assert.Equal("LegacyVendor", first.RateSource);
        Assert.Equal(.08m, second.Rate);
        Assert.Equal("CategoryDefault", second.RateSource);
        Assert.DoesNotContain(Current, await owned.Db.Database.GetAppliedMigrationsAsync());
        await owned.Db.Database.OpenConnectionAsync();
        try
        {
            await using var command = owned.Db.Database.GetDbConnection().CreateCommand();
            command.CommandText = "SELECT OBJECT_ID('dbo.CommissionNegotiationRequests')";
            Assert.Equal(DBNull.Value, await command.ExecuteScalarAsync());
        }
        finally { await owned.Db.Database.CloseConnectionAsync(); }
    }

    private static async Task<string> Identity(Wedding.Infrastructure.Persistence.ApplicationDbContext db, Guid id) =>
        JsonSerializer.Serialize(await db.Users.AsNoTracking().Where(u => u.Id == id)
            .Select(u => new { u.Id, u.FullName, u.Email, u.PhoneNumber, u.PasswordHash, u.Role, u.IsActive, u.LoginName, u.CreatedAt }).SingleAsync());

    private static async Task<string> VendorPolicy(Wedding.Infrastructure.Persistence.ApplicationDbContext db, Guid id) =>
        JsonSerializer.Serialize(await db.Vendors.AsNoTracking().Where(v => v.Id == id)
            .Select(v => new { v.Id, v.CommissionRate, v.PartnershipPolicyVersion, v.PartnershipPolicyAcceptedAt, v.OperatingRegions }).SingleAsync());

    private static async Task ExecuteScript(Wedding.Infrastructure.Persistence.ApplicationDbContext db, string script)
    {
        var alreadyOpen = db.Database.GetDbConnection().State == System.Data.ConnectionState.Open;
        if (!alreadyOpen) await db.Database.OpenConnectionAsync();
        try
        {
            foreach (var batch in Regex.Split(script, @"^\s*GO\s*$", RegexOptions.Multiline | RegexOptions.IgnoreCase))
            {
                if (string.IsNullOrWhiteSpace(batch)) continue;
                await using var command = db.Database.GetDbConnection().CreateCommand();
                command.CommandText = batch;
                await command.ExecuteNonQueryAsync();
            }
        }
        finally { if (!alreadyOpen) await db.Database.CloseConnectionAsync(); }
    }

    private sealed record Actor(Guid? UserId, string? Role) : ICurrentUserService { public string? Email => null; }
}
