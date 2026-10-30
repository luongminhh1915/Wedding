using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Wedding.Application.Common.Interfaces;
using Wedding.Domain.Entities;
using Wedding.Domain.Enums;
using Wedding.Domain.Exceptions;
using Wedding.Infrastructure.Persistence;
using Wedding.Infrastructure.Services;

namespace Wedding.Tests;

public sealed class FirstAdministratorBootstrapTests
{
    private const string Password = "StudyAdmin-Strong-2026";
    private static FirstAdministratorInput Input => new("First Administrator", " ADMIN@Example.Invalid ", "0901234567", Password, Password);

    [Fact]
    public async Task CreatesOnlyFirstAdministratorAndAddsRedactedAuditAtomically()
    {
        await using var fixture = await Fixture.CreateAsync();
        var input = Input;
        var id = await fixture.Service.CreateAsync(input);
        var administrator = await fixture.Db.Users.SingleAsync();
        Assert.Equal(id, administrator.Id);
        Assert.Equal(UserRole.SuperAdmin, administrator.Role);
        Assert.Equal("admin@example.invalid", administrator.Email);
        Assert.True(administrator.IsActive);
        Assert.NotEqual(Password, administrator.PasswordHash);
        Assert.True(fixture.Hasher.Verify(Password, administrator.PasswordHash));
        var audit = await fixture.Db.AuditLogs.SingleAsync();
        Assert.Equal("Deployment.FirstAdministratorCreated", audit.Action);
        Assert.Null(audit.ActorId);
        Assert.Equal("DeploymentTool", audit.ActorRole);
        Assert.Equal(id.ToString(), audit.EntityId);
        Assert.DoesNotContain(Password, audit.DataJson);
        Assert.DoesNotContain(administrator.PasswordHash, audit.DataJson);
        Assert.DoesNotContain(administrator.Email, audit.DataJson);
        Assert.DoesNotContain(administrator.PhoneNumber, audit.DataJson);
        Assert.DoesNotContain(administrator.FullName, audit.DataJson);
        Assert.DoesNotContain(Password, input.ToString());
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ExistingActiveOrLockedAdministratorPreventsAnotherAndIsNeverReset(bool isActive)
    {
        await using var fixture = await Fixture.CreateAsync();
        var administrator = User.Create("Existing Administrator", "other@example.invalid", "0901234567", "original-hash", UserRole.SuperAdmin);
        administrator.SetStatus(isActive);
        fixture.Db.Users.Add(administrator); await fixture.Db.SaveChangesAsync();
        await Assert.ThrowsAsync<DomainException>(() => fixture.Service.CreateAsync(Input));
        Assert.Single(await fixture.Db.Users.ToListAsync());
        Assert.Equal("original-hash", administrator.PasswordHash);
        Assert.Equal(isActive, administrator.IsActive);
        Assert.Empty(await fixture.Db.AuditLogs.ToListAsync());
    }

    [Fact]
    public async Task ExistingCustomerEmailIsNeverPromotedOrReassigned()
    {
        await using var fixture = await Fixture.CreateAsync();
        var customer = User.Create("Original Customer", "admin@example.invalid", "0901111111", "customer-hash", UserRole.Customer);
        fixture.Db.Users.Add(customer); await fixture.Db.SaveChangesAsync();
        await Assert.ThrowsAsync<DomainException>(() => fixture.Service.CreateAsync(Input));
        Assert.Equal(UserRole.Customer, customer.Role);
        Assert.Equal("Original Customer", customer.FullName);
        Assert.Equal("0901111111", customer.PhoneNumber);
        Assert.Equal("customer-hash", customer.PasswordHash);
        Assert.Single(await fixture.Db.Users.ToListAsync());
        Assert.Empty(await fixture.Db.AuditLogs.ToListAsync());
    }

    [Fact]
    public async Task SecondCallCannotCreateAnotherOrResetTheFirst()
    {
        await using var fixture = await Fixture.CreateAsync();
        var id = await fixture.Service.CreateAsync(Input);
        await Assert.ThrowsAsync<DomainException>(() => fixture.Service.CreateAsync(Input with { Email = "second@example.invalid" }));
        Assert.Equal(id, (await fixture.Db.Users.SingleAsync()).Id);
        Assert.Single(await fixture.Db.AuditLogs.ToListAsync());
    }

    [Theory]
    [InlineData("")]
    [InlineData("123456")]
    [InlineData("12345678901")]
    [InlineData("            ")]
    public async Task InvalidPasswordNeverReachesDatabaseWrites(string password)
    {
        await using var fixture = await Fixture.CreateAsync();
        await Assert.ThrowsAsync<DomainException>(() => fixture.Service.CreateAsync(Input with { Password = password, ConfirmPassword = password }));
        Assert.Equal(0, fixture.Hasher.HashCalls);
        Assert.Empty(await fixture.Db.Users.ToListAsync());
        Assert.Empty(await fixture.Db.AuditLogs.ToListAsync());
    }

    [Fact]
    public async Task Utf8ByteLimitConfirmationAndCommonAccountRulesAreEnforced()
    {
        await using var fixture = await Fixture.CreateAsync();
        var oversized = new string('ấ', 25);
        var invalid = new[]
        {
            Input with { Password = oversized, ConfirmPassword = oversized },
            Input with { Password = new string('x', 73), ConfirmPassword = new string('x', 73) },
            Input with { ConfirmPassword = "Different-Password-2026" },
            Input with { FullName = " " },
            Input with { FullName = new string('n', 151) },
            Input with { Email = "not-an-email" },
            Input with { PhoneNumber = "+84901234567" },
        };
        foreach (var input in invalid) await Assert.ThrowsAsync<DomainException>(() => fixture.Service.CreateAsync(input));
        Assert.Equal(0, fixture.Hasher.HashCalls);
        Assert.Empty(await fixture.Db.Users.ToListAsync());
        Assert.Empty(await fixture.Db.AuditLogs.ToListAsync());
    }

    private sealed class TestHasher : IPasswordHasher
    {
        public int HashCalls { get; private set; }
        public string Hash(string password) { HashCalls++; return "test-only-hash:" + password; }
        public bool Verify(string password, string passwordHash) => passwordHash == "test-only-hash:" + password;
    }

    private sealed class Fixture(SqliteConnection connection, ApplicationDbContext db) : IAsyncDisposable
    {
        public ApplicationDbContext Db { get; } = db;
        public TestHasher Hasher { get; } = new();
        public FirstAdministratorBootstrapper Service => new(Db, Hasher);
        public static async Task<Fixture> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:"); await connection.OpenAsync();
            var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options);
            await db.Database.EnsureCreatedAsync();
            return new(connection, db);
        }
        public async ValueTask DisposeAsync() { await Db.DisposeAsync(); await connection.DisposeAsync(); }
    }
}
