using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Wedding.Application.Common.Interfaces;
using Wedding.Application.Features.Auth.Commands;
using Wedding.Application.Features.Auth.DTOs;
using Wedding.Application.Features.BusinessGroups;
using Wedding.Application.Features.Contracts.Commands.ConfirmContract;
using Wedding.Domain.Entities;
using Wedding.Domain.Enums;
using Wedding.Domain.Exceptions;
using Wedding.Infrastructure.Persistence;
using Wedding.WebApi.Controllers;

namespace Wedding.Tests;

public class VendorBusinessGroupTests
{
    private const string Password = "Synthetic registration passphrase";

    [Fact]
    public async Task SeededGroupsAndCategoriesMatchRequiredNamesRatesAndStableIdentities()
    {
        await using var f = await Fixture.Create();
        var groups = await new GetBusinessGroupsQueryHandler(f.Db).Handle(new(), default);
        Assert.Equal(5, groups.Count);
        Assert.Equal(new[]
        {
            "Địa điểm & Tiệc cưới", "Trang trí & Thiệp cưới", "Quay chụp, Váy cưới & Makeup",
            "Tổ chức cưới trọn gói (A–Z)", "Tiệc & Sự kiện khác"
        }, groups.Select(group => group.Name));
        Assert.Equal(new[] { .03m, .08m, .10m, .12m, .03m }, groups.Select(group => group.DefaultCommissionRate));
        Assert.Equal(Enumerable.Range(201, 5).Select(number => Guid.Parse($"22222222-2222-2222-2222-222222222{number}")),
            groups.Select(group => group.Id));
        Assert.All(groups, group =>
        {
            Assert.False(string.IsNullOrWhiteSpace(group.Description));
            Assert.Equal(BusinessGroupPolicy.InitialVersion, group.PolicyVersion);
        });
        Assert.Equal(BusinessGroupPolicy.FullServicePlanningId, Assert.Single(groups.Where(group => group.RequiresCommissionBasisApproval)).Id);

        var categories = await f.Db.Categories.AsNoTracking().ToListAsync();
        Assert.Equal(8, categories.Count);
        var planning = Assert.Single(categories.Where(category => category.Slug == "wedding-planner"));
        Assert.Equal(Guid.Parse("11111111-1111-1111-1111-111111111107"), planning.Id);
        Assert.Equal("Tổ chức cưới trọn gói (A–Z) – Full-Service Wedding Planning", planning.Name);
        Assert.Equal(BusinessGroupPolicy.FullServicePlanningId, planning.BusinessGroupId);
        Assert.Equal(.12m, planning.DefaultCommissionRate); Assert.True(planning.RequiresCommissionBasisApproval);
        var venue = Assert.Single(categories.Where(category => category.Slug == "tiem-cuoi-venues"));
        var events = Assert.Single(categories.Where(category => category.Slug == "tiec-su-kien-khac"));
        Assert.Equal(BusinessGroupPolicy.VenueBanquetId, venue.BusinessGroupId);
        Assert.Equal(BusinessGroupPolicy.OtherEventsId, events.BusinessGroupId);
        Assert.NotEqual(venue.Id, events.Id);
        Assert.Equal(.03m, events.DefaultCommissionRate);
    }

    [Fact]
    public async Task RegistrationWithoutGroupSelectionPersistsGeneralAcknowledgementAndReloadsAfterLogin()
    {
        await using var f = await Fixture.Create();
        var before = DateTime.UtcNow;
        var registered = await f.Register.Handle(f.Valid(), default);
        await using var reload = f.OpenContext();
        var vendor = await reload.Vendors.Include(item => item.BusinessGroups).SingleAsync();
        Assert.Equal(registered.User.Id, vendor.UserId); Assert.Equal(registered.User.VendorId, vendor.Id);
        Assert.Equal(0m, vendor.CommissionRate); Assert.False(vendor.IsVerified);
        Assert.NotNull(vendor.PartnershipPolicyAcceptedAt);
        Assert.InRange(vendor.PartnershipPolicyAcceptedAt.Value, before, DateTime.UtcNow);
        Assert.Equal(BusinessGroupPolicy.ServiceCategoryRegistrationVersion, vendor.PartnershipPolicyVersion);
        Assert.Empty(vendor.BusinessGroups);
        Assert.Single(await reload.Users.ToListAsync()); Assert.Empty(await reload.VendorBusinessGroups.ToListAsync());
        Assert.Empty(await reload.Listings.ToListAsync()); Assert.Empty(await reload.Commissions.ToListAsync());

        var login = await new LoginCommandHandler(reload, f.Hasher, new Jwt()).Handle(new(registered.User.Email, Password), default);
        Assert.Equal("VendorOwner", login.User.Role); Assert.Equal(vendor.Id, login.User.VendorId);
        var own = await new GetMyBusinessGroupsQueryHandler(reload, new Current(login.User.Id)).Handle(new(), default);
        Assert.False(own.UsesLegacyCommissionRate); Assert.Null(own.LegacyCommissionRate);
        Assert.Equal(vendor.PartnershipPolicyAcceptedAt, own.PolicyAcceptedAt);
        Assert.Equal(BusinessGroupPolicy.ServiceCategoryRegistrationVersion, own.PolicyVersion);
        Assert.Empty(own.Groups);
    }

