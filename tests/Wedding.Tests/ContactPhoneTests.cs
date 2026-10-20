using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Wedding.Application.Common.Interfaces;
using Wedding.Application.Features.Auth.Commands;
using Wedding.Domain.Entities;
using Wedding.Domain.Enums;
using Wedding.Domain.Exceptions;
using Wedding.Infrastructure.Persistence;
using Wedding.Infrastructure.Services;

namespace Wedding.Tests;

public class ContactPhoneTests
{
    private const string Phone = "0901234567";

    [Fact]
    public async Task GooglePlaceholderCanBecomeContactPhoneWithoutPretendingToVerifyOwnership()
    {
        await using var f = await Fixture.Create();
        var user = f.AddUser(); user.LinkGoogle("google-subject");
        user.UpdateProfile("Tên khách hàng giữ nguyên", "https://example.invalid/avatar.webp"); user.RecordLogin();
        var profile = WeddingProfile.Create(user.Id);
        profile.Update("Chú rể", "Cô dâu", new DateTime(2027, 2, 12), "Hà Nội", 300000000, 180, "Classic", "partner@example.invalid");
        profile.CompleteOnboarding(); f.Db.WeddingProfiles.Add(profile); await f.Db.SaveChangesAsync();
        var previousProfile = new { profile.Id, profile.GroomName, profile.BrideName, profile.City, profile.TotalBudget, profile.GuestCount, profile.OnboardingCompletedAt, profile.UpdatedAt };
        var hash = user.PasswordHash; var login = user.LastLoginAt; var before = Read(f.Jwt.GenerateToken(user));

        var response = await f.Handler(user.Id).Handle(new("  " + Phone + "  "), default);

        Assert.Equal(user.Id, response.User.Id); Assert.Equal(Phone, response.User.PhoneNumber); Assert.Equal("Customer", response.User.Role);
        Assert.Null(response.User.VendorId); Assert.Null(response.User.VendorBrandName);
        Assert.Equal(hash, user.PasswordHash); Assert.Equal("google-subject", user.GoogleSubject);
        Assert.Equal("Tên khách hàng giữ nguyên", response.User.FullName); Assert.Equal("actor@example.invalid", response.User.Email);
        Assert.Equal("https://example.invalid/avatar.webp", response.User.AvatarUrl); Assert.Equal(login, user.LastLoginAt);
        Assert.Null(user.PhoneVerifiedAt); Assert.Equal(1, user.SecurityVersion); Assert.True(user.IsActive);
        var after = Read(response.Token);
        Assert.Equal(Phone, after.Claims.Single(c => c.Type == "phone_number").Value);
        Assert.Equal("1", after.Claims.Single(c => c.Type == "security_version").Value);
        Assert.Equal("0", before.Claims.Single(c => c.Type == "security_version").Value); Assert.NotEqual(before.Id, after.Id);
        Assert.False(JwtSessionPolicy.Matches(Principal(before), "Customer", user.SecurityVersion));
        Assert.True(JwtSessionPolicy.Matches(Principal(after), "Customer", user.SecurityVersion));
        var unchangedProfile = await f.Db.WeddingProfiles.SingleAsync();
        Assert.Equal(previousProfile, new { unchangedProfile.Id, unchangedProfile.GroomName, unchangedProfile.BrideName, unchangedProfile.City, unchangedProfile.TotalBudget, unchangedProfile.GuestCount, unchangedProfile.OnboardingCompletedAt, unchangedProfile.UpdatedAt });
        var audit = Assert.Single(await f.Db.AuditLogs.ToListAsync());
        Assert.Equal("Auth.UpdateContact", audit.Action); Assert.Equal(user.Id, audit.ActorId); Assert.Equal("Customer", audit.ActorRole);
        Assert.Equal(user.Id.ToString(), audit.EntityId); Assert.Null(audit.DataJson); Assert.DoesNotContain(Phone, JsonSerializer.Serialize(audit));
        Assert.DoesNotContain(hash, JsonSerializer.Serialize(response)); Assert.DoesNotContain(Phone, new UpdateContactCommand(Phone).ToString());
    }

    [Fact]
    public async Task ChangingAnAlreadyVerifiedPhoneResetsVerificationButSamePhoneDoesNotChangeVersion()
    {
        await using var f = await Fixture.Create();
        var user = f.AddUser(); user.VerifyPhone("0912345678"); await f.Db.SaveChangesAsync();
        var verifiedAt = user.PhoneVerifiedAt;
        var unchanged = await f.Handler(user.Id).Handle(new(" 0912345678 "), default);
        Assert.Equal(0, user.SecurityVersion); Assert.Equal(verifiedAt, user.PhoneVerifiedAt); Assert.Empty(await f.Db.AuditLogs.ToListAsync());
        var changed = await f.Handler(user.Id).Handle(new(Phone), default);
        Assert.Equal(1, user.SecurityVersion); Assert.Null(user.PhoneVerifiedAt); Assert.NotEqual(unchanged.Token, changed.Token);
        var repeated = await f.Handler(user.Id).Handle(new(Phone), default);
        Assert.Equal(1, user.SecurityVersion); Assert.Null(user.PhoneVerifiedAt); Assert.NotEqual(changed.Token, repeated.Token);
        Assert.Single(await f.Db.AuditLogs.ToListAsync());
    }

