using System.Reflection;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Wedding.Application.Common.Interfaces;
using Wedding.Domain.Entities;
using Wedding.Domain.Enums;
using Wedding.Domain.Exceptions;
using Wedding.Infrastructure.Persistence;
using Wedding.Infrastructure.Services;
using Wedding.WebApi.Controllers;

namespace Wedding.Tests;

public class AdvancePaymentAccessTests
{
    [Fact]
    public async Task VendorListFiltersInDatabaseAndNeverReturnsAnotherShopsBankingData()
    {
        await using var f = await Fixture.Create();
        var auditCount = await f.Db.AuditLogs.CountAsync();
        var own = await f.Service(f.Owner).GetAllAsync();
        Assert.Equal(2, own.Count);
        Assert.All(own, notice => Assert.Equal(f.Vendor.Id, notice.VendorId));
        var serialized = JsonSerializer.Serialize(own);
        Assert.DoesNotContain("other-shop-bank-number", serialized);
        Assert.DoesNotContain("Other shop account holder", serialized);
        Assert.DoesNotContain("Other shop private note", serialized);

        var other = Assert.Single(await f.Service(f.OtherOwner).GetAllAsync());
        Assert.Equal(f.OtherVendor.Id, other.VendorId);
        Assert.DoesNotContain(own, notice => notice.Id == other.Id);
        Assert.Equal(3, await f.Db.AdvancePayments.CountAsync());
        Assert.Equal(auditCount, await f.Db.AuditLogs.CountAsync());
    }

    [Theory]
    [InlineData(UserRole.Finance)]
    [InlineData(UserRole.SuperAdmin)]
    public async Task FinanceAndSuperAdminCanStillListEveryVendorsNotices(UserRole role)
    {
        await using var f = await Fixture.Create();
        var actor = f.AddUser(role); await f.Db.SaveChangesAsync();
        var notices = await f.Service(actor).GetAllAsync();
        Assert.Equal(3, notices.Count);
        Assert.Contains(notices, notice => notice.VendorId == f.Vendor.Id);
        Assert.Contains(notices, notice => notice.VendorId == f.OtherVendor.Id);
    }

    [Fact]
    public async Task ExistingVendorSettlementCallerStillWorksWithoutForeignBankData()
    {
        await using var f = await Fixture.Create();
        foreach (var (owner, vendor, expectedCount) in new[] { (f.Owner, f.Vendor, 2), (f.OtherOwner, f.OtherVendor, 1) })
        {
            var current = new Current(owner.Id, nameof(UserRole.VendorOwner));
            var handler = new Wedding.Application.Features.Commissions.Queries.GetVendorSettlementStatement.GetVendorSettlementStatementQueryHandler(
                f.Db, current, new UnusedQr(), f.Service(current));
            var statement = await handler.Handle(new(), default);
            Assert.Equal(vendor.Id, statement.VendorId);
            Assert.NotNull(statement.AdvanceRequests);
            Assert.Equal(expectedCount, statement.AdvanceRequests.Count);
            Assert.All(statement.AdvanceRequests, notice => Assert.Equal(vendor.Id, notice.VendorId));
        }
    }

    [Theory]
    [InlineData(UserRole.Customer)]
    [InlineData(UserRole.Moderator)]
    [InlineData(UserRole.CustomerCare)]
    public async Task NonFinancialRolesCannotBypassControllerByCallingListService(UserRole role)
    {
        await using var f = await Fixture.Create();
        var actor = f.AddUser(role); await f.Db.SaveChangesAsync();
        await Assert.ThrowsAsync<DomainException>(() => f.Service(actor).GetAllAsync());
    }

    [Fact]
    public async Task MissingInactiveOrForgedActorsFailClosed()
    {
        await using var f = await Fixture.Create();
        foreach (var actor in new[]
        {
            new Current(null, "SuperAdmin"), new Current(Guid.Empty, "Finance"),
            new Current(Guid.NewGuid(), "SuperAdmin"), new Current(f.Owner.Id, null),
            new Current(f.Owner.Id, "SuperAdmin"), new Current(f.Owner.Id, "vendorowner")
        })
            await Assert.ThrowsAsync<DomainException>(() => f.Service(actor).GetAllAsync());

        f.Owner.SetStatus(false); await f.Db.SaveChangesAsync();
        await Assert.ThrowsAsync<DomainException>(() => f.Service(f.Owner).GetAllAsync());
        var finance = f.AddUser(UserRole.Finance); finance.SetStatus(false); await f.Db.SaveChangesAsync();
        await Assert.ThrowsAsync<DomainException>(() => f.Service(finance).GetAllAsync());
    }

    [Fact]
    public async Task VendorWithoutOwnActiveShopCannotReceiveAllNotices()
    {
        await using var f = await Fixture.Create();
        var ownerWithoutShop = f.AddUser(UserRole.VendorOwner); await f.Db.SaveChangesAsync();
        await Assert.ThrowsAsync<DomainException>(() => f.Service(ownerWithoutShop).GetAllAsync());
        f.Db.Entry(f.Vendor).Property(vendor => vendor.IsActive).CurrentValue = false;
        await f.Db.SaveChangesAsync();
        await Assert.ThrowsAsync<DomainException>(() => f.Service(f.Owner).GetAllAsync());
    }