    [Fact]
    public async Task FailedTokenCreationRollsBackUserVendorAndMembershipsTogether()
    {
        await using var f = await Fixture.Create();
        var handler = new RegisterVendorCommandHandler(f.Db, f.Hasher, new FailingJwt());
        await Assert.ThrowsAsync<InvalidOperationException>(() => handler.Handle(f.Valid(), default));
        await using var reload = f.OpenContext();
        Assert.Empty(await reload.Users.ToListAsync()); Assert.Empty(await reload.Vendors.ToListAsync());
        Assert.Empty(await reload.VendorBusinessGroups.ToListAsync());
        Assert.Equal(5, await reload.BusinessGroups.CountAsync());
    }

    [Theory]
    [InlineData(0)] [InlineData(.03)] [InlineData(.08)] [InlineData(.10)] [InlineData(.12)] [InlineData(.15)] [InlineData(-1)]
    public async Task PublicRegistrationRejectsEveryClientSuppliedRateIncludingFormerValidRates(decimal rate)
    {
        await using var f = await Fixture.Create();
        var request = f.Valid() with { CommissionRate = rate };
        var validation = new RegisterVendorCommandValidator().Validate(request);
        Assert.Contains(validation.Errors, error => error.PropertyName == nameof(RegisterVendorCommand.CommissionRate));
        await Assert.ThrowsAsync<DomainException>(() => f.Register.Handle(request, default));
        Assert.Empty(await f.Db.Users.ToListAsync()); Assert.Empty(await f.Db.Vendors.ToListAsync());
        Assert.Empty(await f.Db.VendorBusinessGroups.ToListAsync());
    }

    [Fact]
    public async Task MissingGeneralAcknowledgementNeverCreatesPartialAccount()
    {
        await using var f = await Fixture.Create();
        await Assert.ThrowsAsync<DomainException>(() => f.Register.Handle(f.Valid() with { PartnershipPolicyAccepted = false }, default));
        Assert.Empty(await f.Db.Users.ToListAsync()); Assert.Empty(await f.Db.Vendors.ToListAsync());
        Assert.Empty(await f.Db.VendorBusinessGroups.ToListAsync());
    }

    [Fact]
    public async Task RegistrationDoesNotDependOnGroupAvailabilityOrClaimGroupPolicyAcceptance()
    {
        await using var f = await Fixture.Create();
        var group = await f.Db.BusinessGroups.SingleAsync(item => item.Id == BusinessGroupPolicy.PhotoBridalMakeupId);
        f.Db.Entry(group).Property(item => item.IsActive).CurrentValue = false;
        await f.Db.SaveChangesAsync();
        var first = await f.Register.Handle(f.Valid(), default);
        var publicGroups = await new GetBusinessGroupsQueryHandler(f.Db).Handle(new(), default);
        Assert.Equal(4, publicGroups.Count); Assert.DoesNotContain(publicGroups, item => item.Id == group.Id);

        f.Db.ChangeTracker.Clear();
        group = await f.Db.BusinessGroups.SingleAsync(item => item.Id == BusinessGroupPolicy.PhotoBridalMakeupId);
        f.Db.Entry(group).Property(item => item.IsActive).CurrentValue = true;
        f.Db.Entry(group).Property(item => item.PolicyVersion).CurrentValue = "different-unapproved-fixture-version";
        await f.Db.SaveChangesAsync();
        var second = await f.Register.Handle(f.Valid("other-new-owner@example.invalid"), default);
        Assert.NotEqual(first.User.Id, second.User.Id);
        Assert.Equal(2, await f.Db.Users.CountAsync()); Assert.Equal(2, await f.Db.Vendors.CountAsync());
        Assert.All(await f.Db.Vendors.AsNoTracking().ToListAsync(), vendor => Assert.Equal(BusinessGroupPolicy.ServiceCategoryRegistrationVersion, vendor.PartnershipPolicyVersion));
        Assert.Empty(await f.Db.VendorBusinessGroups.ToListAsync());
    }

