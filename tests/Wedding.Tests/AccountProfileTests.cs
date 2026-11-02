using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Wedding.Application.Common.Interfaces;
using Wedding.Application.Features.Auth.Commands;
using Wedding.Domain.Entities;
using Wedding.Domain.Enums;
using Wedding.Domain.Exceptions;
using Wedding.Infrastructure.Persistence;

namespace Wedding.Tests;

public class AccountProfileTests
{
    [Theory]
    [InlineData(UserRole.Customer)] [InlineData(UserRole.VendorOwner)] [InlineData(UserRole.Moderator)]
    [InlineData(UserRole.Finance)] [InlineData(UserRole.CustomerCare)] [InlineData(UserRole.SuperAdmin)]
    public async Task AllActorsCanUpdateOnlyTheirDisplayNameAndPreserveSecurityAndWeddingData(UserRole role)
    {
        await using var f = await Fixture.Create();
        var user = f.AddUser(role); var other = f.AddUser(UserRole.Customer, "other");
        user.UpdateProfile("Before", "https://example.invalid/avatar.webp");
        var wedding = WeddingProfile.Create(user.Id); wedding.CompleteOnboarding(); f.Db.WeddingProfiles.Add(wedding);
        await f.Db.SaveChangesAsync();
        var before = WeddingState(wedding);
        var command = JsonSerializer.Deserialize<UpdateAccountProfileCommand>(JsonSerializer.Serialize(new
        { fullName = "  Nguyễn Văn An  ", id = other.Id, userId = other.Id, email = "changed@example.invalid", role = "SuperAdmin", avatarUrl = "https://bad.invalid/image", passwordHash = "bad", phoneNumber = "0999999999" }), new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
        var result = await new UpdateAccountProfileHandler(f.Db, new Current(user.Id)).Handle(command, default);
        Assert.Equal(user.Id, result.Id); Assert.Equal("Nguyễn Văn An", result.FullName); Assert.Equal(role.ToString(), result.Role);
        Assert.Equal("actor@example.invalid", result.Email); Assert.Equal("0901234567", result.PhoneNumber);
        Assert.Equal("https://example.invalid/avatar.webp", result.AvatarUrl); Assert.Equal("original-password-hash", user.PasswordHash);
        Assert.Equal(0, user.SecurityVersion); Assert.Equal("other", other.FullName); Assert.Equal(before, WeddingState(wedding));
        var audit = Assert.Single(await f.Db.AuditLogs.ToListAsync()); Assert.Equal("Auth.UpdateProfile", audit.Action);
        Assert.Equal(user.Id, audit.ActorId); Assert.Null(audit.DataJson);
        Assert.DoesNotContain("Nguyễn", command.ToString()); Assert.DoesNotContain("password-hash", JsonSerializer.Serialize(result));
        await new UpdateAccountProfileHandler(f.Db, new Current(user.Id)).Handle(new("Nguyễn Văn An"), default);
        Assert.Single(await f.Db.AuditLogs.ToListAsync());
    }

    [Theory]
    [InlineData(null)] [InlineData("")] [InlineData("  ")] [InlineData("A\nB")] [InlineData("A\u007fB")] [InlineData("A\u0085B")]
    public async Task InvalidNamesNeverPersist(string? name)
    {
        await using var f = await Fixture.Create(); var user = f.AddUser(UserRole.Customer); await f.Db.SaveChangesAsync();
        await Assert.ThrowsAsync<DomainException>(() => new UpdateAccountProfileHandler(f.Db, new Current(user.Id)).Handle(new(name!), default));
        Assert.Equal("actor", user.FullName); Assert.Empty(await f.Db.AuditLogs.ToListAsync());
    }

    [Fact]
    public async Task LengthAndActiveOwnershipAreEnforcedInHandler()
    {
        await using var f = await Fixture.Create(); var user = f.AddUser(UserRole.Customer); await f.Db.SaveChangesAsync();
        await Assert.ThrowsAsync<DomainException>(() => new UpdateAccountProfileHandler(f.Db, new Current(user.Id)).Handle(new(new string('a', 151)), default));
        await new UpdateAccountProfileHandler(f.Db, new Current(user.Id)).Handle(new(new string('a', 150)), default);
        user.SetStatus(false); await f.Db.SaveChangesAsync();
        foreach (var id in new Guid?[] { user.Id, Guid.NewGuid(), null })
            await Assert.ThrowsAsync<DomainException>(() => new UpdateAccountProfileHandler(f.Db, new Current(id)).Handle(new("New"), default));
        Assert.Equal(new string('a', 150), user.FullName);
    }

    private static string WeddingState(WeddingProfile p) => JsonSerializer.Serialize(new { p.Id, p.CustomerId, p.GroomName, p.BrideName, p.City, p.TotalBudget, p.GuestCount, p.OnboardingCompletedAt, p.UpdatedAt });
    private sealed record Current(Guid? UserId) : ICurrentUserService { public string? Role => null; public string? Email => null; }
    private sealed class Fixture(SqliteConnection connection, ApplicationDbContext db) : IAsyncDisposable
    {
        public ApplicationDbContext Db { get; } = db;
        public static async Task<Fixture> Create()
        {
            var c = new SqliteConnection("Data Source=:memory:"); await c.OpenAsync();
            var d = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(c).Options);
            await d.Database.EnsureCreatedAsync(); return new(c, d);
        }
        public User AddUser(UserRole role, string name = "actor")
        { var u = User.Create(name, name + "@example.invalid", "0901234567", "original-password-hash", role); Db.Users.Add(u); return u; }
        public async ValueTask DisposeAsync() { await Db.DisposeAsync(); await connection.DisposeAsync(); }
    }
}
