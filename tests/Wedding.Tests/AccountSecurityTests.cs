using System.IdentityModel.Tokens.Jwt;
using Microsoft.Data.SqlClient;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using Wedding.Application.Common.Interfaces;
using Wedding.Application.Features.Auth.Commands;
using Wedding.Domain.Entities;
using Wedding.Domain.Enums;
using Wedding.Domain.Exceptions;
using Wedding.Infrastructure.Persistence;
using Wedding.Infrastructure.Services;

namespace Wedding.Tests;

public class AccountSecurityTests
{
    private const string OldPassword = "Current passphrase 123";
    private const string NewPassword = "New passphrase 456";

    [Theory]
    [InlineData(UserRole.Customer)] [InlineData(UserRole.VendorOwner)] [InlineData(UserRole.Moderator)]
    [InlineData(UserRole.Finance)] [InlineData(UserRole.CustomerCare)] [InlineData(UserRole.SuperAdmin)]
    public async Task EveryActorCanChangeOnlyItsOwnPasswordAndMustLogInAgain(UserRole role)
    {
        await using var f = await Fixture.Create();
        var user = f.AddUser(role); var other = f.AddUser(UserRole.Customer, "other");
        await f.Db.SaveChangesAsync();
        var oldHash = user.PasswordHash; var otherHash = other.PasswordHash;
        var before = Read(f.Jwt.GenerateToken(user));

        await f.Handler(user.Id).Handle(new(OldPassword, NewPassword, NewPassword), default);

        Assert.NotEqual(oldHash, user.PasswordHash); Assert.Equal(otherHash, other.PasswordHash);
        Assert.Equal(1, user.SecurityVersion); Assert.Equal(0, other.SecurityVersion);
        Assert.True(f.Hasher.Verify(NewPassword, user.PasswordHash)); Assert.False(f.Hasher.Verify(OldPassword, user.PasswordHash));
        Assert.Equal("0", before.Claims.Single(c => c.Type == "security_version").Value);
        var after = Read(f.Jwt.GenerateToken(user));
        Assert.Equal("1", after.Claims.Single(c => c.Type == "security_version").Value);
        Assert.NotEqual(before.Id, after.Id);
        Assert.DoesNotContain(after.Claims, c => c.Type.Contains("password", StringComparison.OrdinalIgnoreCase));
        var audit = Assert.Single(await f.Db.AuditLogs.ToListAsync());
        Assert.Equal("Auth.ChangePassword", audit.Action); Assert.Equal(user.Id, audit.ActorId);
        Assert.Equal(role.ToString(), audit.ActorRole); Assert.Equal(user.Id.ToString(), audit.EntityId); Assert.Null(audit.DataJson);
        var serializedAudit = System.Text.Json.JsonSerializer.Serialize(audit);
        Assert.DoesNotContain(oldHash, serializedAudit); Assert.DoesNotContain(NewPassword, serializedAudit);
        var login = new LoginCommandHandler(f.Db, f.Hasher, f.Jwt);
        await Assert.ThrowsAsync<DomainException>(() => login.Handle(new(user.Email, OldPassword), default));
        Assert.Equal(user.Id, (await login.Handle(new(user.Email, NewPassword), default)).User.Id);
    }

    [Fact]
    public async Task InvalidChangesNeverAlterHashVersionOrWriteAudit()
    {
        await using var f = await Fixture.Create();
        var user = f.AddUser(UserRole.Customer); await f.Db.SaveChangesAsync();
        var oldHash = user.PasswordHash;
        var invalid = new[]
        {
            new ChangePasswordCommand("wrong", NewPassword, NewPassword),
            new ChangePasswordCommand("", NewPassword, NewPassword),
            new ChangePasswordCommand(OldPassword, OldPassword, OldPassword),
            new ChangePasswordCommand(OldPassword, NewPassword, "mismatch"),
            new ChangePasswordCommand(OldPassword, "1234567", "1234567"),
            new ChangePasswordCommand(OldPassword, "        ", "        "),
            new ChangePasswordCommand(OldPassword, new string('a', 73), new string('a', 73)),
            new ChangePasswordCommand(OldPassword, new string('ầ', 25), new string('ầ', 25)),
            new ChangePasswordCommand(OldPassword, null!, null!),
            new ChangePasswordCommand(null!, NewPassword, NewPassword)
        };
        foreach (var command in invalid)
        {
            await Assert.ThrowsAsync<DomainException>(() => f.Handler(user.Id).Handle(command, default));
            // Failed transactions intentionally clear the change tracker; assert the persisted row.
            var persisted = await f.Db.Users.AsNoTracking().SingleAsync();
            Assert.Equal(oldHash, persisted.PasswordHash); Assert.Equal(0, persisted.SecurityVersion);
            Assert.Empty(await f.Db.AuditLogs.ToListAsync());
        }
    }

