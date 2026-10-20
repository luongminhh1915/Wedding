using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage;
using System.Text.RegularExpressions;
using Wedding.Domain.Entities;
using Wedding.Domain.Enums;
using Wedding.Infrastructure.Persistence;

namespace Wedding.Tests;
public class SchemaTests
{
    [SqlServerFact]
    public async Task ActualIdempotentSqlCanCreateAndReplayTheEntireSchemaWithoutLosingUsers()
    {
        var name = $"WeddingPlanTest_Idempotent_{Guid.NewGuid():N}";
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlServer($"Server=localhost;Database={name};Integrated Security=true;TrustServerCertificate=true;Connection Timeout=5").Options;
        await using var db = new ApplicationDbContext(options);
        try
        {
            await db.GetService<IRelationalDatabaseCreator>().CreateAsync();
            var script = db.GetService<IMigrator>().GenerateScript(options: MigrationsSqlGenerationOptions.Idempotent);
            await db.Database.OpenConnectionAsync();
            await db.Database.ExecuteSqlRawAsync("SET XACT_ABORT ON; SET ANSI_NULLS ON; SET QUOTED_IDENTIFIER ON; SET ANSI_PADDING ON; SET ANSI_WARNINGS ON; SET CONCAT_NULL_YIELDS_NULL ON; SET ARITHABORT ON; SET NUMERIC_ROUNDABORT OFF;");
            var batches = Regex.Split(script, @"^\s*GO\s*$", RegexOptions.Multiline | RegexOptions.IgnoreCase).Where(batch => !string.IsNullOrWhiteSpace(batch)).ToArray();
            async Task RunBatches()
            {
                foreach (var batch in batches)
                {
                    // Generated SQL includes literal JSON braces; send it verbatim,
                    // not through EF's composite-format placeholder substitution.
                    await using var command = db.Database.GetDbConnection().CreateCommand();
                    command.CommandText = batch;
                    await command.ExecuteNonQueryAsync();
                }
            }
            await RunBatches();
            var user = User.Create("Retained SQL script customer", "script-preservation@example.invalid", "0900000000", "original-password-hash", UserRole.Customer);
            db.Users.Add(user); await db.SaveChangesAsync();
            await RunBatches();
            db.ChangeTracker.Clear();
            Assert.Equal("original-password-hash", (await db.Users.SingleAsync()).PasswordHash);
            Assert.Equal(8, await db.Categories.CountAsync());
            Assert.Equal(5, await db.BusinessGroups.CountAsync());
            Assert.Equal(16, await db.InvitationTemplates.CountAsync());
            Assert.Empty(await db.Database.GetPendingMigrationsAsync());
        }
        finally
        {
            await db.Database.CloseConnectionAsync();
            Assert.StartsWith("WeddingPlanTest_Idempotent_", name);
            await db.Database.EnsureDeletedAsync();
        }
    }

    [SqlServerFact]
    public async Task SqlServerMigrationsPreserveExistingUsersAndAreRepeatable()
    {
        var name = $"WeddingPlanTest_Migration_{Guid.NewGuid():N}";
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlServer($"Server=localhost;Database={name};Integrated Security=true;TrustServerCertificate=true;Connection Timeout=5").Options;
        await using var db = new ApplicationDbContext(options);
        try
        {
            var migrator = db.GetService<Microsoft.EntityFrameworkCore.Migrations.IMigrator>();
            await migrator.MigrateAsync("20261016104748_WeddyJourneyP0toP6");
            var id = Guid.NewGuid();
            await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO Users (Id, FullName, Email, PhoneNumber, PasswordHash, Role, IsActive, CreatedAt) VALUES ({id}, {"Existing customer"}, {"existing@test.vn"}, {"0900000000"}, {"unchanged-hash"}, {"Customer"}, {true}, {DateTime.UtcNow})");
            var profileId = Guid.NewGuid();
            var createdAt = DateTime.UtcNow.AddDays(-10);
            await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO WeddingProfiles (Id, CustomerId, GroomName, BrideName, City, TotalBudget, GuestCount, Style, CreatedAt) VALUES ({profileId}, {id}, {"Existing groom"}, {"Existing bride"}, {"Hà Nội"}, {300000000m}, {200}, {"Classic"}, {createdAt})");
            await db.Database.MigrateAsync(); await db.Database.MigrateAsync();
            Assert.False(db.Database.HasPendingModelChanges());
            var user = await db.Users.SingleAsync(u => u.Id == id); Assert.Equal("unchanged-hash", user.PasswordHash);
            var profile = await db.WeddingProfiles.SingleAsync(p => p.Id == profileId);
            Assert.Equal(createdAt, profile.OnboardingCompletedAt);
            Assert.Equal(300000000m, profile.TotalBudget);
            Assert.Null(WeddingProfile.Create(Guid.NewGuid()).OnboardingCompletedAt);
            Assert.Equal(16, await db.InvitationTemplates.CountAsync());
            var audit = AuditLog.Create(null, "System", "Test", "Schema", "1"); db.AuditLogs.Add(audit); await db.SaveChangesAsync();
            await Assert.ThrowsAsync<SqlException>(() => db.Database.ExecuteSqlRawAsync("DELETE FROM AuditLogs"));
        }
        finally { Assert.StartsWith("WeddingPlanTest_Migration_", name); await db.Database.EnsureDeletedAsync(); }
    }
}