    [Fact]
    public async Task LegacyGroupInputsAreIgnoredWithoutAssigningMembershipOrClientChosenRate()
    {
        await using var f = await Fixture.Create();
        List<Guid>?[] oldInputs = [null, [], [Guid.Empty], [BusinessGroupPolicy.DecorInvitationsId],
            [BusinessGroupPolicy.PhotoBridalMakeupId, BusinessGroupPolicy.PhotoBridalMakeupId], [Guid.NewGuid()]];
        for (var index = 0; index < oldInputs.Length; index++)
        {
            var registered = await f.Register.Handle(f.Valid($"compat-{index}@example.invalid") with { BusinessGroupIds = oldInputs[index] }, default);
            var vendor = await f.Db.Vendors.AsNoTracking().SingleAsync(row => row.Id == registered.User.VendorId);
            Assert.Equal(0, vendor.CommissionRate);
            Assert.Equal(BusinessGroupPolicy.ServiceCategoryRegistrationVersion, vendor.PartnershipPolicyVersion);
        }
        Assert.Equal(oldInputs.Length, await f.Db.Users.CountAsync());
        Assert.Empty(await f.Db.VendorBusinessGroups.ToListAsync()); Assert.Empty(await f.Db.Commissions.ToListAsync());
    }

    [Fact]
    public async Task MyGroupsAreDerivedFromCurrentVendorAndCannotLeakAnotherShopsMembership()
    {
        await using var f = await Fixture.Create();
        var first = await f.Register.Handle(f.Valid(), default);
        var second = await f.Register.Handle(f.Valid("other-owner@example.invalid") with { BusinessGroupIds = [BusinessGroupPolicy.OtherEventsId] }, default);
        // Historical memberships remain independently readable but no longer arise at signup.
        f.Db.VendorBusinessGroups.AddRange(
            VendorBusinessGroup.Create(first.User.VendorId!.Value, BusinessGroupPolicy.DecorInvitationsId),
            VendorBusinessGroup.Create(first.User.VendorId.Value, BusinessGroupPolicy.PhotoBridalMakeupId),
            VendorBusinessGroup.Create(second.User.VendorId!.Value, BusinessGroupPolicy.OtherEventsId));
        await f.Db.SaveChangesAsync();
        var own = await new GetMyBusinessGroupsQueryHandler(f.Db, new Current(first.User.Id)).Handle(new(), default);
        Assert.Equal(2, own.Groups.Count); Assert.DoesNotContain(own.Groups, group => group.Id == BusinessGroupPolicy.OtherEventsId);
        var other = await new GetMyBusinessGroupsQueryHandler(f.Db, new Current(second.User.Id)).Handle(new(), default);
        Assert.Equal(BusinessGroupPolicy.OtherEventsId, Assert.Single(other.Groups).Id);

        foreach (var role in new[] { "Customer", "Moderator", "Finance", "CustomerCare", "SuperAdmin", null })
            await Assert.ThrowsAsync<DomainException>(() => new GetMyBusinessGroupsQueryHandler(f.Db, new Current(first.User.Id, role)).Handle(new(), default));
        await Assert.ThrowsAsync<DomainException>(() => new GetMyBusinessGroupsQueryHandler(f.Db, new Current(null)).Handle(new(), default));
        await Assert.ThrowsAsync<NotFoundException>(() => new GetMyBusinessGroupsQueryHandler(f.Db, new Current(Guid.NewGuid())).Handle(new(), default));
        var missingShopOwner = User.Create("Synthetic missing shop", "missing-shop@example.invalid", "0901234568", "hash", UserRole.VendorOwner);
        f.Db.Users.Add(missingShopOwner); await f.Db.SaveChangesAsync();
        await Assert.ThrowsAsync<NotFoundException>(() => new GetMyBusinessGroupsQueryHandler(f.Db, new Current(missingShopOwner.Id)).Handle(new(), default));
        var customer = User.Create("Synthetic customer", "not-a-vendor@example.invalid", "0901234569", "hash", UserRole.Customer);
        f.Db.Users.Add(customer); await f.Db.SaveChangesAsync();
        await Assert.ThrowsAsync<NotFoundException>(() => new GetMyBusinessGroupsQueryHandler(f.Db, new Current(customer.Id)).Handle(new(), default));
    }

