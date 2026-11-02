using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Wedding.Domain.Entities;
using Wedding.Domain.Enums;
using Wedding.Infrastructure.Persistence;

namespace Wedding.Tests;

public sealed class AdminVendorMigrationSqlTests
{
    private const string PreviousMigration = "20261027152038_OptionalSocialContactEmail";
    private const string Migration = "20261029152219_AdminVendorContactAndOperatingAreas";
    private static readonly DateTime CreatedAt = new(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc);
    private static readonly DateTime AcceptedAt = CreatedAt.AddDays(-1);

    [SqlServerFact]
    public async Task UpgradeFromSchema20AndIdempotentReplayPreserveExistingAccountsAndVendorPolicy()
    {
        // Opt-in permits only this fixture's freshly created localhost GUID database.
        // No application/hosting configuration or environment connection string is read.
        await using var owned = await CommissionSqlTestDatabase.CreateAsync(migrate: false);
        owned.AssertTarget();
        var db = owned.Db;
        var migrator = db.GetService<IMigrator>();
        await migrator.MigrateAsync(PreviousMigration);
        var seed = await SeedLegacyRows(db);
        var before = (await db.Database.GetAppliedMigrationsAsync()).ToArray();
        Assert.DoesNotContain(Migration, before);

        var script = migrator.GenerateScript(PreviousMigration, Migration, MigrationsSqlGenerationOptions.Idempotent);
        Assert.Contains("IF NOT EXISTS", script);
        Assert.DoesNotContain("DROP COLUMN", script, StringComparison.OrdinalIgnoreCase);
        await db.Database.OpenConnectionAsync();
        try
        {
            await ExecuteScript(db, script);
            await AssertLegacyRows(db, seed);
            // Existing email accounts have no alias; a filtered unique index must allow both.
            Assert.Equal(2, await db.Users.CountAsync(user => user.LoginName == null));
            Assert.Equal(1, await Scalar(db, """
                SELECT COUNT(*) FROM sys.indexes
                WHERE object_id=OBJECT_ID(N'dbo.Users') AND name=N'IX_Users_LoginName'
                    AND is_unique=1 AND has_filter=1;
                """));

            var first = User.CreateAdministrativeVendor("Synthetic alias one", null, null, "synthetic-hash-one", "synthetic-alias-one");
            var second = User.CreateAdministrativeVendor("Synthetic alias two", null, null, "synthetic-hash-two", "synthetic-alias-two");
            db.Users.AddRange(first, second);
            await db.SaveChangesAsync();
            Assert.Equal(2, await db.Users.CountAsync(user => user.Email == null && user.Role == UserRole.VendorOwner));

            await using (var duplicate = owned.CreateContext())
            {
                duplicate.Users.Add(User.CreateAdministrativeVendor("Synthetic duplicate", null, null, "synthetic-hash-three", first.LoginName!));
                var failure = await Assert.ThrowsAsync<DbUpdateException>(() => duplicate.SaveChangesAsync());
                var sql = Assert.IsType<SqlException>(failure.InnerException);
                Assert.True(sql.Number is 2601 or 2627);
            }
            // A null-email Vendor must have a stable alias; staff still require contact email.
            var missingAlias = await Assert.ThrowsAsync<SqlException>(() =>
                db.Database.ExecuteSqlInterpolatedAsync($"UPDATE [Users] SET [LoginName]=NULL WHERE [Id]={first.Id};"));
            Assert.Equal(547, missingAlias.Number);
            var missingVendorEmailAndAlias = await Assert.ThrowsAsync<SqlException>(() =>
                db.Database.ExecuteSqlInterpolatedAsync($"UPDATE [Users] SET [Email]=NULL WHERE [Id]={seed.VendorOwnerId};"));
            Assert.Equal(547, missingVendorEmailAndAlias.Number);
            var missingStaffEmail = await Assert.ThrowsAsync<SqlException>(() =>
                db.Database.ExecuteSqlInterpolatedAsync($"UPDATE [Users] SET [Email]=NULL WHERE [Id]={seed.AdminId};"));
            Assert.Equal(547, missingStaffEmail.Number);

            await ExecuteScript(db, script);
            Assert.Equal(before.Length + 1, (await db.Database.GetAppliedMigrationsAsync()).Count());
            Assert.Single(await db.Database.GetAppliedMigrationsAsync(), value => value == Migration);
            Assert.Equal(4, await db.Users.CountAsync());
            Assert.Equal("synthetic-alias-one", (await db.Users.AsNoTracking().SingleAsync(user => user.Id == first.Id)).LoginName);
            Assert.Equal("synthetic-alias-two", (await db.Users.AsNoTracking().SingleAsync(user => user.Id == second.Id)).LoginName);
            await AssertLegacyRows(db, seed);
        }
        finally { await db.Database.CloseConnectionAsync(); }
    }

