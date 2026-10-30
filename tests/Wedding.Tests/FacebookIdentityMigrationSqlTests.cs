using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using System.Text.RegularExpressions;
using Wedding.Domain.Enums;
using Wedding.Domain.Entities;
using Wedding.Infrastructure.Persistence;
using Xunit.Abstractions;

namespace Wedding.Tests;

public sealed class FacebookIdentityMigrationSqlTests(ITestOutputHelper output)
{
    private const string PreviousMigration = "20261022124728_PasswordRecoveryTokens";
    private const string FacebookMigration = "20261026083936_FacebookCustomerIdentity";
    private const string OptionalContactMigration = "20261027152038_OptionalSocialContactEmail";
    private const string DatabasePrefix = "WeddingPlanTest_FacebookIdentity_";

    [SqlServerFact]
    public async Task UpgradeFromPasswordRecoveryPreservesUsersAndGoogleLinksAndCanReplayWithFacebookLinks()
    {
        var database = DatabasePrefix + Guid.NewGuid().ToString("N");
        // WEDDING_TEST_SQLSERVER only opts this test in. Its value is never used as
        // a connection string, and application/private configuration is not loaded.
        var target = new SqlConnectionStringBuilder
        {
            DataSource = "localhost", InitialCatalog = database, IntegratedSecurity = true,
            Encrypt = true, TrustServerCertificate = true, Pooling = false, ConnectTimeout = 5
        };
        AssertOwnedTarget(target, database);
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlServer(target.ConnectionString).Options;
        var created = false;
        try
        {
            await CreateOwnedDatabaseAsync(target, database);
            created = true;
            output.WriteLine($"Created owned localhost test database: {database}");
            await using (var db = new ApplicationDbContext(options))
            {
                var migrator = db.GetService<IMigrator>();
                await migrator.MigrateAsync(PreviousMigration);
                var before = (await db.Database.GetAppliedMigrationsAsync()).ToArray();
                Assert.Contains(PreviousMigration, before);
                Assert.DoesNotContain(FacebookMigration, before);

                var id = Guid.NewGuid();
                var otherId = Guid.NewGuid();
                var profileId = Guid.NewGuid();
                var createdAt = new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc);
                var updatedAt = createdAt.AddDays(1);
                await db.Database.ExecuteSqlInterpolatedAsync($"""
                    INSERT INTO [Users] ([Id],[FullName],[Email],[PhoneNumber],[PasswordHash],[Role],[IsActive],
                        [CreatedAt],[UpdatedAt],[SecurityVersion],[AvatarUrl],[GoogleSubject])
                    VALUES ({id},{"Preserved synthetic customer"},{"preserved-facebook-migration@example.invalid"},
                        {"0900000000"},{"synthetic-unchanged-password-hash"},{"Customer"},{true},
                        {createdAt},{updatedAt},{7},{"https://example.invalid/avatar.png"},{"preserved-google-subject"});
                    INSERT INTO [Users] ([Id],[FullName],[Email],[PhoneNumber],[PasswordHash],[Role],[IsActive],[CreatedAt])
                    VALUES ({otherId},{"Second synthetic customer"},{"second-facebook-migration@example.invalid"},
                        {"0900000001"},{"second-synthetic-password-hash"},{"Customer"},{true},{createdAt});
                    INSERT INTO [WeddingProfiles] ([Id],[CustomerId],[GroomName],[BrideName],[City],
                        [TotalBudget],[GuestCount],[Style],[CreatedAt],[OnboardingCompletedAt])
                    VALUES ({profileId},{id},{"Synthetic groom"},{"Synthetic bride"},{"Hà Nội"},
                        {123000000m},{50},{"Classic"},{createdAt},{updatedAt});
                    """);

                await db.Database.OpenConnectionAsync();
                Assert.Equal(0, await ScalarIntAsync(db,
                    "SELECT COUNT(*) FROM sys.columns WHERE [object_id]=OBJECT_ID(N'[dbo].[Users]') AND [name]=N'FacebookSubject';"));
                var script = migrator.GenerateScript(PreviousMigration, FacebookMigration, MigrationsSqlGenerationOptions.Idempotent);
                Assert.Contains("IF NOT EXISTS", script);
                await RunScriptAsync(db, script);

                // This fixture deliberately stops at migration19. Project only columns
                // present there instead of materializing the latest User mapping.
                Assert.Equal(0, await ScalarIntAsync(db, "SELECT COUNT(*) FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.Users') AND name=N'LoginName';"));
                var retained = await HistoricalUserAsync(db, id);
                Assert.Equal("Preserved synthetic customer", retained.FullName);
                Assert.Equal("preserved-facebook-migration@example.invalid", retained.Email);
                Assert.Equal("0900000000", retained.PhoneNumber);
                Assert.Equal("synthetic-unchanged-password-hash", retained.PasswordHash);
                Assert.Equal(UserRole.Customer, retained.Role);
                Assert.True(retained.IsActive);
                Assert.Equal(createdAt, retained.CreatedAt);
                Assert.Equal(updatedAt, retained.UpdatedAt);
                Assert.Equal(7, retained.SecurityVersion);
                Assert.Equal("https://example.invalid/avatar.png", retained.AvatarUrl);
                Assert.Equal("preserved-google-subject", retained.GoogleSubject);
                Assert.Null(retained.FacebookSubject);
                var profile = await db.WeddingProfiles.AsNoTracking().SingleAsync(value => value.Id == profileId);
                Assert.Equal(id, profile.CustomerId);
                Assert.Equal("Synthetic groom", profile.GroomName);
                Assert.Equal("Synthetic bride", profile.BrideName);
                Assert.Equal("Hà Nội", profile.City);
                Assert.Equal(123000000m, profile.TotalBudget);
                Assert.Equal(50, profile.GuestCount);
                Assert.Equal("Classic", profile.Style);
                Assert.Equal(createdAt, profile.CreatedAt);
                Assert.Equal(updatedAt, profile.OnboardingCompletedAt);
                // Two existing null identities must coexist under the filtered index.
                Assert.Equal(2, await db.Users.CountAsync(user => user.FacebookSubject == null));
                Assert.Equal(1, await ScalarIntAsync(db, """
                    SELECT COUNT(*) FROM sys.columns AS c JOIN sys.types AS t ON t.user_type_id=c.user_type_id
                    WHERE c.[object_id]=OBJECT_ID(N'[dbo].[Users]') AND c.[name]=N'FacebookSubject'
                        AND c.is_nullable=1 AND t.[name]=N'nvarchar' AND c.max_length=510;
                    """));
                Assert.Equal(1, await ScalarIntAsync(db, """
                    SELECT COUNT(*) FROM sys.indexes AS i
                    JOIN sys.index_columns AS ic ON ic.object_id=i.object_id AND ic.index_id=i.index_id
                    JOIN sys.columns AS c ON c.object_id=ic.object_id AND c.column_id=ic.column_id
                    WHERE i.object_id=OBJECT_ID(N'[dbo].[Users]') AND i.[name]=N'IX_Users_FacebookSubject'
                        AND i.is_unique=1 AND i.has_filter=1 AND c.[name]=N'FacebookSubject';
                    """));
                await using (var filterCommand = db.Database.GetDbConnection().CreateCommand())
                {
                    filterCommand.CommandText = "SELECT filter_definition FROM sys.indexes WHERE object_id=OBJECT_ID(N'[dbo].[Users]') AND [name]=N'IX_Users_FacebookSubject';";
                    var filter = Assert.IsType<string>(await filterCommand.ExecuteScalarAsync());
                    Assert.Equal("[FACEBOOKSUBJECT]ISNOTNULL", Regex.Replace(filter, @"[\s()]", "").ToUpperInvariant());
                }

                const string subject = "synthetic-facebook-subject";
                await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE [Users] SET [FacebookSubject]={subject} WHERE [Id]={id};");
                var duplicate = await Assert.ThrowsAsync<SqlException>(() =>
                    db.Database.ExecuteSqlInterpolatedAsync($"UPDATE [Users] SET [FacebookSubject]={subject} WHERE [Id]={otherId};"));
                Assert.True(duplicate.Number is 2601 or 2627, $"Expected a SQL uniqueness violation, received {duplicate.Number}.");

                // The second application must neither duplicate migration history nor
                // overwrite the newly linked identity or the original account data.
                await RunScriptAsync(db, script);
                var applied = (await db.Database.GetAppliedMigrationsAsync()).ToArray();
                Assert.Equal(before.Length + 1, applied.Length);
                Assert.Single(applied, migration => migration == FacebookMigration);
                var replayed = await HistoricalUserAsync(db, id);
                Assert.Equal(subject, replayed.FacebookSubject);
                Assert.Equal(retained.FullName, replayed.FullName);
                Assert.Equal(retained.Email, replayed.Email);
                Assert.Equal(retained.PhoneNumber, replayed.PhoneNumber);
                Assert.Equal(retained.PasswordHash, replayed.PasswordHash);
                Assert.Equal(retained.Role, replayed.Role);
                Assert.Equal(retained.IsActive, replayed.IsActive);
                Assert.Equal(retained.CreatedAt, replayed.CreatedAt);
                Assert.Equal(retained.UpdatedAt, replayed.UpdatedAt);
                Assert.Equal(retained.SecurityVersion, replayed.SecurityVersion);
                Assert.Equal(retained.AvatarUrl, replayed.AvatarUrl);
                Assert.Equal(retained.GoogleSubject, replayed.GoogleSubject);
                Assert.Null((await HistoricalUserAsync(db, otherId)).FacebookSubject);
                var replayedProfile = await db.WeddingProfiles.AsNoTracking().SingleAsync(value => value.Id == profileId);
                Assert.Equal(profile.CustomerId, replayedProfile.CustomerId);
                Assert.Equal(profile.GroomName, replayedProfile.GroomName);
                Assert.Equal(profile.BrideName, replayedProfile.BrideName);
                Assert.Equal(profile.City, replayedProfile.City);
                Assert.Equal(profile.TotalBudget, replayedProfile.TotalBudget);
                Assert.Equal(profile.GuestCount, replayedProfile.GuestCount);
                Assert.Equal(profile.Style, replayedProfile.Style);
                Assert.Equal(profile.CreatedAt, replayedProfile.CreatedAt);
                Assert.Equal(profile.OnboardingCompletedAt, replayedProfile.OnboardingCompletedAt);
            }
        }
        finally
        {
            // Only drop after our successful CREATE, after all context connections
            // have been disposed; never accept an environment-supplied target.
            if (created)
            {
                await DropOwnedDatabaseAsync(target, database);
                output.WriteLine($"Removed owned localhost test database: {database}");
            }
        }
    }

    [SqlServerFact]
    public async Task OptionalEmailMigrationPreservesOldAccountsAndSupportsDistinctSocialOnlyCustomers()
    {
        var database = DatabasePrefix + Guid.NewGuid().ToString("N");
        var target = new SqlConnectionStringBuilder
        {
            DataSource = "localhost", InitialCatalog = database, IntegratedSecurity = true,
            Encrypt = true, TrustServerCertificate = true, Pooling = false, ConnectTimeout = 5
        };
        var created = false;
        try
        {
            await CreateOwnedDatabaseAsync(target, database); created = true;
            var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlServer(target.ConnectionString).Options;
            await using var db = new ApplicationDbContext(options);
            var migrator = db.GetService<IMigrator>();
            await migrator.MigrateAsync(FacebookMigration);
            var original = User.Create("Preserved customer", "preserved-contact@example.invalid", "0900000000", "preserved-hash", UserRole.Customer);
            original.LinkGoogle("preserved-google-identity");
            // Seed only migration19 columns; do not add migration21 merely to make
            // the current model's optional LoginName property available to this test.
            await InsertHistoricalUserAsync(db, original);
            await db.Database.OpenConnectionAsync();
            var script = migrator.GenerateScript(FacebookMigration, OptionalContactMigration, MigrationsSqlGenerationOptions.Idempotent);
            await RunScriptAsync(db, script);
            db.ChangeTracker.Clear();
            var retained = await HistoricalUserAsync(db, original.Id);
            Assert.Equal(original.Email, retained.Email);
            Assert.Equal(original.PasswordHash, retained.PasswordHash);
            Assert.Equal(original.GoogleSubject, retained.GoogleSubject);
            Assert.Equal(original.CreatedAt, retained.CreatedAt);
            Assert.Equal(1, await ScalarIntAsync(db, "SELECT is_nullable FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.Users') AND name=N'Email';"));
            Assert.Equal(0, await ScalarIntAsync(db, "SELECT COUNT(*) FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.Users') AND name=N'LoginName';"));

            var facebook = User.CreateSocialCustomer("Facebook customer", "random-inaccessible-hash", facebookSubject: "fb-without-email");
            var google = User.CreateSocialCustomer("Google customer", "other-random-hash", googleSubject: "google-without-contact");
            await InsertHistoricalUserAsync(db, facebook);
            await InsertHistoricalUserAsync(db, google);
            db.ChangeTracker.Clear();
            Assert.Equal(2, await ScalarIntAsync(db, "SELECT COUNT(*) FROM dbo.Users WHERE Email IS NULL;"));
            Assert.Empty((await HistoricalUserAsync(db, facebook.Id)).Email ?? "");
            var jwtConfig = new Microsoft.Extensions.Configuration.ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>
            {
                ["JwtSettings:Secret"] = new string('x', 64)
            }).Build();
            var token = new Wedding.Infrastructure.Services.JwtProvider(jwtConfig).GenerateToken(facebook);
            var decoded = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler().ReadJwtToken(token);
            Assert.DoesNotContain(decoded.Claims, claim => claim.Type == "email" || claim.Type == System.Security.Claims.ClaimTypes.Email);
            // FindAsync in the real sender uses this already-known historical user.
            // Only Notification is inserted; no current-model User query/write occurs.
            db.Attach(facebook);
            var sender = new Wedding.Infrastructure.Services.DurableNotificationSender(db);
            await sender.SendAsync("Email", facebook.NotificationRecipient, "Private synthetic reminder");
            Assert.Equal("InApp", (await db.Notifications.SingleAsync()).Channel);

            await Assert.ThrowsAsync<SqlException>(() => db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.Users SET Role={"Finance"} WHERE Id={facebook.Id};"));
            await Assert.ThrowsAsync<SqlException>(() => db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.Users SET Email={original.Email} WHERE Id={facebook.Id};"));
            await RunScriptAsync(db, script);
            Assert.Equal(3, await db.Users.CountAsync());
            Assert.Equal(0, await ScalarIntAsync(db, "SELECT COUNT(*) FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.Users') AND name=N'LoginName';"));
            Assert.Equal(1, await ScalarIntAsync(db, $"SELECT COUNT(*) FROM dbo.__EFMigrationsHistory WHERE MigrationId='{OptionalContactMigration}';"));
            var down = migrator.GenerateScript(OptionalContactMigration, FacebookMigration);
            Assert.Contains("Cannot roll back optional contact email", down);
        }
        finally { if (created) await DropOwnedDatabaseAsync(target, database); }
    }

    private sealed record HistoricalUser(Guid Id, string FullName, string? Email, string PhoneNumber,
        string PasswordHash, UserRole Role, bool IsActive, DateTime CreatedAt, DateTime? UpdatedAt,
        int SecurityVersion, string? AvatarUrl, string? GoogleSubject, string? FacebookSubject);

    private static Task<HistoricalUser> HistoricalUserAsync(ApplicationDbContext db, Guid id)
        => db.Users.AsNoTracking().Where(user => user.Id == id).Select(user => new HistoricalUser(
            user.Id, user.FullName, user.Email, user.PhoneNumber, user.PasswordHash, user.Role,
            user.IsActive, user.CreatedAt, user.UpdatedAt, user.SecurityVersion, user.AvatarUrl,
            user.GoogleSubject, user.FacebookSubject)).SingleAsync();

    private static Task<int> InsertHistoricalUserAsync(ApplicationDbContext db, User user)
    {
        string? email = string.IsNullOrWhiteSpace(user.Email) ? null : user.Email;
        return db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO [Users] ([Id],[FullName],[Email],[PhoneNumber],[PasswordHash],[Role],[IsActive],
                [CreatedAt],[UpdatedAt],[SecurityVersion],[AvatarUrl],[GoogleSubject],[FacebookSubject])
            VALUES ({user.Id},{user.FullName},{email},{user.PhoneNumber},{user.PasswordHash},{user.Role.ToString()},
                {user.IsActive},{user.CreatedAt},{user.UpdatedAt},{user.SecurityVersion},{user.AvatarUrl},
                {user.GoogleSubject},{user.FacebookSubject});
            """);
    }

    private static void AssertOwnedTarget(SqlConnectionStringBuilder target, string database)
    {
        Assert.Equal("localhost", target.DataSource);
        Assert.Equal(database, target.InitialCatalog);
        Assert.True(target.IntegratedSecurity);
        Assert.False(target.Pooling);
        Assert.Matches("^" + DatabasePrefix + "[a-f0-9]{32}$", database);
    }

    private static async Task CreateOwnedDatabaseAsync(SqlConnectionStringBuilder target, string database)
    {
        AssertOwnedTarget(target, database);
        await using var connection = new SqlConnection(new SqlConnectionStringBuilder(target.ConnectionString) { InitialCatalog = "master" }.ConnectionString);
        await connection.OpenAsync();
        await using var exists = connection.CreateCommand();
        exists.CommandText = "SELECT DB_ID(@name);";
        exists.Parameters.AddWithValue("@name", database);
        Assert.Equal(DBNull.Value, await exists.ExecuteScalarAsync());
        await using var create = connection.CreateCommand();
        create.CommandText = $"CREATE DATABASE [{database}];";
        await create.ExecuteNonQueryAsync();
    }

    private static async Task DropOwnedDatabaseAsync(SqlConnectionStringBuilder target, string database)
    {
        AssertOwnedTarget(target, database);
        await using var connection = new SqlConnection(new SqlConnectionStringBuilder(target.ConnectionString) { InitialCatalog = "master" }.ConnectionString);
        await connection.OpenAsync();
        await using var drop = connection.CreateCommand();
        drop.CommandText = $"DROP DATABASE [{database}];";
        await drop.ExecuteNonQueryAsync();
        await using var exists = connection.CreateCommand();
        exists.CommandText = "SELECT DB_ID(@name);";
        exists.Parameters.AddWithValue("@name", database);
        Assert.Equal(DBNull.Value, await exists.ExecuteScalarAsync());
    }

    private static async Task RunScriptAsync(ApplicationDbContext db, string script)
    {
        await db.Database.ExecuteSqlRawAsync("SET XACT_ABORT ON; SET ANSI_NULLS ON; SET QUOTED_IDENTIFIER ON; SET ANSI_PADDING ON; SET ANSI_WARNINGS ON; SET CONCAT_NULL_YIELDS_NULL ON; SET ARITHABORT ON; SET NUMERIC_ROUNDABORT OFF;");
        foreach (var batch in Regex.Split(script, @"^\s*GO\s*$", RegexOptions.Multiline | RegexOptions.IgnoreCase).Where(batch => !string.IsNullOrWhiteSpace(batch)))
        {
            await using var command = db.Database.GetDbConnection().CreateCommand();
            command.CommandText = batch;
            await command.ExecuteNonQueryAsync();
        }
    }

    private static async Task<int> ScalarIntAsync(ApplicationDbContext db, string sql)
    {
        await using var command = db.Database.GetDbConnection().CreateCommand();
        command.CommandText = sql;
        return Convert.ToInt32(await command.ExecuteScalarAsync());
    }
}