    [Fact]
    public async Task LegacyRateAndExistingSnapshotRemainUnchangedAfterNewRegistration()
    {
        await using var f = await Fixture.Create();
        var owner = User.Create("Synthetic legacy owner", "legacy-owner@example.invalid", "0901234568", "legacy-hash", UserRole.VendorOwner);
        var legacy = Vendor.Create(owner.Id, "Legacy shop", "legacy-shop", "Legacy owner", "0901234568", "Bình Dương", .10m);
        f.Db.Users.Add(owner); f.Db.Vendors.Add(legacy); await f.Db.SaveChangesAsync();
        var (contract, _) = await f.AddContract(legacy, "wedding-planner");
        contract.ConfirmByCustomer(); contract.SnapshotCommissionRate(.10m); await f.Db.SaveChangesAsync();
        await f.Register.Handle(f.Valid(), default);
        await using var reload = f.OpenContext();
        var preserved = await reload.Vendors.SingleAsync(item => item.Id == legacy.Id);
        Assert.Equal(.10m, preserved.CommissionRate); Assert.Equal("Bình Dương", preserved.City);
        Assert.Null(preserved.PartnershipPolicyAcceptedAt); Assert.Null(preserved.PartnershipPolicyVersion);
        Assert.Empty(await reload.VendorBusinessGroups.Where(item => item.VendorId == legacy.Id).ToListAsync());
        var snapshot = await reload.BookingContracts.SingleAsync(item => item.Id == contract.Id);
        Assert.Equal(.10m, snapshot.CommissionRateSnapshot); Assert.Equal(ContractStatus.Confirmed, snapshot.Status);
        var own = await new GetMyBusinessGroupsQueryHandler(reload, new Current(owner.Id)).Handle(new(), default);
        Assert.True(own.UsesLegacyCommissionRate); Assert.Equal(.10m, own.LegacyCommissionRate);
        Assert.Empty(own.Groups); Assert.Null(own.PolicyAcceptedAt);
    }

    [Theory]
    [InlineData("tiem-cuoi-venues", .03)]
    [InlineData("trang-tri-decor", .08)]
    [InlineData("quay-chup-photo-video", .10)]
    public async Task NewVendorUsesEachServicesCategoryRateRatherThanOneShopWideRate(string categorySlug, decimal expectedRate)
    {
        await using var f = await Fixture.Create();
        var registered = await f.Register.Handle(f.Valid() with
        {
            BusinessGroupIds = [BusinessGroupPolicy.VenueBanquetId, BusinessGroupPolicy.DecorInvitationsId, BusinessGroupPolicy.PhotoBridalMakeupId]
        }, default);
        var vendor = await f.Db.Vendors.SingleAsync(item => item.Id == registered.User.VendorId);
        var (contract, customer) = await f.AddContract(vendor, categorySlug);
        await new ConfirmContractCommandHandler(f.Db, new Current(customer.Id, "Customer")).Handle(new(contract.Id), default);
        await using var reload = f.OpenContext();
        var confirmed = await reload.BookingContracts.SingleAsync(item => item.Id == contract.Id);
        Assert.Equal(expectedRate, confirmed.CommissionRateSnapshot); Assert.Equal(ContractStatus.Confirmed, confirmed.Status);
        Assert.NotNull(confirmed.ConfirmedAt); Assert.True(confirmed.ConfirmedAt >= confirmed.CreatedAt);
        var commissions = await reload.Commissions.Where(item => item.ContractId == contract.Id).ToListAsync();
        Assert.Equal(2, commissions.Count); Assert.All(commissions, item => Assert.Equal(expectedRate, item.CommissionRate));
        Assert.Equal(100000000m * expectedRate, commissions.Sum(item => item.CommissionAmount));
        Assert.Equal(0m, (await reload.Vendors.SingleAsync(item => item.Id == vendor.Id)).CommissionRate);
    }

