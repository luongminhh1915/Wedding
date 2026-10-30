using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Wedding.Application.Features.CommissionPolicies;
using Wedding.Domain.Entities;
using Wedding.Domain.Enums;
using Wedding.Infrastructure.Persistence;

namespace Wedding.Tests;

public class CommissionRateResolverTests : IAsyncDisposable
{
    private readonly SqliteConnection _connection;
    private readonly ApplicationDbContext _db;
    private readonly DateTime _now = new(2026, 10, 30, 12, 0, 0, DateTimeKind.Utc);
    private readonly User _admin;
    private readonly User _owner;
    private readonly Vendor _vendor;
    private readonly Category _category;
    private readonly ServiceItem _service;

    public CommissionRateResolverTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        _db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(_connection).Options);
        _db.Database.EnsureCreatedAsync().GetAwaiter().GetResult();

        _admin = User.Create("Admin", $"admin-{Guid.NewGuid():N}@test.local", "0900000000", "admin-user", UserRole.SuperAdmin);
        _owner = User.Create("Owner", $"owner-{Guid.NewGuid():N}@test.local", "0900000001", "owner-user", UserRole.VendorOwner);
        _db.Users.AddRange(_admin, _owner);

        _category = Category.Create("Test Category", $"cat-{Guid.NewGuid():N}", null, null, 0.08m);
        _db.Categories.Add(_category);

        _vendor = Vendor.Create(_owner.Id, "Test Vendor", $"vendor-{Guid.NewGuid():N}", "Owner", "0900000001", "HCM", 0.08m);
        _db.Vendors.Add(_vendor);

        var listing = Listing.Create(_vendor.Id, _category.Id, "Test Listing", $"listing-{Guid.NewGuid():N}", 100_000, 100_000, "Desc", "HCM");
        listing.Approve();
        _db.Listings.Add(listing);

        _service = ServiceItem.Create(_vendor.Id, listing.Id, _category.Id, "Test Service", 100_000, "Package", 1, 100, "HCM", "Terms");
        _service.Submit();
        _service.Review(true, null);
        _db.ServiceItems.Add(_service);

        _db.SaveChanges();
    }

    public async ValueTask DisposeAsync()
    {
        await _db.DisposeAsync();
        await _connection.DisposeAsync();
    }

    private CommissionPolicyRule AddApprovedRule(
        CommissionRuleScope scope,
        Guid? categoryId = null,
        Guid? vendorId = null,
        Guid? serviceItemId = null,
        decimal? rate = null,
        DateTime? from = null,
        DateTime? to = null,
        string version = "v1")
    {
        var rule = CommissionPolicyRule.Create(
            scope,
            categoryId,
            vendorId,
            serviceItemId,
            null,
            null,
            rate,
            CommissionBasisKind.StandardService,
            version,
            "Test rule",
            from ?? _now.AddDays(-10),
            to ?? _now.AddDays(10),
            _admin.Id);

        rule.Submit();
        rule.Approve(_admin.Id);
        _db.CommissionPolicyRules.Add(rule);
        _db.SaveChanges();
        return rule;
    }

    [Fact]
    public async Task ServiceScopeTakesHighestPriority()
    {
        AddApprovedRule(CommissionRuleScope.System, rate: 0.10m);
        AddApprovedRule(CommissionRuleScope.Category, categoryId: _category.Id, rate: 0.09m);
        AddApprovedRule(CommissionRuleScope.Vendor, vendorId: _vendor.Id, rate: 0.08m);
        AddApprovedRule(CommissionRuleScope.VendorCategory, categoryId: _category.Id, vendorId: _vendor.Id, rate: 0.07m);
        AddApprovedRule(CommissionRuleScope.Service, serviceItemId: _service.Id, rate: 0.06m, version: "service-v1");

        var resolved = await CommissionRateResolver.ResolveRateAsync(
            _db, _vendor.Id, _category.Id, _service.Id, null, 0.12m, 0.08m, _now);

        Assert.Equal(0.06m, resolved.Rate);
        Assert.Equal("PolicyRule:Service", resolved.RateSource);
        Assert.Equal("service-v1", resolved.PolicyVersion);
    }

    [Fact]
    public async Task VendorCategoryScopeOverridesVendorCategoryAndSystem()
    {
        AddApprovedRule(CommissionRuleScope.System, rate: 0.10m);
        AddApprovedRule(CommissionRuleScope.Category, categoryId: _category.Id, rate: 0.09m);
        AddApprovedRule(CommissionRuleScope.Vendor, vendorId: _vendor.Id, rate: 0.08m);
        AddApprovedRule(CommissionRuleScope.VendorCategory, categoryId: _category.Id, vendorId: _vendor.Id, rate: 0.07m, version: "vc-v1");

        var resolved = await CommissionRateResolver.ResolveRateAsync(
            _db, _vendor.Id, _category.Id, null, null, 0.12m, 0.08m, _now);

        Assert.Equal(0.07m, resolved.Rate);
        Assert.Equal("PolicyRule:VendorCategory", resolved.RateSource);
        Assert.Equal("vc-v1", resolved.PolicyVersion);
    }

    [Fact]
    public async Task VendorScopeOverridesCategoryAndSystem()
    {
        AddApprovedRule(CommissionRuleScope.System, rate: 0.10m);
        AddApprovedRule(CommissionRuleScope.Category, categoryId: _category.Id, rate: 0.09m);
        AddApprovedRule(CommissionRuleScope.Vendor, vendorId: _vendor.Id, rate: 0.08m, version: "v-v1");

        var resolved = await CommissionRateResolver.ResolveRateAsync(
            _db, _vendor.Id, _category.Id, null, null, 0.12m, 0.08m, _now);

        Assert.Equal(0.08m, resolved.Rate);
        Assert.Equal("PolicyRule:Vendor", resolved.RateSource);
        Assert.Equal("v-v1", resolved.PolicyVersion);
    }

    [Fact]
    public async Task CategoryScopeOverridesSystem()
    {
        AddApprovedRule(CommissionRuleScope.System, rate: 0.10m);
        AddApprovedRule(CommissionRuleScope.Category, categoryId: _category.Id, rate: 0.09m, version: "cat-v1");

        var resolved = await CommissionRateResolver.ResolveRateAsync(
            _db, _vendor.Id, _category.Id, null, null, 0.12m, 0.08m, _now);

        Assert.Equal(0.09m, resolved.Rate);
        Assert.Equal("PolicyRule:Category", resolved.RateSource);
        Assert.Equal("cat-v1", resolved.PolicyVersion);
    }

    [Fact]
    public async Task SystemScopeOverridesFallback()
    {
        AddApprovedRule(CommissionRuleScope.System, rate: 0.10m, version: "sys-v1");

        var resolved = await CommissionRateResolver.ResolveRateAsync(
            _db, _vendor.Id, _category.Id, null, null, 0.12m, 0.08m, _now);

        Assert.Equal(0.10m, resolved.Rate);
        Assert.Equal("PolicyRule:System", resolved.RateSource);
        Assert.Equal("sys-v1", resolved.PolicyVersion);
    }

    [Fact]
    public async Task DraftSubmittedOrRejectedRulesAreIgnored()
    {
        var draft = CommissionPolicyRule.Create(
            CommissionRuleScope.Vendor, null, _vendor.Id, null, null, null, 0.05m,
            CommissionBasisKind.StandardService, "draft", "reason", _now.AddDays(-1), _now.AddDays(1), _admin.Id);

        var submitted = CommissionPolicyRule.Create(
            CommissionRuleScope.Category, _category.Id, null, null, null, null, 0.05m,
            CommissionBasisKind.StandardService, "submitted", "reason", _now.AddDays(-1), _now.AddDays(1), _admin.Id);
        submitted.Submit();

        var rejected = CommissionPolicyRule.Create(
            CommissionRuleScope.System, null, null, null, null, null, 0.05m,
            CommissionBasisKind.StandardService, "rejected", "reason", _now.AddDays(-1), _now.AddDays(1), _admin.Id);
        rejected.Submit();
        rejected.Reject(_admin.Id, "rejected reason");

        _db.CommissionPolicyRules.AddRange(draft, submitted, rejected);
        await _db.SaveChangesAsync();

        var resolved = await CommissionRateResolver.ResolveRateAsync(
            _db, _vendor.Id, _category.Id, null, null, 0m, 0.08m, _now);

        Assert.Equal(0.08m, resolved.Rate);
        Assert.Equal("CategoryDefault", resolved.RateSource);
        Assert.Null(resolved.RuleId);
    }

    [Fact]
    public async Task OutOfWindowRulesAreIgnored()
    {
        // Expired yesterday
        AddApprovedRule(CommissionRuleScope.Vendor, vendorId: _vendor.Id, rate: 0.05m,
            from: _now.AddDays(-10), to: _now.AddDays(-1));

        // Starts tomorrow
        AddApprovedRule(CommissionRuleScope.Category, categoryId: _category.Id, rate: 0.05m,
            from: _now.AddDays(1), to: _now.AddDays(10));

        var resolved = await CommissionRateResolver.ResolveRateAsync(
            _db, _vendor.Id, _category.Id, null, null, 0.12m, 0.08m, _now);

        Assert.Equal(0.12m, resolved.Rate);
        Assert.Equal("LegacyVendor", resolved.RateSource);
    }

    [Fact]
    public async Task FallbackToCategoryDefaultWhenLegacyVendorRateIsZero()
    {
        var resolved = await CommissionRateResolver.ResolveRateAsync(
            _db, _vendor.Id, _category.Id, null, null, 0m, 0.08m, _now);

        Assert.Equal(0.08m, resolved.Rate);
        Assert.Equal("CategoryDefault", resolved.RateSource);
        Assert.Null(resolved.PolicyVersion);
    }

    [Fact]
    public async Task FallbackToLegacyVendorWhenLegacyVendorRateIsGreaterThanZero()
    {
        var resolved = await CommissionRateResolver.ResolveRateAsync(
            _db, _vendor.Id, _category.Id, null, null, 0.11m, 0.08m, _now);

        Assert.Equal(0.11m, resolved.Rate);
        Assert.Equal("LegacyVendor", resolved.RateSource);
        Assert.Null(resolved.PolicyVersion);
    }
}