    [Theory]
    [InlineData(null)] [InlineData("")] [InlineData(" ")] [InlineData("090123456")]
    [InlineData("09012345678")] [InlineData("+84901234567")] [InlineData("0١٢٣٤٥٦٧٨٩")]
    [InlineData("090 123456")] [InlineData("090123456x")] [InlineData("1901234567")]
    public async Task InvalidContactNumbersNeverPersistOrIssueToken(string? phone)
    {
        await using var f = await Fixture.Create();
        var user = f.AddUser(); await f.Db.SaveChangesAsync();
        var probe = new JwtProbe(() => Assert.Fail("A rejected update must not issue a token."));
        await Assert.ThrowsAsync<DomainException>(() => f.Handler(user.Id, jwt: probe).Handle(new(phone!), default));
        var persisted = await f.Db.Users.AsNoTracking().SingleAsync();
        Assert.Equal("chua-cap-nhat", persisted.PhoneNumber); Assert.Equal(0, persisted.SecurityVersion);
        Assert.Null(persisted.PhoneVerifiedAt); Assert.Empty(await f.Db.AuditLogs.ToListAsync());
    }

    [Theory]
    [InlineData(UserRole.VendorOwner)] [InlineData(UserRole.Moderator)] [InlineData(UserRole.Finance)]
    [InlineData(UserRole.CustomerCare)] [InlineData(UserRole.SuperAdmin)]
    public async Task DatabaseRoleWinsEvenIfRequestIdentityClaimsCustomer(UserRole role)
    {
        await using var f = await Fixture.Create();
        var user = f.AddUser(role); await f.Db.SaveChangesAsync();
        await Assert.ThrowsAsync<DomainException>(() => f.Handler(user.Id, "Customer").Handle(new(Phone), default));
        var persisted = await f.Db.Users.SingleAsync(); Assert.Equal("chua-cap-nhat", persisted.PhoneNumber);
        Assert.Equal(0, persisted.SecurityVersion); Assert.Empty(await f.Db.AuditLogs.ToListAsync());
    }

    [Fact]
    public async Task InactiveMissingAndUnauthenticatedUsersCannotUpdateContact()
    {
        await using var f = await Fixture.Create();
        var user = f.AddUser(); user.SetStatus(false); await f.Db.SaveChangesAsync();
        foreach (var id in new Guid?[] { user.Id, Guid.NewGuid(), null })
            await Assert.ThrowsAsync<DomainException>(() => f.Handler(id).Handle(new(Phone), default));
        var persisted = await f.Db.Users.SingleAsync();
        Assert.Equal("chua-cap-nhat", persisted.PhoneNumber); Assert.Equal(1, persisted.SecurityVersion);
        Assert.Empty(await f.Db.AuditLogs.ToListAsync());
    }