    [Fact]
    public async Task FullServiceForNewVendorRequiresApprovedBasisBeforeAnyConfirmationOrCommissionMutation()
    {
        await using var f = await Fixture.Create();
        var registered = await f.Register.Handle(f.Valid() with { BusinessGroupIds = [BusinessGroupPolicy.FullServicePlanningId] }, default);
        var vendor = await f.Db.Vendors.SingleAsync(item => item.Id == registered.User.VendorId);
        var (contract, customer) = await f.AddContract(vendor, "wedding-planner");
        var error = await Assert.ThrowsAsync<DomainException>(() => new ConfirmContractCommandHandler(f.Db,
            new Current(customer.Id, "Customer")).Handle(new(contract.Id), default));
        Assert.Contains("cơ sở", error.Message);
        await AssertPendingAndNoFinancialRows(f, contract.Id);
    }

    [Theory]
    [InlineData("missing-listing")]
    [InlineData("unmapped-category")]
    [InlineData("inactive-category")]
    [InlineData("inactive-group")]
    public async Task CategoryInheritanceCannotFallBackToGuessedRateWhenCategoryPolicyIsMissingOrInactive(string condition)
    {
        await using var f = await Fixture.Create();
        var registered = await f.Register.Handle(f.Valid(), default);
        var vendor = await f.Db.Vendors.SingleAsync(item => item.Id == registered.User.VendorId);
        if (condition == "unmapped-category")
        {
            f.Db.Categories.Add(Category.Create("Unmapped fixture", "unmapped-fixture", null, null, .08m));
            await f.Db.SaveChangesAsync();
        }
        var slug = condition == "missing-listing" ? null : condition == "unmapped-category" ? "unmapped-fixture" : "trang-tri-decor";
        var (contract, customer) = await f.AddContract(vendor, slug);
        if (condition == "inactive-category")
            f.Db.Entry(await f.Db.Categories.SingleAsync(item => item.Slug == slug)).Property(item => item.IsActive).CurrentValue = false;
        if (condition == "inactive-group")
            f.Db.Entry(await f.Db.BusinessGroups.SingleAsync(item => item.Id == BusinessGroupPolicy.DecorInvitationsId)).Property(item => item.IsActive).CurrentValue = false;
        await f.Db.SaveChangesAsync();
        await Assert.ThrowsAsync<DomainException>(() => new ConfirmContractCommandHandler(f.Db,
            new Current(customer.Id, "Customer")).Handle(new(contract.Id), default));
        await AssertPendingAndNoFinancialRows(f, contract.Id);
    }

    [Fact]
    public async Task ExistingPositiveLegacyRateStillOverridesFullServiceCategoryForNewContracts()
    {
        await using var f = await Fixture.Create();
        var owner = User.Create("Synthetic legacy owner", "legacy-owner@example.invalid", "0901234568", "legacy-hash", UserRole.VendorOwner);
        var legacy = Vendor.Create(owner.Id, "Legacy shop", "legacy-shop", "Legacy owner", "0901234568", "Hà Nội", .10m);
        f.Db.Users.Add(owner); f.Db.Vendors.Add(legacy); await f.Db.SaveChangesAsync();
        var (contract, customer) = await f.AddContract(legacy, "wedding-planner");
        await new ConfirmContractCommandHandler(f.Db, new Current(customer.Id, "Customer")).Handle(new(contract.Id), default);
        await using var reload = f.OpenContext();
        Assert.Equal(.10m, (await reload.BookingContracts.SingleAsync(item => item.Id == contract.Id)).CommissionRateSnapshot);
        Assert.Equal(10000000m, (await reload.Commissions.Where(item => item.ContractId == contract.Id).ToListAsync()).Sum(item => item.CommissionAmount));
        Assert.Equal(.10m, (await reload.Vendors.SingleAsync(item => item.Id == legacy.Id)).CommissionRate);
    }