    [Fact]
    public async Task PasswordPolicyAcceptsExactly72Utf8BytesAndLegacyCurrentPasswords()
    {
        await using var f = await Fixture.Create();
        var user = User.Create("Legacy account", "legacy@example.invalid", "0901234567", f.Hasher.Hash("123456"), UserRole.Customer);
        f.Db.Users.Add(user); await f.Db.SaveChangesAsync();
        var password = new string('ầ', 24);
        await f.Handler(user.Id).Handle(new("123456", password, password), default);
        Assert.True(f.Hasher.Verify(password, (await f.Db.Users.SingleAsync()).PasswordHash));
        Assert.Equal(1, (await f.Db.Users.SingleAsync()).SecurityVersion);
    }

    [Fact]
    public async Task DisabledMissingAndUnauthenticatedAccountsCannotChangePassword()
    {
        await using var f = await Fixture.Create();
        var user = f.AddUser(UserRole.Customer); user.SetStatus(false); await f.Db.SaveChangesAsync();
        foreach (var id in new Guid?[] { null, Guid.NewGuid(), user.Id })
            await Assert.ThrowsAsync<DomainException>(() => f.Handler(id).Handle(new(OldPassword, NewPassword, NewPassword), default));
        Assert.Equal(1, (await f.Db.Users.SingleAsync()).SecurityVersion); Assert.Empty(await f.Db.AuditLogs.ToListAsync());
    }

    [Fact]
    public async Task GoogleOnlyAccountCannotSetPasswordWithoutKnowingCurrentPassword()
    {
        await using var f = await Fixture.Create();
        var user = User.Create("Google account", "google@example.invalid", "0901234567", f.Hasher.Hash(Guid.NewGuid().ToString("N")), UserRole.Customer);
        user.LinkGoogle("google-subject"); f.Db.Users.Add(user); await f.Db.SaveChangesAsync();
        var error = await Assert.ThrowsAsync<DomainException>(() => f.Handler(user.Id).Handle(new("google-subject", NewPassword, NewPassword), default));
        Assert.Contains("Mật khẩu hiện tại không đúng", error.Message);
        Assert.Equal(0, (await f.Db.Users.SingleAsync()).SecurityVersion); Assert.Empty(await f.Db.AuditLogs.ToListAsync());
    }

    [Fact]
    public void SensitiveCommandStringAndDomainPasswordGuardDoNotLeakSecretsOrChangeVersion()
    {
        var command = new ChangePasswordCommand(OldPassword, NewPassword, NewPassword);
        Assert.DoesNotContain(OldPassword, command.ToString()); Assert.DoesNotContain(NewPassword, command.ToString());
        var user = User.Create("Actor", "actor@example.invalid", "0901234567", "original-hash", UserRole.Customer);
        Assert.Throws<DomainException>(() => user.UpdatePassword(" "));
        Assert.Equal("original-hash", user.PasswordHash); Assert.Equal(0, user.SecurityVersion);
        user.UpdatePassword("second-hash"); user.UpdatePassword("third-hash");
        Assert.Equal(2, user.SecurityVersion);
    }

