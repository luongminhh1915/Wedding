using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Wedding.Domain.Entities;
using Wedding.Domain.Enums;
using Wedding.Infrastructure.Persistence;

namespace Wedding.Tests;
public class SchemaTests
{
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
            await db.Database.MigrateAsync(); await db.Database.MigrateAsync();
            var user = await db.Users.SingleAsync(u => u.Id == id); Assert.Equal("unchanged-hash", user.PasswordHash);
            Assert.Equal(16, await db.InvitationTemplates.CountAsync());
            var audit = AuditLog.Create(null, "System", "Test", "Schema", "1"); db.AuditLogs.Add(audit); await db.SaveChangesAsync();
            await Assert.ThrowsAsync<SqlException>(() => db.Database.ExecuteSqlRawAsync("DELETE FROM AuditLogs"));
        }
        finally { Assert.StartsWith("WeddingPlanTest_Migration_", name); await db.Database.EnsureDeletedAsync(); }
    }
}