    [Fact]
    public void LegacyFactoryDoesNotAcceptZeroWhileNewFactoryRequiresVersionedAcknowledgement()
    {
        Assert.Throws<DomainException>(() => Vendor.Create(Guid.NewGuid(), "Shop", "shop", "Owner", "0901234567", "Hà Nội", 0));
        Assert.Throws<DomainException>(() => Vendor.Create(Guid.NewGuid(), "Shop", "shop", "Owner", "0901234567", "Hà Nội", .15m));
        Assert.Throws<DomainException>(() => Vendor.CreateWithCategoryDefaults(Guid.NewGuid(), "Shop", "shop", "Owner", "0901234567", "Hà Nội", ""));
        Assert.Throws<DomainException>(() => VendorBusinessGroup.Create(Guid.Empty, BusinessGroupPolicy.DecorInvitationsId));
        Assert.Throws<DomainException>(() => VendorBusinessGroup.Create(Guid.NewGuid(), Guid.Empty));
    }

    [Fact]
    public void PublicPolicyEndpointIsAnonymousButOwnMembershipEndpointIsVendorOnly()
    {
        var controller = typeof(BusinessGroupsController);
        Assert.Equal("api/business-groups", controller.GetCustomAttribute<RouteAttribute>()?.Template);
        Assert.NotNull(controller.GetMethod(nameof(BusinessGroupsController.Get))!.GetCustomAttribute<AllowAnonymousAttribute>());
        var own = controller.GetMethod(nameof(BusinessGroupsController.My))!;
        Assert.Null(own.GetCustomAttribute<AllowAnonymousAttribute>());
        Assert.Equal("VendorOwner", Assert.Single(own.GetCustomAttributes<AuthorizeAttribute>()).Roles);
        Assert.Empty(typeof(GetMyBusinessGroupsQuery).GetProperties());
    }