    [SqlServerFact]
    public async Task DownRefusesToLoseAliasesOrOperatingCoverageEvenWhenContactEmailExists()
    {
        await using var owned = await CommissionSqlTestDatabase.CreateAsync();
        owned.AssertTarget();
        var db = owned.Db;
        var seed = await SeedLegacyRows(db);
        var migrator = db.GetService<IMigrator>();

        // This user has a real synthetic email, so an email-only rollback guard is insufficient.
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE [Users] SET [LoginName]={"retained-synthetic-alias"} WHERE [Id]={seed.VendorOwnerId};");
        var aliasFailure = await Assert.ThrowsAsync<SqlException>(() => migrator.MigrateAsync(PreviousMigration));
        Assert.Equal(51000, aliasFailure.Number);
        Assert.Equal("retained-synthetic-alias", (await db.Users.AsNoTracking().SingleAsync(user => user.Id == seed.VendorOwnerId)).LoginName);
        Assert.Contains(Migration, await db.Database.GetAppliedMigrationsAsync());

        // Fixture-only alteration isolates coverage preservation from the alias branch.
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE [Users] SET [LoginName]=NULL WHERE [Id]={seed.VendorOwnerId};");
        foreach (var coverage in new[] { "*", "Hà Nội,Đà Nẵng", "" })
        {
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE [Vendors] SET [OperatingRegions]={coverage} WHERE [Id]={seed.VendorId};");
            var areaFailure = await Assert.ThrowsAsync<SqlException>(() => migrator.MigrateAsync(PreviousMigration));
            Assert.Equal(51000, areaFailure.Number);
            Assert.Equal(coverage, (await db.Vendors.AsNoTracking().SingleAsync(vendor => vendor.Id == seed.VendorId)).OperatingRegions);
            Assert.Contains(Migration, await db.Database.GetAppliedMigrationsAsync());
        }
        // No existing financial or identity snapshot was rewritten by rejected rollback attempts.
        await AssertLegacyRows(db, seed, expectNullCoverage: false);
    }

    private static async Task<LegacyRows> SeedLegacyRows(ApplicationDbContext db)
    {
        var seed = new LegacyRows(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO [Users] ([Id],[FullName],[Email],[PhoneNumber],[PasswordHash],[Role],[IsActive],[CreatedAt],[SecurityVersion])
            VALUES ({seed.VendorOwnerId},{"Preserved synthetic owner"},{"preserved-owner@migration.example.invalid"},
                {"0900000101"},{"preserved-password-hash"},{"VendorOwner"},{true},{CreatedAt},{9});
            INSERT INTO [Users] ([Id],[FullName],[Email],[PhoneNumber],[PasswordHash],[Role],[IsActive],[CreatedAt],[SecurityVersion])
            VALUES ({seed.AdminId},{"Preserved synthetic admin"},{"preserved-admin@migration.example.invalid"},
                {"0900000102"},{"preserved-admin-hash"},{"SuperAdmin"},{true},{CreatedAt},{7});
            INSERT INTO [Vendors] ([Id],[UserId],[BrandName],[Slug],[ContactPerson],[Hotline],[City],
                [CommissionRate],[RatingAvg],[TotalReviews],[IsActive],[IsVerified],[IsTopRated],
                [AllowInCombos],[MaxComboDiscount],[CreatedAt],[PartnershipPolicyAcceptedAt],[PartnershipPolicyVersion])
            VALUES ({seed.VendorId},{seed.VendorOwnerId},{"Preserved synthetic shop"},{"preserved-synthetic-shop"},
                {"Preserved synthetic owner"},{"0900000101"},{"Hà Nội"},{0.07m},{0m},{0},{true},{false},{false},
                {false},{0m},{CreatedAt},{AcceptedAt},{"legacy-approved-v1"});
            """);
        return seed;
    }

    private static async Task AssertLegacyRows(ApplicationDbContext db, LegacyRows seed, bool expectNullCoverage = true)
    {
        var user = await db.Users.AsNoTracking().SingleAsync(value => value.Id == seed.VendorOwnerId);
        Assert.Equal("preserved-owner@migration.example.invalid", user.Email);
        Assert.Equal("0900000101", user.PhoneNumber);
        Assert.Equal("preserved-password-hash", user.PasswordHash);
        Assert.Equal(9, user.SecurityVersion);
        Assert.Null(user.LoginName);
        Assert.Equal(UserRole.VendorOwner, user.Role);
        Assert.Equal(CreatedAt, user.CreatedAt);
        var admin = await db.Users.AsNoTracking().SingleAsync(value => value.Id == seed.AdminId);
        Assert.Equal("preserved-admin@migration.example.invalid", admin.Email);
        Assert.Equal("preserved-admin-hash", admin.PasswordHash);
        Assert.Equal(7, admin.SecurityVersion);
        var vendor = await db.Vendors.AsNoTracking().SingleAsync(value => value.Id == seed.VendorId);
        Assert.Equal("Hà Nội", vendor.City);
        Assert.Equal(0.07m, vendor.CommissionRate);
        Assert.Equal("legacy-approved-v1", vendor.PartnershipPolicyVersion);
        Assert.Equal(AcceptedAt, vendor.PartnershipPolicyAcceptedAt);
        Assert.False(vendor.IsVerified);
        Assert.Equal(CreatedAt, vendor.CreatedAt);
        if (expectNullCoverage) Assert.Null(vendor.OperatingRegions);
    }

    private static async Task ExecuteScript(ApplicationDbContext db, string script)
    {
        await db.Database.ExecuteSqlRawAsync("SET XACT_ABORT ON; SET ANSI_NULLS ON; SET QUOTED_IDENTIFIER ON; SET ANSI_PADDING ON; SET ANSI_WARNINGS ON; SET CONCAT_NULL_YIELDS_NULL ON; SET ARITHABORT ON; SET NUMERIC_ROUNDABORT OFF;");
        foreach (var batch in Regex.Split(script, @"^\s*GO\s*$", RegexOptions.Multiline | RegexOptions.IgnoreCase).Where(value => !string.IsNullOrWhiteSpace(value)))
        {
            await using var command = db.Database.GetDbConnection().CreateCommand();
            command.CommandText = batch;
            await command.ExecuteNonQueryAsync();
        }
    }

    private static async Task<int> Scalar(ApplicationDbContext db, string sql)
    {
        await using var command = db.Database.GetDbConnection().CreateCommand();
        command.CommandText = sql;
        return Convert.ToInt32(await command.ExecuteScalarAsync());
    }

    private sealed record LegacyRows(Guid VendorOwnerId, Guid AdminId, Guid VendorId);
}