    [Fact]
    public async Task ExtraIdentityFieldsCannotTargetAnotherCustomerOrChangeRoleEmailOrProfile()
    {
        await using var f = await Fixture.Create();
        var user = f.AddUser(); var other = f.AddUser(local: "other"); await f.Db.SaveChangesAsync();
        var command = JsonSerializer.Deserialize<UpdateContactCommand>(JsonSerializer.Serialize(new
        {
            phoneNumber = Phone, userId = other.Id, customerId = other.Id, role = "SuperAdmin",
            email = "attacker@example.invalid", fullName = "Replaced", phoneVerifiedAt = DateTime.UtcNow
        }), new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
        var response = await f.Handler(user.Id).Handle(command, default);
        Assert.Equal(user.Id, response.User.Id); Assert.Equal("actor@example.invalid", response.User.Email);
        Assert.Equal("actor", response.User.FullName); Assert.Equal("Customer", response.User.Role);
        Assert.Equal("chua-cap-nhat", other.PhoneNumber); Assert.Equal(0, other.SecurityVersion); Assert.Null(user.PhoneVerifiedAt);
    }

    [Fact]
    public async Task ReplacementTokenIsIssuedOnlyAfterTransactionCommit()
    {
        await using var f = await Fixture.Create();
        var user = f.AddUser(); await f.Db.SaveChangesAsync();
        var probe = new JwtProbe(() =>
        {
            Assert.Null(f.Db.Database.CurrentTransaction);
            Assert.Equal(Phone, f.Db.Users.AsNoTracking().Single().PhoneNumber);
            Assert.Single(f.Db.AuditLogs.AsNoTracking().ToList());
        });
        var response = await f.Handler(user.Id, jwt: probe).Handle(new(Phone), default);
        Assert.Equal("issued-after-commit", response.Token); Assert.Equal(1, probe.Issued);
    }

    [Fact]
    public async Task DomainValidationAndConcurrencyTokenProtectContactFromStalePasswordWriters()
    {
        await using var f = await Fixture.Create();
        var user = f.AddUser(); await f.Db.SaveChangesAsync();
        Assert.Throws<DomainException>(() => user.UpdateContactPhone("bad")); Assert.Equal(0, user.SecurityVersion);
        await using var stale = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(f.Connection).Options);
        var staleUser = await stale.Users.SingleAsync();
        await f.Handler(user.Id).Handle(new(Phone), default);
        staleUser.UpdatePassword("stale-password-hash");
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => stale.SaveChangesAsync());
        f.Db.ChangeTracker.Clear(); var persisted = await f.Db.Users.SingleAsync();
        Assert.Equal(Phone, persisted.PhoneNumber); Assert.Equal("original-password-hash", persisted.PasswordHash); Assert.Equal(1, persisted.SecurityVersion);
    }

    [SqlServerFact]
    public async Task SqlServerContactUpdatePersistsVersionAndPreservesPasswordAndIdentity()
    {
        var database = $"WeddingPlanTest_Contact_{Guid.NewGuid():N}";
        var source = Environment.GetEnvironmentVariable("WEDDING_TEST_SQLSERVER");
        var builder = new SqlConnectionStringBuilder(string.IsNullOrWhiteSpace(source) || source == "1"
            ? "Server=localhost;Integrated Security=true;TrustServerCertificate=true;Connection Timeout=5" : source) { InitialCatalog = database };
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlServer(builder.ConnectionString).Options;
        await using var db = new ApplicationDbContext(options);
        try
        {
            await db.Database.EnsureCreatedAsync();
            var user = User.Create("Customer", "contact@example.invalid", "chua-cap-nhat", "original-password-hash", UserRole.Customer);
            db.Users.Add(user); await db.SaveChangesAsync();
            await new UpdateContactCommandHandler(db, new Current(user.Id), Provider()).Handle(new(Phone), default);
            db.ChangeTracker.Clear(); var persisted = await db.Users.SingleAsync();
            Assert.Equal(user.Id, persisted.Id); Assert.Equal("contact@example.invalid", persisted.Email);
            Assert.Equal("original-password-hash", persisted.PasswordHash); Assert.Equal(UserRole.Customer, persisted.Role);
            Assert.Equal(Phone, persisted.PhoneNumber); Assert.Null(persisted.PhoneVerifiedAt); Assert.Equal(1, persisted.SecurityVersion);
            Assert.Single(await db.AuditLogs.Where(a => a.Action == "Auth.UpdateContact").ToListAsync());
        }
        finally { Assert.StartsWith("WeddingPlanTest_Contact_", database); await db.Database.EnsureDeletedAsync(); }
    }

    private static JwtSecurityToken Read(string token) => new JwtSecurityTokenHandler().ReadJwtToken(token);
    private static ClaimsPrincipal Principal(JwtSecurityToken token) => new(new ClaimsIdentity(
        token.Claims.Select(claim => new Claim(claim.Type == "role" ? ClaimTypes.Role : claim.Type, claim.Value)), "Bearer"));
    private static JwtProvider Provider() => new(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
    { ["JwtSettings:Secret"] = "Only-a-unit-test-signing-key-with-more-than-32-characters" }).Build());
    private sealed class Fixture : IAsyncDisposable
    {
        public SqliteConnection Connection { get; }
        public ApplicationDbContext Db { get; }
        public JwtProvider Jwt { get; } = Provider();
        private Fixture(SqliteConnection connection, ApplicationDbContext db) { Connection = connection; Db = db; }
        public static async Task<Fixture> Create()
        {
            var connection = new SqliteConnection("Data Source=:memory:"); await connection.OpenAsync();
            var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options);
            await db.Database.EnsureCreatedAsync(); return new(connection, db);
        }
        public User AddUser(UserRole role = UserRole.Customer, string local = "actor")
        {
            var user = User.Create(local, $"{local}@example.invalid", "chua-cap-nhat", "original-password-hash", role);
            Db.Users.Add(user); return user;
        }
        public UpdateContactCommandHandler Handler(Guid? id, string? role = "Customer", IJwtProvider? jwt = null) => new(Db, new Current(id, role), jwt ?? Jwt);
        public async ValueTask DisposeAsync() { await Db.DisposeAsync(); await Connection.DisposeAsync(); }
    }
    private sealed record Current(Guid? UserId, string? Role = "Customer") : ICurrentUserService { public string? Email => null; }
    private sealed class JwtProbe(Action onIssue) : IJwtProvider
    {
        public int Issued { get; private set; }
        public string GenerateToken(User user) { onIssue(); Issued++; return "issued-after-commit"; }
    }
}
