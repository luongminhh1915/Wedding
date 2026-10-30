using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Wedding.Infrastructure.Persistence;

namespace Wedding.Tests;

public class CommissionPolicyPersistenceTests
{
    [Fact]
    public async Task SqliteTestOwnedDatabase_CreatesAllNewCommissionTables()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options;
        await using var db = new ApplicationDbContext(options);

        await db.Database.EnsureCreatedAsync();
        var tables = await db.Database.SqlQueryRaw<string>(
                "SELECT name AS Value FROM sqlite_master WHERE type = 'table' AND name IN ('CommissionPolicyRules', 'CommissionComponentBasisApprovals', 'CommissionAdjustmentRequests')")
            .ToListAsync();

        Assert.Equal(3, tables.Count);
        Assert.Contains("CommissionPolicyRules", tables);
        Assert.Contains("CommissionComponentBasisApprovals", tables);
        Assert.Contains("CommissionAdjustmentRequests", tables);
    }
}
