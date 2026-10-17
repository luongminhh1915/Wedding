using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.Data.SqlClient;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Wedding.Application.Common.Interfaces;
using Wedding.Application.Features.Auth.Commands;
using Wedding.Application.Features.Users.Commands.CreateStaffAccount;
using Wedding.Application.Features.Users.Commands.UpdateUserStatus;
using Wedding.Domain.Entities;
using Wedding.Domain.Enums;
using Wedding.Domain.Exceptions;
using Wedding.Infrastructure.Persistence;
using Wedding.Infrastructure.Services;

namespace Wedding.Tests;

public class AccountAuthTests
{
    private const string Password = "A long test passphrase";

    [Fact]
    public async Task CustomerRegistrationNormalizesInputAndLoginReturnsPersistedActor()
    {
        await using var f = await Fixture.Create();
        var registered = await f.Customer.Handle(new("  Cặp đôi kiểm thử  ", " Couple@Example.invalid ", " 0901234567 ", Password), default);
        Assert.Equal("Customer", registered.User.Role); Assert.Null(registered.User.VendorId);
        Assert.Equal("couple@example.invalid", registered.User.Email); Assert.Equal("Cặp đôi kiểm thử", registered.User.FullName);
        Assert.Equal("0901234567", registered.User.PhoneNumber);
        var login = await f.Login.Handle(new(" COUPLE@EXAMPLE.INVALID ", Password), default);
        Assert.Equal(registered.User.Id, login.User.Id); Assert.NotNull((await f.Db.Users.SingleAsync()).LastLoginAt);
        await Assert.ThrowsAsync<DomainException>(() => f.Vendor.Handle(new("Other", "couple@example.invalid", "0901234568", Password, "Studio", "HCM"), default));
        Assert.Single(await f.Db.Users.ToListAsync()); Assert.Empty(await f.Db.Vendors.ToListAsync());
    }

    [Fact]
    public async Task VendorRegistrationCreatesAtomicOneToOneProfileAndUniqueVietnameseSlug()
    {
        await using var f = await Fixture.Create();
        var first = await f.Vendor.Handle(new(" Đại diện ", "OWNER@EXAMPLE.INVALID", "0901234567", Password, " Đám Cưới Đẹp ", " HCM ", .12m), default);
        var second = await f.Vendor.Handle(new("Owner two", "second@example.invalid", "0901234568", Password, "Đám Cưới Đẹp", "HCM", .03m), default);
        Assert.Equal("VendorOwner", first.User.Role); Assert.NotNull(first.User.VendorId);
        Assert.Equal(first.User.VendorId, (await f.Login.Handle(new(first.User.Email, Password), default)).User.VendorId);
        Assert.Equal("dam-cuoi-dep", (await f.Db.Vendors.SingleAsync(v => v.Id == first.User.VendorId)).Slug);
        Assert.Equal("dam-cuoi-dep-1", (await f.Db.Vendors.SingleAsync(v => v.Id == second.User.VendorId)).Slug);
        Assert.All(await f.Db.Vendors.ToListAsync(), vendor => Assert.False(vendor.IsVerified));
        Assert.Equal(.12m, (await f.Db.Vendors.SingleAsync(v => v.Id == first.User.VendorId)).CommissionRate);
        await Assert.ThrowsAsync<DomainException>(() => f.Vendor.Handle(new("Owner", "bad@example.invalid", "0901234567", Password, "Bad rate", "HCM", .15m), default));
        await Assert.ThrowsAsync<DomainException>(() => f.Vendor.Handle(new("Owner", "bad2@example.invalid", "0901234567", Password, "Bad rate", "HCM", .04m), default));
        Assert.Equal(2, await f.Db.Users.CountAsync()); Assert.Equal(2, await f.Db.Vendors.CountAsync());
    }

    [Fact]
    public async Task RegistrationValidationRunsInHandlerAndRejectsSchemaOverflowOrBcryptTruncation()
    {
        await using var f = await Fixture.Create();
        var valid = new RegisterCustomerCommand("Name", "new@example.invalid", "0901234567", Password);
        var invalid = new[]
        {
            valid with { FullName = "  " }, valid with { FullName = new string('a', 151) },
            valid with { Email = "Full Name <new@example.invalid>" }, valid with { Email = new string('a', 145) + "@example.invalid" },
            valid with { PhoneNumber = "0123" }, valid with { Password = "1234567" },
            valid with { Password = new string('a', 73) }, valid with { Password = new string('ầ', 25) },
            valid with { Password = "        " }, valid with { PhoneNumber = "0١٢٣٤٥٦٧٨٩" }
        };
        foreach (var command in invalid) await Assert.ThrowsAsync<DomainException>(() => f.Customer.Handle(command, default));
        Assert.Empty(await f.Db.Users.ToListAsync());
        await f.Customer.Handle(valid with { Password = new string('ầ', 24) }, default);
        Assert.Single(await f.Db.Users.ToListAsync());
    }