    [Fact]
    public async Task SecurityVersionIsPersistedAndRejectsStalePasswordWriters()
    {
        await using var f = await Fixture.Create();
        var user = f.AddUser(UserRole.Customer); await f.Db.SaveChangesAsync();
        await using var stale = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(f.Connection).Options);
        var staleUser = await stale.Users.SingleAsync();
        await f.Handler(user.Id).Handle(new(OldPassword, NewPassword, NewPassword), default);
        staleUser.UpdatePassword("stale-hash");
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => stale.SaveChangesAsync());
        f.Db.ChangeTracker.Clear(); Assert.True(f.Hasher.Verify(NewPassword, (await f.Db.Users.SingleAsync()).PasswordHash));
    }

    [Fact]
    public async Task DisablingAndReenablingNeverRevivesOldTokensOrAllowsStalePasswordWrites()
    {
        await using var f = await Fixture.Create();
        var user = f.AddUser(UserRole.Customer); await f.Db.SaveChangesAsync();
        var initial = Read(f.Jwt.GenerateToken(user));
        await using var stale = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(f.Connection).Options);
        var staleUser = await stale.Users.SingleAsync();
        user.SetStatus(false); await f.Db.SaveChangesAsync(); Assert.Equal(1, user.SecurityVersion);
        user.SetStatus(false); Assert.Equal(1, user.SecurityVersion);
        user.SetStatus(true); await f.Db.SaveChangesAsync(); Assert.Equal(2, user.SecurityVersion);
        user.SetStatus(true); Assert.Equal(2, user.SecurityVersion);
        Assert.Equal("0", initial.Claims.Single(c => c.Type == "security_version").Value);
        Assert.Equal("2", Read(f.Jwt.GenerateToken(user)).Claims.Single(c => c.Type == "security_version").Value);
        staleUser.UpdatePassword("stale-hash");
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => stale.SaveChangesAsync());
        f.Db.ChangeTracker.Clear(); Assert.True(f.Hasher.Verify(OldPassword, (await f.Db.Users.SingleAsync()).PasswordHash));
    }

    [SqlServerFact]
    public async Task SecurityVersionMigrationPreservesExistingAccountDataAndCanBeReplayed()
    {
        var database = $"WeddingPlanTest_SecurityMigration_{Guid.NewGuid():N}";
        var source = Environment.GetEnvironmentVariable("WEDDING_TEST_SQLSERVER");
        var builder = new SqlConnectionStringBuilder(string.IsNullOrWhiteSpace(source) || source == "1"
            ? "Server=localhost;Integrated Security=true;TrustServerCertificate=true;Connection Timeout=5" : source) { InitialCatalog = database };
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlServer(builder.ConnectionString).Options;
        await using var db = new ApplicationDbContext(options);
        try
        {
            await db.GetService<IMigrator>().MigrateAsync("20261017051915_CustomerOrderLifecycle");
            var id = Guid.NewGuid();
            await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO Users (Id, FullName, Email, PhoneNumber, PasswordHash, Role, IsActive, CreatedAt) VALUES ({id}, {"Existing staff"}, {"preserved@example.invalid"}, {"0901234567"}, {"existing-password-hash"}, {"Finance"}, {true}, {DateTime.UtcNow})");
            await db.Database.MigrateAsync(); await db.Database.MigrateAsync();
            Assert.False(db.Database.HasPendingModelChanges());
            var user = await db.Users.SingleAsync();
            Assert.Equal(id, user.Id); Assert.Equal("existing-password-hash", user.PasswordHash);
            Assert.Equal("preserved@example.invalid", user.Email); Assert.Equal(UserRole.Finance, user.Role);
            Assert.True(user.IsActive); Assert.Equal(0, user.SecurityVersion);
        }
        finally { Assert.StartsWith("WeddingPlanTest_SecurityMigration_", database); await db.Database.EnsureDeletedAsync(); }
    }

    [SqlServerFact]
    public async Task ConcurrentPasswordChangesAuthenticateCurrentHashInsideSerializedTransaction()
    {
        var database = $"WeddingPlanTest_Security_{Guid.NewGuid():N}";
        var source = Environment.GetEnvironmentVariable("WEDDING_TEST_SQLSERVER");
        var builder = new SqlConnectionStringBuilder(string.IsNullOrWhiteSpace(source) || source == "1"
            ? "Server=localhost;Integrated Security=true;TrustServerCertificate=true;Connection Timeout=5" : source) { InitialCatalog = database };
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlServer(builder.ConnectionString).Options;
        await using var setup = new ApplicationDbContext(options);
        try
        {
            await setup.Database.EnsureCreatedAsync();
            var hasher = new FakeHasher(); var user = User.Create("Concurrent", "concurrent@example.invalid", "0901234567", hasher.Hash(OldPassword), UserRole.Customer);
            setup.Users.Add(user); await setup.SaveChangesAsync();
            var results = await Task.WhenAll(Enumerable.Range(0, 4).Select(async index =>
            {
                await using var db = new ApplicationDbContext(options);
                try
                {
                    var password = $"New password number {index}";
                    await new ChangePasswordCommandHandler(db, new Current(user.Id), hasher).Handle(new(OldPassword, password, password), default);
                    return true;
                }
                catch (DomainException) { return false; }
            }));
            Assert.Equal(1, results.Count(result => result)); setup.ChangeTracker.Clear();
            Assert.Equal(1, (await setup.Users.SingleAsync()).SecurityVersion);
            Assert.Single(await setup.AuditLogs.Where(a => a.Action == "Auth.ChangePassword").ToListAsync());
        }
        finally { Assert.StartsWith("WeddingPlanTest_Security_", database); await setup.Database.EnsureDeletedAsync(); }
    }

    private static JwtSecurityToken Read(string token) => new JwtSecurityTokenHandler().ReadJwtToken(token);
    private sealed class Fixture : IAsyncDisposable
    {
        public SqliteConnection Connection { get; }
        public ApplicationDbContext Db { get; }
        public FakeHasher Hasher { get; } = new();
        public JwtProvider Jwt { get; } = new(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        { ["JwtSettings:Secret"] = "Only-a-unit-test-signing-key-with-more-than-32-characters" }).Build());
        private Fixture(SqliteConnection connection, ApplicationDbContext db) { Connection = connection; Db = db; }
        public static async Task<Fixture> Create()
        {
            var connection = new SqliteConnection("Data Source=:memory:"); await connection.OpenAsync();
            var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options);
            await db.Database.EnsureCreatedAsync(); return new(connection, db);
        }
        public User AddUser(UserRole role, string local = "actor")
        {
            var user = User.Create(local, $"{local}@example.invalid", "0901234567", Hasher.Hash(OldPassword), role);
            Db.Users.Add(user); return user;
        }
        public ChangePasswordCommandHandler Handler(Guid? id) => new(Db, new Current(id), Hasher);
        public async ValueTask DisposeAsync() { await Db.DisposeAsync(); await Connection.DisposeAsync(); }
    }
    private sealed class FakeHasher : IPasswordHasher
    {
        public string Hash(string password) => "hash:" + password;
        public bool Verify(string password, string passwordHash) => Hash(password) == passwordHash;
    }
    private sealed record Current(Guid? UserId) : ICurrentUserService
    { public string? Role => null; public string? Email => null; }
}