    [Theory]
    [InlineData(null, false, false)]
    [InlineData("Customer", false, false)]
    [InlineData("Moderator", false, false)]
    [InlineData("CustomerCare", false, false)]
    [InlineData("VendorOwner", true, false)]
    [InlineData("Finance", true, true)]
    [InlineData("SuperAdmin", true, true)]
    public async Task ListEndpointsRequireExpectedRolesAndRejectAnonymous(string? role, bool mayUseList, bool mayUseAdminList)
    {
        using var services = new ServiceCollection().AddLogging().AddAuthorizationCore().BuildServiceProvider();
        var principal = role == null ? new ClaimsPrincipal(new ClaimsIdentity()) : new ClaimsPrincipal(
            new ClaimsIdentity([new Claim(ClaimTypes.Role, role)], "TestAuthentication"));
        Assert.Equal(mayUseList, await CanAccess(nameof(CommissionsController.GetAllAdvanceRequests)));
        Assert.Equal(mayUseAdminList, await CanAccess(nameof(CommissionsController.GetAdvanceRequests)));

        async Task<bool> CanAccess(string methodName)
        {
            var method = typeof(CommissionsController).GetMethod(methodName)!;
            Assert.Empty(method.GetCustomAttributes<AllowAnonymousAttribute>());
            var data = typeof(CommissionsController).GetCustomAttributes<AuthorizeAttribute>()
                .Concat(method.GetCustomAttributes<AuthorizeAttribute>()).Cast<IAuthorizeData>();
            var provider = services.GetRequiredService<IAuthorizationPolicyProvider>();
            var policy = await AuthorizationPolicy.CombineAsync(provider, data);
            Assert.NotNull(policy);
            return (await services.GetRequiredService<IAuthorizationService>().AuthorizeAsync(principal, null, policy)).Succeeded;
        }
    }

    private sealed record Current(Guid? UserId, string? Role) : ICurrentUserService { public string? Email => null; }
    private sealed class UnusedQr : IVietQrService
    {
        public VietQrInfo GenerateVietQr(decimal amount, string description) => throw new NotSupportedException();
        public string GenerateQrImageUrl(string bankId, string accountNumber, string accountName, decimal amount, string description) => throw new NotSupportedException();
    }
    private sealed class Audit : IAuditLogger
    {
        public Task LogAsync(string action, string entityType, string? entityId, object? data = null) => Task.CompletedTask;
    }

    private sealed class Fixture(SqliteConnection connection, ApplicationDbContext db) : IAsyncDisposable
    {
        public ApplicationDbContext Db { get; } = db;
        public User Owner { get; private set; } = null!;
        public User OtherOwner { get; private set; } = null!;
        public Vendor Vendor { get; private set; } = null!;
        public Vendor OtherVendor { get; private set; } = null!;

        public static async Task<Fixture> Create()
        {
            var connection = new SqliteConnection("Data Source=:memory:"); await connection.OpenAsync();
            var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options);
            await db.Database.EnsureCreatedAsync(); var f = new Fixture(connection, db);
            f.Owner = f.AddUser(UserRole.VendorOwner); f.OtherOwner = f.AddUser(UserRole.VendorOwner);
            f.Vendor = Vendor.Create(f.Owner.Id, "Own shop", "own-shop", "Own owner", "0900000000", "Hà Nội", .08m);
            f.OtherVendor = Vendor.Create(f.OtherOwner.Id, "Other shop", "other-shop", "Other owner", "0900000001", "Hà Nội", .1m);
            db.Vendors.AddRange(f.Vendor, f.OtherVendor);
            db.AdvancePayments.AddRange(
                AdvancePayment.Create(Guid.NewGuid(), "OWN-1", f.Vendor.Id, f.Vendor.BrandName, 100, "Synthetic request", "Test bank", "own-shop-bank-number", "Own account holder", null),
                AdvancePayment.Create(Guid.NewGuid(), "OWN-2", f.Vendor.Id, f.Vendor.BrandName, 200, "Synthetic request", "Test bank", "own-shop-bank-number", "Own account holder", null),
                AdvancePayment.Create(Guid.NewGuid(), "OTHER-1", f.OtherVendor.Id, f.OtherVendor.BrandName, 300, "Synthetic request", "Test bank", "other-shop-bank-number", "Other shop account holder", "Other shop private note"));
            await db.SaveChangesAsync(); return f;
        }

        public User AddUser(UserRole role)
        {
            var user = User.Create("Synthetic actor", $"{Guid.NewGuid():N}@example.invalid", "0900000000", "test-hash", role);
            Db.Users.Add(user); return user;
        }
        public AdvancePaymentService Service(User user) => Service(new Current(user.Id, user.Role.ToString()));
        public AdvancePaymentService Service(ICurrentUserService user) => new(Db, new Audit(), user);
        public async ValueTask DisposeAsync() { await Db.DisposeAsync(); await connection.DisposeAsync(); }
    }
}