    [Theory]
    [InlineData(UserRole.Customer)] [InlineData(UserRole.VendorOwner)] [InlineData(UserRole.Moderator)]
    [InlineData(UserRole.Finance)] [InlineData(UserRole.CustomerCare)] [InlineData(UserRole.SuperAdmin)]
    public async Task LoginAuthenticatesEveryActorButCannotChooseOrChangeRole(UserRole role)
    {
        await using var f = await Fixture.Create();
        var user = User.Create("Actor", "actor@example.invalid", "0901234567", f.Hasher.Hash("legacy"), role);
        f.Db.Users.Add(user); await f.Db.SaveChangesAsync();
        var login = await f.Login.Handle(new(user.Email, "legacy"), default);
        Assert.Equal(role.ToString(), login.User.Role); Assert.Equal(user.Id, login.User.Id);
        await Assert.ThrowsAsync<DomainException>(() => f.Login.Handle(new(user.Email, "incorrect"), default));
        user.SetStatus(false); await f.Db.SaveChangesAsync();
        await Assert.ThrowsAsync<DomainException>(() => f.Login.Handle(new(user.Email, "legacy"), default));
    }

    [Theory]
    [InlineData("Moderator")] [InlineData("Finance")] [InlineData("CustomerCare")]
    public async Task OnlySuperAdminCanProvisionInternalActorsWithoutReplacingItsSession(string role)
    {
        await using var f = await Fixture.Create();
        var admin = f.AddUser(UserRole.SuperAdmin, "admin"); await f.Db.SaveChangesAsync();
        var handler = new CreateStaffAccountCommandHandler(f.Db, new Current(admin.Id, "SuperAdmin"), f.Hasher);
        var result = await handler.Handle(new(" New staff ", " STAFF@EXAMPLE.INVALID ", "0901234567", Password, role), default);
        Assert.Equal(role, result.Role); Assert.Equal("staff@example.invalid", result.Email); Assert.Null(result.LastLoginAt);
        Assert.Equal(role, (await f.Login.Handle(new(result.Email, Password), default)).User.Role);
        var serialized = System.Text.Json.JsonSerializer.Serialize(result);
        Assert.DoesNotContain("Password", serialized); Assert.DoesNotContain("Token", serialized); Assert.DoesNotContain(Password, serialized);
        Assert.Single(await f.Db.AuditLogs.Where(a => a.Action == "Staff.Create").ToListAsync());
        foreach (var forbidden in new[] { "SuperAdmin", "Customer", "VendorOwner", "6", "finance", "Unknown" })
            await Assert.ThrowsAsync<DomainException>(() => handler.Handle(new("Staff", "other@example.invalid", "0901234567", Password, forbidden), default));
        foreach (var actorRole in new[] { "Customer", "VendorOwner", "Moderator", "Finance", "CustomerCare" })
            await Assert.ThrowsAsync<DomainException>(() => new CreateStaffAccountCommandHandler(f.Db, new Current(admin.Id, actorRole), f.Hasher)
                .Handle(new("Staff", "other@example.invalid", "0901234567", Password, role), default));
        Assert.Equal(2, await f.Db.Users.CountAsync());
    }

    [Fact]
    public async Task ModeratorCannotDisableOtherStaffButCanModeratePublicAccounts()
    {
        await using var f = await Fixture.Create();
        var moderator = f.AddUser(UserRole.Moderator, "moderator"); var finance = f.AddUser(UserRole.Finance, "finance");
        var customer = f.AddUser(UserRole.Customer, "customer"); await f.Db.SaveChangesAsync();
        var handler = new UpdateUserStatusCommandHandler(f.Db, new Current(moderator.Id, "Moderator"));
        await Assert.ThrowsAsync<DomainException>(() => handler.Handle(new(finance.Id, false), default));
        await Assert.ThrowsAsync<DomainException>(() => handler.Handle(new(moderator.Id, false), default));
        await handler.Handle(new(customer.Id, false), default); Assert.False(customer.IsActive); Assert.True(finance.IsActive);
        moderator.SetStatus(false); await f.Db.SaveChangesAsync();
        await Assert.ThrowsAsync<DomainException>(() => handler.Handle(new(customer.Id, true), default)); Assert.False(customer.IsActive);
    }