    [SqlServerFact]
    public async Task SqlUpgradePreservesLegacyVendorAndContractSnapshotWithoutGuessingMemberships()
    {
        var database = $"WeddingPlanTest_BusinessGroups_{Guid.NewGuid():N}";
        var source = Environment.GetEnvironmentVariable("WEDDING_TEST_SQLSERVER");
        var builder = new SqlConnectionStringBuilder(string.IsNullOrWhiteSpace(source) || source == "1"
            ? "Server=localhost;Integrated Security=true;TrustServerCertificate=true;Connection Timeout=5" : source)
            { InitialCatalog = database };
        await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlServer(builder.ConnectionString).Options);
        try
        {
            await db.GetService<IMigrator>().MigrateAsync("20261018022533_NotificationCenter");
            var oldCategoryIds = await db.Database.SqlQueryRaw<Guid>("SELECT [Id] AS [Value] FROM [Categories]").ToListAsync();
            Assert.Equal(7, oldCategoryIds.Count);
            var ownerId = Guid.NewGuid(); var customerId = Guid.NewGuid(); var vendorId = Guid.NewGuid();
            var leadId = Guid.NewGuid(); var contractId = Guid.NewGuid(); var version = Guid.NewGuid();
            var createdAt = DateTime.UtcNow.AddDays(-10); var confirmedAt = createdAt.AddDays(1); var weddingDate = DateTime.UtcNow.AddDays(100);
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO Users (Id,FullName,Email,PhoneNumber,PasswordHash,SecurityVersion,Role,IsActive,CreatedAt)
                VALUES ({ownerId},{"Synthetic legacy owner"},{"legacy-owner@example.invalid"},{"0901234568"},{"unchanged-owner-hash"},{3},{"VendorOwner"},{true},{createdAt});
                INSERT INTO Users (Id,FullName,Email,PhoneNumber,PasswordHash,SecurityVersion,Role,IsActive,CreatedAt)
                VALUES ({customerId},{"Synthetic legacy customer"},{"legacy-customer@example.invalid"},{"0901234569"},{"unchanged-customer-hash"},{2},{"Customer"},{true},{createdAt});
                INSERT INTO Vendors (Id,UserId,BrandName,Slug,ContactPerson,Hotline,City,CommissionRate,RatingAvg,TotalReviews,IsActive,IsVerified,IsTopRated,AllowInCombos,MaxComboDiscount,CreatedAt)
                VALUES ({vendorId},{ownerId},{"Synthetic legacy shop"},{"synthetic-legacy-shop"},{"Legacy owner"},{"0901234568"},{"Bình Dương"},{.10m},{0m},{0},{true},{false},{false},{false},{0m},{createdAt});
                INSERT INTO Leads (Id,CustomerId,VendorId,RawPhoneNumber,MaskedPhoneNumber,IsPhoneUnlocked,Status,SlaDeadline,CreatedAt,Source)
                VALUES ({leadId},{customerId},{vendorId},{"0901234569"},{"0901***569"},{true},{"Contracted"},{createdAt.AddHours(2)},{createdAt},{"Listing"});
                INSERT INTO BookingContracts (Id,ContractCode,LeadId,VendorId,CustomerId,ContractValue,DepositAmount,WeddingDate,Status,VerificationDeadline,ConfirmedAt,CommissionRateSnapshot,CancellationPolicySnapshot,RevisionCount,Version,AppointmentDepositCredit,CreatedAt)
                VALUES ({contractId},{"LEGACY-PHASE2-SNAPSHOT"},{leadId},{vendorId},{customerId},{100000000m},{30000000m},{weddingDate},{"Confirmed"},{createdAt.AddHours(72)},{confirmedAt},{.10m},{"Unchanged legacy cancellation agreement"},{3},{version},{1000000m},{createdAt});
                """);

            await db.Database.MigrateAsync(); await db.Database.MigrateAsync();
            Assert.False(db.Database.HasPendingModelChanges()); Assert.Empty(await db.Database.GetPendingMigrationsAsync());
            var owner = await db.Users.SingleAsync(item => item.Id == ownerId);
            Assert.Equal("unchanged-owner-hash", owner.PasswordHash); Assert.Equal(3, owner.SecurityVersion); Assert.Equal(UserRole.VendorOwner, owner.Role);
            var customer = await db.Users.SingleAsync(item => item.Id == customerId);
            Assert.Equal("unchanged-customer-hash", customer.PasswordHash); Assert.Equal(2, customer.SecurityVersion);
            var legacy = await db.Vendors.SingleAsync(item => item.Id == vendorId);
            Assert.Equal(.10m, legacy.CommissionRate); Assert.Equal("Bình Dương", legacy.City);
            Assert.Null(legacy.PartnershipPolicyAcceptedAt); Assert.Null(legacy.PartnershipPolicyVersion);
            Assert.Empty(await db.VendorBusinessGroups.ToListAsync());
            var contract = await db.BookingContracts.SingleAsync(item => item.Id == contractId);
            Assert.Equal(.10m, contract.CommissionRateSnapshot); Assert.Equal(confirmedAt, contract.ConfirmedAt);
            Assert.Equal(100000000m, contract.ContractValue); Assert.Equal(30000000m, contract.DepositAmount);
            Assert.Equal(1000000m, contract.AppointmentDepositCredit); Assert.Equal(3, contract.RevisionCount);
            Assert.Equal(version, contract.Version); Assert.Equal("Unchanged legacy cancellation agreement", contract.CancellationPolicySnapshot);
            Assert.Equal(ContractStatus.Confirmed, contract.Status); Assert.Empty(await db.Commissions.ToListAsync());
            var categoryIds = await db.Categories.Select(item => item.Id).ToListAsync();
            Assert.Equal(8, categoryIds.Count);
            Assert.Equal(oldCategoryIds.Order(), categoryIds.Where(id => oldCategoryIds.Contains(id)).Order());
            Assert.Equal(Guid.Parse("11111111-1111-1111-1111-111111111108"), Assert.Single(categoryIds.Except(oldCategoryIds)));
            Assert.Equal(5, await db.BusinessGroups.CountAsync());
            var own = await new GetMyBusinessGroupsQueryHandler(db, new Current(ownerId)).Handle(new(), default);
            Assert.True(own.UsesLegacyCommissionRate); Assert.Equal(.10m, own.LegacyCommissionRate); Assert.Empty(own.Groups);

            var signup = await new RegisterVendorCommandHandler(db, new Hasher(), new Jwt()).Handle(new(
                "SQL new owner", "new-sql-owner@example.invalid", "0901234570", Password, "New SQL shop", "HCM",
                BusinessGroupIds: [BusinessGroupPolicy.DecorInvitationsId, BusinessGroupPolicy.PhotoBridalMakeupId], PartnershipPolicyAccepted: true), default);
            db.ChangeTracker.Clear();
            Assert.Equal(0m, (await db.Vendors.SingleAsync(item => item.Id == signup.User.VendorId)).CommissionRate);
            Assert.Empty(await db.VendorBusinessGroups.Where(item => item.VendorId == signup.User.VendorId).ToListAsync());
            Assert.Equal(BusinessGroupPolicy.ServiceCategoryRegistrationVersion, (await db.Vendors.SingleAsync(item => item.Id == signup.User.VendorId)).PartnershipPolicyVersion);
            Assert.Empty(await db.VendorBusinessGroups.Where(item => item.VendorId == vendorId).ToListAsync());
            Assert.Equal(.10m, (await db.BookingContracts.SingleAsync(item => item.Id == contractId)).CommissionRateSnapshot);
        }
        finally
        {
            Assert.StartsWith("WeddingPlanTest_BusinessGroups_", database);
            await db.Database.EnsureDeletedAsync();
        }
    }

    private static async Task AssertPendingAndNoFinancialRows(Fixture fixture, Guid contractId)
    {
        await using var reload = fixture.OpenContext();
        var persisted = await reload.BookingContracts.SingleAsync(item => item.Id == contractId);
        Assert.Equal(ContractStatus.PendingVerification, persisted.Status);
        Assert.Null(persisted.ConfirmedAt); Assert.Null(persisted.CommissionRateSnapshot);
        Assert.Empty(await reload.Commissions.ToListAsync()); Assert.Empty(await reload.BudgetItems.ToListAsync());
        Assert.Empty(await reload.ChecklistTasks.ToListAsync());
    }

    private sealed record Current(Guid? UserId, string? Role = "VendorOwner") : ICurrentUserService { public string? Email => null; }
    private sealed class Hasher : IPasswordHasher
    {
        public string Hash(string password) => "synthetic-hash:" + password;
        public bool Verify(string password, string passwordHash) => passwordHash == Hash(password);
    }
    private sealed class Jwt : IJwtProvider { public string GenerateToken(User user) => $"synthetic-token:{user.Id}"; }
    private sealed class FailingJwt : IJwtProvider { public string GenerateToken(User user) => throw new InvalidOperationException("Synthetic token failure for rollback verification."); }

    private sealed class Fixture(SqliteConnection connection, ApplicationDbContext db) : IAsyncDisposable
    {
        public ApplicationDbContext Db { get; } = db;
        public Hasher Hasher { get; } = new();
        public RegisterVendorCommandHandler Register => new(Db, Hasher, new Jwt());
        public RegisterVendorCommand Valid(string email = "new-owner@example.invalid") => new("Synthetic owner", email,
            "0901234567", Password, "Synthetic shop", "HCM", PartnershipPolicyAccepted: true);
        public ApplicationDbContext OpenContext() => new(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options);
        public static async Task<Fixture> Create()
        {
            var connection = new SqliteConnection("Data Source=:memory:"); await connection.OpenAsync();
            var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options);
            await db.Database.EnsureCreatedAsync(); return new(connection, db);
        }
        public async Task<(BookingContract Contract, User Customer)> AddContract(Vendor vendor, string? categorySlug)
        {
            var customer = User.Create("Synthetic couple", $"couple-{Guid.NewGuid():N}@example.invalid", "0901234569", "synthetic-hash", UserRole.Customer);
            Db.Users.Add(customer);
            Listing? listing = null;
            if (categorySlug != null)
            {
                var category = await Db.Categories.SingleAsync(item => item.Slug == categorySlug);
                listing = Listing.Create(vendor.Id, category.Id, "Synthetic service", $"synthetic-service-{Guid.NewGuid():N}",
                    1000000, 100000000, "Synthetic service for commission tests", "Hà Nội");
                Db.Listings.Add(listing);
            }
            var weddingDate = DateTime.UtcNow.AddDays(100);
            var lead = Lead.Create(customer.Id, vendor.Id, listing?.Id, "0901234569", weddingDate, 100, 100000000, "Synthetic request");
            lead.Accept(); Db.Leads.Add(lead);
            var contract = BookingContract.Create(lead.Id, null, vendor.Id, customer.Id, 100000000, 30000000, null, weddingDate);
            Db.BookingContracts.Add(contract); await Db.SaveChangesAsync(); return (contract, customer);
        }
        public async ValueTask DisposeAsync() { await Db.DisposeAsync(); await connection.DisposeAsync(); }
    }
}
