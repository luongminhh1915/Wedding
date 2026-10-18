using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Wedding.Application.Common.Interfaces;
using Wedding.Application.Features.Auth.Commands;
using Wedding.Domain.Entities;
using Wedding.Domain.Exceptions;
using Wedding.Infrastructure.Persistence;

namespace Wedding.Tests;

public class VietnamProvincesTests
{
    [Fact]
    public void DirectoryContainsExactly34UniqueCurrentNames()
    {
        Assert.Equal(34, VietnamProvinces.Names.Count);
        Assert.Equal(34, VietnamProvinces.Names.Distinct().Count());
        foreach (var name in VietnamProvinces.Names)
        {
            Assert.True(VietnamProvinces.TryNormalize(name, out var normalized));
            Assert.Equal(name, normalized);
        }
        Assert.Contains("Huế", VietnamProvinces.Names);
        Assert.DoesNotContain("Thừa Thiên Huế", VietnamProvinces.Names);
    }

    [Theory]
    [InlineData("TP. HCM", "Hồ Chí Minh")]
    [InlineData("  HN  ", "Hà Nội")]
    [InlineData("Thành phố Đồng Nai", "Đồng Nai")]
    [InlineData("thanh pho Bac Ninh", "Bắc Ninh")]
    [InlineData("Tỉnh Đắk Lắk", "Đắk Lắk")]
    [InlineData("Hue", "Huế")]
    public void NewInputCanUseCommonAliasesWithoutLosingCanonicalName(string input, string expected)
    {
        Assert.True(VietnamProvinces.TryNormalize(input, out var normalized));
        Assert.Equal(expected, normalized);
    }

    [Theory]
    [InlineData(null)] [InlineData("")] [InlineData("Bình Dương")] [InlineData("Thừa Thiên Huế")]
    [InlineData("Hồ Chí Minh arbitrary")]
    public void UnknownOrRetiredProvinceCannotBeUsedForNewRegistration(string? city)
    {
        Assert.False(VietnamProvinces.TryNormalize(city, out _));
        Assert.False(new RegisterVendorCommandValidator().Validate(new RegisterVendorCommand("Vendor", "vendor@example.invalid", "0901234567", "Long test password", "Brand", city!)).IsValid);
    }

    [Fact]
    public async Task RegistrationPersistsCanonicalCityButDoesNotRewriteAnExistingLegacyVendor()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync();
        var legacyOwner = User.Create("Existing owner", "legacy@example.invalid", "0901234568", "existing", Wedding.Domain.Enums.UserRole.VendorOwner);
        var legacy = Vendor.Create(legacyOwner.Id, "Existing", "existing", "Existing owner", "0901234568", "Bình Dương", .08m);
        db.Users.Add(legacyOwner); db.Vendors.Add(legacy); await db.SaveChangesAsync();
        var handler = new RegisterVendorCommandHandler(db, new Hasher(), new Jwt());
        await Assert.ThrowsAsync<DomainException>(() => handler.Handle(new("New", "invalid@example.invalid", "0901234567", "Long test password", "New", "Unknown"), default));
        Assert.Equal(1, await db.Users.CountAsync());
        var result = await handler.Handle(new("New", "new@example.invalid", "0901234567", "Long test password", "New", " TP. HCM "), default);
        Assert.Equal("Hồ Chí Minh", (await db.Vendors.SingleAsync(vendor => vendor.Id == result.User.VendorId)).City);
        Assert.Equal("Bình Dương", (await db.Vendors.SingleAsync(vendor => vendor.Id == legacy.Id)).City);
    }

    private sealed class Hasher : IPasswordHasher
    {
        public string Hash(string password) => "hash:" + password;
        public bool Verify(string password, string passwordHash) => passwordHash == Hash(password);
    }
    private sealed class Jwt : IJwtProvider { public string GenerateToken(User user) => "test-token:" + user.Id; }
}