    [Fact]
    public async Task OtpIsCustomerOnlyAndNeverCreatesDuplicateAccountOrSendsStaffCodes()
    {
        await using var f = await Fixture.Create();
        var delivery = new Delivery(); var existing = f.AddUser(UserRole.Customer, "couple");
        var staff = f.AddUser(UserRole.Finance, "finance"); await f.Db.SaveChangesAsync();
        var request = new RequestOtpCommandHandler(f.Db, delivery);
        await Assert.ThrowsAsync<DomainException>(() => request.Handle(new(staff.Email), default)); Assert.Equal(0, delivery.Sent);
        var otp = OtpCode.Create(existing.Email, out var code); f.Db.OtpCodes.Add(otp); await f.Db.SaveChangesAsync();
        var verify = new VerifyOtpCommandHandler(f.Db, f.Hasher, f.Jwt);
        var result = await verify.Handle(new(" COUPLE@EXAMPLE.INVALID ", code, null), default);
        Assert.Equal(existing.Id, result.User.Id); Assert.Equal("Customer", result.User.Role);
        await Assert.ThrowsAsync<DomainException>(() => verify.Handle(new(existing.Email, code, null), default));
        await Assert.ThrowsAsync<DomainException>(() => verify.Handle(new("bad target", "123456", null), default));
        Assert.Equal(2, await f.Db.Users.CountAsync());
    }

    [Fact]
    public async Task GoogleNeverAutoLinksAnExistingEmailAndCreatesOnlyCustomer()
    {
        await using var f = await Fixture.Create();
        var existing = f.AddUser(UserRole.VendorOwner, "owner"); await f.Db.SaveChangesAsync();
        var conflict = new GoogleLoginCommandHandler(f.Db, f.Hasher, f.Jwt, new Google(new("google-subject", existing.Email, "Owner", null)));
        await Assert.ThrowsAsync<DomainException>(() => conflict.Handle(new("verified-by-fake"), default));
        Assert.Null(existing.GoogleSubject);
        var google = new GoogleLoginCommandHandler(f.Db, f.Hasher, f.Jwt, new Google(new("new-google-subject", "google@example.invalid", "Google customer", null)));
        var result = await google.Handle(new("verified-by-fake"), default);
        Assert.Equal("Customer", result.User.Role); Assert.Equal(result.User.Id, (await google.Handle(new("verified-by-fake"), default)).User.Id);
        var customer = await f.Db.Users.SingleAsync(u => u.Id == result.User.Id); customer.SetStatus(false); await f.Db.SaveChangesAsync();
        await Assert.ThrowsAsync<DomainException>(() => google.Handle(new("verified-by-fake"), default));
        Assert.Equal(2, await f.Db.Users.CountAsync());
    }

    [Theory]
    [InlineData(UserRole.Customer)] [InlineData(UserRole.VendorOwner)] [InlineData(UserRole.Moderator)]
    [InlineData(UserRole.Finance)] [InlineData(UserRole.CustomerCare)] [InlineData(UserRole.SuperAdmin)]
    public void IssuedJwtUsesActualUserRoleAndId(UserRole role)
    {
        var user = User.Create("JWT actor", "actor@example.invalid", "0901234567", "hash", role);
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        { ["JwtSettings:Secret"] = "Only-a-local-unit-test-signing-key-with-more-than-32-characters" }).Build();
        var token = new JwtSecurityTokenHandler().ReadJwtToken(new JwtProvider(configuration).GenerateToken(user));
        Assert.Contains(token.Claims, c => c.Type == "role" && c.Value == role.ToString());
        Assert.Contains(token.Claims, c => c.Type == "nameid" && c.Value == user.Id.ToString());
        Assert.DoesNotContain(token.Claims, c => c.Type.Contains("password", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void EveryLoginIssuesDistinctTokenEvenForSameUserAndSameSecond()
    {
        var user = User.Create("JWT actor", "actor@example.invalid", "0901234567", "hash", UserRole.Customer);
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        { ["JwtSettings:Secret"] = "Only-a-local-unit-test-signing-key-with-more-than-32-characters" }).Build();
        var provider = new JwtProvider(configuration);
        var first = provider.GenerateToken(user); var second = provider.GenerateToken(user);
        Assert.NotEqual(first, second);
        var reader = new JwtSecurityTokenHandler();
        var firstId = reader.ReadJwtToken(first).Claims.Single(c => c.Type == JwtRegisteredClaimNames.Jti).Value;
        var secondId = reader.ReadJwtToken(second).Claims.Single(c => c.Type == JwtRegisteredClaimNames.Jti).Value;
        Assert.NotEqual(firstId, secondId); Assert.True(Guid.TryParseExact(firstId, "N", out _)); Assert.True(Guid.TryParseExact(secondId, "N", out _));
    }

    [SqlServerFact]
    public async Task SqlServerConcurrentRegistrationOfSameEmailCreatesExactlyOneAccount()
    {
        var database = $"WeddingPlanTest_Auth_{Guid.NewGuid():N}";
        var source = Environment.GetEnvironmentVariable("WEDDING_TEST_SQLSERVER");
        var builder = new SqlConnectionStringBuilder(string.IsNullOrWhiteSpace(source) || source == "1"
            ? "Server=localhost;Integrated Security=true;TrustServerCertificate=true;Connection Timeout=5" : source) { InitialCatalog = database };
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlServer(builder.ConnectionString).Options;
        await using var setup = new ApplicationDbContext(options);
        try
        {
            await setup.Database.EnsureCreatedAsync();
            var results = await Task.WhenAll(Enumerable.Range(0, 8).Select(async index =>
            {
                await using var db = new ApplicationDbContext(options);
                try
                {
                    await new RegisterCustomerCommandHandler(db, new Hasher(), new Jwt()).Handle(
                        new("Concurrent customer", "SAME@example.invalid", "0901234567", Password), default);
                    return true;
                }
                catch (DomainException) { return false; }
            }));
            Assert.Equal(1, results.Count(success => success)); Assert.Equal(1, await setup.Users.CountAsync());
        }
        finally
        {
            Assert.StartsWith("WeddingPlanTest_Auth_", database); await setup.Database.EnsureDeletedAsync();
        }
    }

    private sealed class Fixture : IAsyncDisposable
    {
        private readonly SqliteConnection connection;
        public ApplicationDbContext Db { get; }
        public Hasher Hasher { get; } = new();
        public Jwt Jwt { get; } = new();
        public RegisterCustomerCommandHandler Customer => new(Db, Hasher, Jwt);
        public RegisterVendorCommandHandler Vendor => new(Db, Hasher, Jwt);
        public LoginCommandHandler Login => new(Db, Hasher, Jwt);
        private Fixture(SqliteConnection connection, ApplicationDbContext db) { this.connection = connection; Db = db; }
        public static async Task<Fixture> Create()
        {
            var connection = new SqliteConnection("Data Source=:memory:"); await connection.OpenAsync();
            var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options);
            await db.Database.EnsureCreatedAsync(); return new(connection, db);
        }
        public User AddUser(UserRole role, string local)
        {
            var user = User.Create(local, $"{local}@example.invalid", "0901234567", Hasher.Hash(Password), role); Db.Users.Add(user); return user;
        }
        public async ValueTask DisposeAsync() { await Db.DisposeAsync(); await connection.DisposeAsync(); }
    }
    private sealed class Hasher : IPasswordHasher
    {
        public string Hash(string password) => "hash:" + password;
        public bool Verify(string password, string passwordHash) => Hash(password) == passwordHash;
    }
    private sealed class Jwt : IJwtProvider { public string GenerateToken(User user) => $"test-token:{user.Role}:{user.Id}"; }
    private sealed record Current(Guid? UserId, string? Role) : ICurrentUserService { public string? Email => null; }
    private sealed record Google(GoogleIdentity Identity) : IGoogleIdentityVerifier
    { public Task<GoogleIdentity> VerifyAsync(string idToken, CancellationToken ct) => Task.FromResult(Identity); }
    private sealed class Delivery : IOtpDelivery
    {
        public bool EmailEnabled => true;
        public int Sent { get; private set; }
        public Task SendEmailAsync(string target, string code, CancellationToken ct) { Sent++; return Task.CompletedTask; }
    }
}
