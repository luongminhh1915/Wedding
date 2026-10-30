using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Wedding.Application.Common.Interfaces;
using Wedding.Application.Features.Auth.Commands;
using Wedding.Application.Features.Users.Commands.CreateStaffAccount;
using Wedding.Application.Features.Users.Commands.CreateUser;
using Wedding.Domain.Entities;
using Wedding.Domain.Enums;
using Wedding.Domain.Exceptions;
using Wedding.Infrastructure.Persistence;
using Wedding.WebApi.Controllers;

namespace Wedding.Tests;

public sealed class AdminUserCreationTests
{
    private const string Password = "Synthetic-Admin-2026";
    private static CreateUserCommand Input(string role, string email = "new-user@admin-create.example.invalid")
        => new(" New account ", email, "0901234567", Password, role,
            role == "VendorOwner" ? "Đám Cưới Đẹp" : null, role == "VendorOwner" ? " HCM " : null,
            ConfirmSuperAdminCreation: role == "SuperAdmin");

    [Theory]
    [InlineData("Customer")]
    [InlineData("VendorOwner")]
    [InlineData("Moderator")]
    [InlineData("Finance")]
    [InlineData("CustomerCare")]
    [InlineData("SuperAdmin")]
    public async Task ActiveSuperAdminCanCreateEveryRoleWithoutReturningCredentialsOrReplacingItsIdentity(string role)
    {
        await using var f = await Fixture.Create();
        var actor = new Current(f.Admin.Id, "SuperAdmin");
        var command = Input(role) with { Email = " NEW-USER@ADMIN-CREATE.EXAMPLE.INVALID " };
        var result = await new CreateUserCommandHandler(f.Db, actor, f.Hasher).Handle(command, default);

        Assert.Equal(role, result.Role);
        Assert.Equal("new-user@admin-create.example.invalid", result.Email);
        Assert.Equal("New account", result.FullName);
        Assert.True(result.IsActive);
        Assert.Null(result.LastLoginAt);
        Assert.Equal(f.Admin.Id, actor.UserId);
        Assert.Equal("SuperAdmin", actor.Role);
        var user = await f.Db.Users.SingleAsync(user => user.Id == result.Id);
        Assert.NotEqual(command.Password, user.PasswordHash);
        Assert.True(f.Hasher.Verify(command.Password, user.PasswordHash));
        var response = System.Text.Json.JsonSerializer.Serialize(result);
        Assert.DoesNotContain("Password", response);
        Assert.DoesNotContain("Token", response);
        Assert.DoesNotContain(command.Password, response);
        Assert.DoesNotContain(user.PasswordHash, response);
        Assert.DoesNotContain(command.Password, command.ToString());
        Assert.DoesNotContain(command.Email, command.ToString());

        var audit = await f.Db.AuditLogs.SingleAsync(log => log.Action == "User.Create");
        Assert.Equal(f.Admin.Id, audit.ActorId);
        Assert.Equal("SuperAdmin", audit.ActorRole);
        Assert.Equal(result.Id.ToString(), audit.EntityId);
        Assert.Contains(role, audit.DataJson!);
        foreach (var privateValue in new[] { command.Password, user.PasswordHash, user.Email, user.PhoneNumber, user.FullName })
            Assert.DoesNotContain(privateValue, audit.DataJson!);

        var login = await new LoginCommandHandler(f.Db, f.Hasher, new Jwt()).Handle(new(result.Email, command.Password), default);
        Assert.Equal(result.Id, login.User.Id);
        Assert.Equal(role, login.User.Role);
        Assert.Equal(result.VendorId, login.User.VendorId);
        Assert.Equal(UserRole.SuperAdmin, (await f.Db.Users.SingleAsync(user => user.Id == f.Admin.Id)).Role);
        Assert.Equal(2, await f.Db.Users.CountAsync());
        Assert.Equal(role == "VendorOwner" ? 1 : 0, await f.Db.Vendors.CountAsync());
    }

    [Theory]
    [InlineData("Customer")]
    [InlineData("VendorOwner")]
    [InlineData("Moderator")]
    [InlineData("Finance")]
    [InlineData("CustomerCare")]
    [InlineData(null)]
    public async Task OtherClaimedRolesCannotProvisionAnyAccount(string? claimedRole)
    {
        await using var f = await Fixture.Create();
        await Assert.ThrowsAsync<DomainException>(() => new CreateUserCommandHandler(f.Db,
            new Current(f.Admin.Id, claimedRole), f.Hasher).Handle(Input("Customer"), default));
        Assert.Single(await f.Db.Users.ToListAsync());
        Assert.Empty(await f.Db.AuditLogs.ToListAsync());
    }

    [Theory]
    [InlineData("inactive")]
    [InlineData("wrong-role")]
    [InlineData("missing")]
    [InlineData("anonymous")]
    public async Task DatabaseActorIsRecheckedAndRejectsStaleOrMissingAdministrator(string state)
    {
        await using var f = await Fixture.Create();
        Guid? actorId = f.Admin.Id;
        if (state == "inactive") f.Admin.SetStatus(false);
        if (state == "wrong-role")
        {
            var customer = User.Create("Customer", "customer@admin-create.example.invalid", "0901234568", "synthetic-hash", UserRole.Customer);
            f.Db.Users.Add(customer);
            actorId = customer.Id;
        }
        if (state == "missing") actorId = Guid.NewGuid();
        if (state == "anonymous") actorId = null;
        await f.Db.SaveChangesAsync();
        var before = await f.Db.Users.CountAsync();
        await Assert.ThrowsAsync<DomainException>(() => new CreateUserCommandHandler(f.Db,
            new Current(actorId, "SuperAdmin"), f.Hasher).Handle(Input("Customer"), default));
        Assert.Equal(before, await f.Db.Users.CountAsync());
        Assert.Empty(await f.Db.Vendors.ToListAsync());
        Assert.Empty(await f.Db.AuditLogs.ToListAsync());
    }

    [Fact]
    public async Task SuperAdminCreationRequiresExplicitGrantConfirmationAndStrongNewPassword()
    {
        await using var f = await Fixture.Create();
        var handler = f.Handler;
        await Assert.ThrowsAsync<DomainException>(() => handler.Handle(Input("SuperAdmin") with { ConfirmSuperAdminCreation = false }, default));
        await Assert.ThrowsAsync<DomainException>(() => handler.Handle(Input("SuperAdmin") with { Password = "12345678901" }, default));
        await Assert.ThrowsAsync<DomainException>(() => handler.Handle(Input("SuperAdmin") with { Password = new string('ầ', 25) }, default));
        Assert.Single(await f.Db.Users.ToListAsync());
        Assert.Empty(await f.Db.AuditLogs.ToListAsync());
        var created = await handler.Handle(Input("SuperAdmin") with { Password = "123456789012" }, default);
        Assert.Equal("SuperAdmin", created.Role);
        Assert.Contains("\"SuperAdminGrantConfirmed\":true", (await f.Db.AuditLogs.SingleAsync()).DataJson!);
    }

    [Fact]
    public async Task VendorCreationUsesOneShopUniqueCanonicalSlugAndNeverImpersonatesTermsOrFinancialApproval()
    {
        await using var f = await Fixture.Create();
        var first = await f.Handler.Handle(Input("VendorOwner"), default);
        var second = await f.Handler.Handle(Input("VendorOwner", "second@admin-create.example.invalid"), default);
        Assert.NotNull(first.VendorId);
        Assert.Equal("Đám Cưới Đẹp", first.VendorBrandName);
        var firstShop = await f.Db.Vendors.SingleAsync(vendor => vendor.Id == first.VendorId);
        var secondShop = await f.Db.Vendors.SingleAsync(vendor => vendor.Id == second.VendorId);
        Assert.Equal(first.Id, firstShop.UserId);
        Assert.Equal("dam-cuoi-dep", firstShop.Slug);
        Assert.Equal("dam-cuoi-dep-1", secondShop.Slug);
        Assert.Equal("Hồ Chí Minh", firstShop.City);
        Assert.All(new[] { firstShop, secondShop }, shop =>
        {
            Assert.True(shop.IsActive);
            Assert.False(shop.IsVerified);
            Assert.Equal(0m, shop.CommissionRate);
            Assert.Null(shop.PartnershipPolicyAcceptedAt);
            Assert.Null(shop.PartnershipPolicyVersion);
        });
        Assert.Empty(await f.Db.VendorBusinessGroups.ToListAsync());
        Assert.Empty(await f.Db.BookingContracts.ToListAsync());
        Assert.Empty(await f.Db.Commissions.ToListAsync());
        Assert.Empty(await f.Db.CommissionAdjustmentRequests.ToListAsync());
        Assert.Empty(await f.Db.CommissionPolicyRules.ToListAsync());
    }

    [Fact]
    public async Task BadInputCannotCreatePartialShopAndExistingEmailIsNeverPromotedOrModified()
    {
        await using var f = await Fixture.Create();
        var invalid = new[]
        {
            Input("VendorOwner") with { BrandName = new string('x', 201) },
            Input("VendorOwner") with { City = "unknown-province" },
            Input("Customer") with { FullName = " " },
            Input("Customer") with { Email = "Name <new@example.invalid>" },
            Input("Customer") with { PhoneNumber = "0123" },
            Input("Customer") with { Password = "1234567" },
            Input("Customer") with { Role = "superadmin" },
            Input("Customer") with { Role = "6" },
            Input("Customer") with { Role = "Unknown" }
        };
        foreach (var request in invalid) await Assert.ThrowsAsync<DomainException>(() => f.Handler.Handle(request, default));
        var existing = User.Create("Unchanged Customer", "existing@admin-create.example.invalid", "0901111111", "original-hash", UserRole.Customer);
        f.Db.Users.Add(existing);
        await f.Db.SaveChangesAsync();
        await Assert.ThrowsAsync<DomainException>(() => f.Handler.Handle(Input("SuperAdmin", " EXISTING@ADMIN-CREATE.EXAMPLE.INVALID "), default));
        Assert.Equal(UserRole.Customer, existing.Role);
        Assert.Equal("original-hash", existing.PasswordHash);
        Assert.True(existing.IsActive);
        Assert.Equal(2, await f.Db.Users.CountAsync());
        Assert.Empty(await f.Db.Vendors.ToListAsync());
        Assert.Empty(await f.Db.AuditLogs.ToListAsync());
    }

    [Fact]
    public async Task VendorWithOnlyNameAndPasswordHasNoInventedContactsAndCanLoginUsingReturnedAlias()
    {
        await using var f = await Fixture.Create();
        var result = await f.Handler.Handle(new(" Cưới Xinh ", null, null, Password, "VendorOwner"), default);
        Assert.Equal("cuoi-xinh", result.LoginName);
        Assert.Equal("Cưới Xinh", result.FullName);
        Assert.Equal("Cưới Xinh", result.VendorBrandName);
        Assert.Empty(result.Email);
        Assert.Empty(result.PhoneNumber);
        var persisted = await f.Db.Users.Include(user => user.Vendor).SingleAsync(user => user.Id == result.Id);
        Assert.Null(f.Db.Entry(persisted).Property<string?>("Email").CurrentValue);
        Assert.Equal(result.Id.ToString(), persisted.NotificationRecipient);
        Assert.Empty(persisted.Vendor!.City);
        Assert.Empty(persisted.Vendor.Hotline);
        Assert.False(persisted.Vendor.IsVerified);
        Assert.Null(persisted.Vendor.PartnershipPolicyAcceptedAt);

        var login = await new LoginCommandHandler(f.Db, f.Hasher, new Jwt())
            .Handle(new(" CUOI-XINH ", Password), default);
        Assert.Equal(result.Id, login.User.Id);
        Assert.Equal("VendorOwner", login.User.Role);
        Assert.Equal(result.LoginName, login.User.LoginName);

        await Assert.ThrowsAsync<DomainException>(() => new LoginCommandHandler(f.Db, f.Hasher, new Jwt())
            .Handle(new(" Cưới Xinh ", Password), default));
        await Assert.ThrowsAsync<DomainException>(() => new LoginCommandHandler(f.Db, f.Hasher, new Jwt())
            .Handle(new("cưới xinh", Password), default));
        var me = await new Wedding.Application.Features.Auth.Queries.GetCurrentUserQueryHandler(f.Db)
            .Handle(new(result.Id), default);
        Assert.Equal(result.LoginName, me.LoginName);
        var users = await new Wedding.Application.Features.Users.Queries.GetUsers.GetUsersQueryHandler(f.Db)
            .Handle(new(), default);
        Assert.Equal(result.LoginName, users.Single(user => user.Id == result.Id).LoginName);
        Assert.DoesNotContain(Password, System.Text.Json.JsonSerializer.Serialize(result));
    }

    [Fact]
    public async Task ExactVendorAliasCannotBeShadowedByAnotherRolesDisplayName()
    {
        await using var f = await Fixture.Create();
        var vendor = await f.Handler.Handle(new("Cưới Xinh", null, null, Password, "VendorOwner"), default);
        var customer = User.Create("cuoi-xinh", "alias-collision@example.invalid", "0900000901", f.Hasher.Hash(Password), UserRole.Customer);
        f.Db.Users.Add(customer);
        await f.Db.SaveChangesAsync();
        var login = new LoginCommandHandler(f.Db, f.Hasher, new Jwt());
        Assert.Equal(vendor.Id, (await login.Handle(new("CUOI-XINH", Password), default)).User.Id);
        Assert.Equal(customer.Id, (await login.Handle(new(customer.Email, Password), default)).User.Id);
        customer.UpdateProfile("Another display name", null);
        await f.Db.SaveChangesAsync();
        await Assert.ThrowsAsync<DomainException>(() => login.Handle(new("Another display name", Password), default));
        await Assert.ThrowsAsync<DomainException>(() => login.Handle(new(f.Admin.FullName, Password), default));
    }

    [Fact]
    public async Task VendorOperatingAreasRoundTripThroughCreateAndManagementAndMissingEmailKeepsInAppNotifications()
    {
        await using var f = await Fixture.Create();
        var regions = await f.Handler.Handle(new("Multiple Areas", null, null, Password, "VendorOwner",
            OperatingRegions: ["hn", "Da Nang", "Hà Nội"]), default);
        Assert.Equal(new[] { "Hà Nội", "Đà Nẵng" }, regions.VendorOperatingRegions);
        Assert.False(regions.VendorNationwide);
        var nationwide = await f.Handler.Handle(new("All Areas", null, null, Password, "VendorOwner", Nationwide: true), default);
        Assert.True(nationwide.VendorNationwide);
        Assert.Empty(nationwide.VendorOperatingRegions!);
        var listed = await new Wedding.Application.Features.Users.Queries.GetUsers.GetUsersQueryHandler(f.Db).Handle(new(), default);
        Assert.Equal(regions.VendorOperatingRegions, listed.Single(user => user.Id == regions.Id).VendorOperatingRegions);
        Assert.True(listed.Single(user => user.Id == nationwide.Id).VendorNationwide);
        var persisted = await f.Db.Users.SingleAsync(user => user.Id == regions.Id);
        var sender = new Wedding.Infrastructure.Services.DurableNotificationSender(f.Db);
        await sender.SendAsync("Email", persisted.NotificationRecipient, "Synthetic vendor reminder");
        var notification = await f.Db.Notifications.SingleAsync();
        Assert.Equal(regions.Id, notification.UserId);
        Assert.Equal("InApp", notification.Channel);
        Assert.Empty(notification.Destination);
    }

    [Fact]
    public async Task VendorAliasesAreUniqueBoundedAndStableAfterDisplayNameChanges()
    {
        await using var f = await Fixture.Create();
        var first = await f.Handler.Handle(new("Cưới Xinh", "", "", Password, "VendorOwner"), default);
        var second = await f.Handler.Handle(new("Cưới Xinh", "", "", Password, "VendorOwner"), default);
        Assert.Equal("cuoi-xinh", first.LoginName);
        Assert.Equal("cuoi-xinh-1", second.LoginName);
        var user = await f.Db.Users.SingleAsync(user => user.Id == first.Id);
        user.UpdateProfile("Changed display name", null);
        await f.Db.SaveChangesAsync();
        Assert.Equal(first.LoginName, user.LoginName);
        var longName = await f.Handler.Handle(new(new string('x', 150), null, null, Password, "VendorOwner"), default);
        Assert.True(User.IsValidLoginName(longName.LoginName));
        Assert.InRange(longName.LoginName!.Length, 1, 80);
        Assert.Equal(4, await f.Db.Users.CountAsync());
    }

    [Theory]
    [InlineData("Customer")]
    [InlineData("Moderator")]
    [InlineData("Finance")]
    [InlineData("CustomerCare")]
    [InlineData("SuperAdmin")]
    public async Task OptionalContactExceptionNeverAppliesToOtherRoles(string role)
    {
        await using var f = await Fixture.Create();
        await Assert.ThrowsAsync<DomainException>(() => f.Handler.Handle(Input(role) with { Email = null }, default));
        await Assert.ThrowsAsync<DomainException>(() => f.Handler.Handle(Input(role) with { PhoneNumber = null }, default));
        Assert.Single(await f.Db.Users.ToListAsync());
        Assert.Empty(await f.Db.AuditLogs.ToListAsync());
    }

    [Theory]
    [InlineData("Name <shop@example.invalid>", "0901234567")]
    [InlineData("not-an-email", "0901234567")]
    [InlineData("shop@example.invalid", "123")]
    public async Task OptionalVendorContactsAreStrictlyValidatedWhenProvided(string email, string phone)
    {
        await using var f = await Fixture.Create();
        await Assert.ThrowsAsync<DomainException>(() => f.Handler.Handle(new("Shop", email, phone, Password, "VendorOwner"), default));
        Assert.Single(await f.Db.Users.ToListAsync());
        Assert.Empty(await f.Db.Vendors.ToListAsync());
    }

    [Fact]
    public async Task AliasLoginRejectsWrongPasswordAndInactiveVendorWithoutChangingIdentity()
    {
        await using var f = await Fixture.Create();
        var created = await f.Handler.Handle(new("Shop", null, null, Password, "VendorOwner"), default);
        var handler = new LoginCommandHandler(f.Db, f.Hasher, new Jwt());
        await Assert.ThrowsAsync<DomainException>(() => handler.Handle(new(created.LoginName!, "wrong-password"), default));
        var user = await f.Db.Users.SingleAsync(user => user.Id == created.Id);
        Assert.Null(user.LastLoginAt);
        user.SetStatus(false);
        await f.Db.SaveChangesAsync();
        await Assert.ThrowsAsync<DomainException>(() => handler.Handle(new(created.LoginName!, Password), default));
        Assert.Null(user.LastLoginAt);
        Assert.Equal(UserRole.SuperAdmin, f.Admin.Role);
    }

    [SqlServerFact]
    public async Task ActualSqlOptionalVendorContactAndUniqueAliasAreEnforcedWithoutRelaxingStaffEmail()
    {
        // Fresh owned GUID database; current model is tested independently of migration generation.
        await using var owned = await CommissionSqlTestDatabase.CreateAsync(migrate: false);
        await owned.Db.Database.EnsureCreatedAsync();
        var hasher = new Hasher();
        var admin = User.Create("Admin", "admin@optional-vendor.example.invalid", "0901234567", hasher.Hash(Password), UserRole.SuperAdmin);
        owned.Db.Users.Add(admin);
        await owned.Db.SaveChangesAsync();
        var handler = new CreateUserCommandHandler(owned.Db, new Current(admin.Id, "SuperAdmin"), hasher);
        var first = await handler.Handle(new("Vendor One", null, null, Password, "VendorOwner"), default);
        var second = await handler.Handle(new("Vendor One", null, null, Password, "VendorOwner"), default);
        Assert.NotEqual(first.LoginName, second.LoginName);
        owned.Db.ChangeTracker.Clear();
        Assert.Equal(2, await owned.Db.Users.CountAsync(user => user.Email == null && user.Role == UserRole.VendorOwner));
        var loggedIn = await new LoginCommandHandler(owned.Db, hasher, new Jwt()).Handle(new(first.LoginName!, Password), default);
        Assert.Equal(first.Id, loggedIn.User.Id);

        await using (var duplicate = owned.CreateContext())
        {
            duplicate.Users.Add(User.CreateAdministrativeVendor("Duplicate", null, null, hasher.Hash(Password), first.LoginName!));
            await Assert.ThrowsAsync<DbUpdateException>(() => duplicate.SaveChangesAsync());
        }
        await Assert.ThrowsAsync<Microsoft.Data.SqlClient.SqlException>(() => owned.Db.Database.ExecuteSqlRawAsync(
            "UPDATE [Users] SET [Email]=NULL WHERE [Id]={0}", admin.Id));
        Assert.Equal("admin@optional-vendor.example.invalid", (await owned.Db.Users.AsNoTracking().SingleAsync(user => user.Id == admin.Id)).Email);
    }

    [Fact]
    public async Task FailureAfterSavingProvisioningRollsBackUserShopAndAuditTogether()
    {
        var failure = new SaveFailure();
        await using var f = await Fixture.Create(failure);
        failure.Armed = true;
        await Assert.ThrowsAsync<InvalidOperationException>(() => f.Handler.Handle(Input("VendorOwner"), default));
        Assert.Single(await f.Db.Users.ToListAsync());
        Assert.Empty(await f.Db.Vendors.ToListAsync());
        Assert.Empty(await f.Db.AuditLogs.ToListAsync());
    }

    [Fact]
    public void CanonicalHttpCreateIsSuperAdminOnlyAndLegacyStaffEndpointRemainsCompatible()
    {
        var method = typeof(UsersController).GetMethod(nameof(UsersController.CreateUser))!;
        Assert.Equal("SuperAdmin", method.GetCustomAttributes(typeof(AuthorizeAttribute), true).Cast<AuthorizeAttribute>().Single().Roles);
        Assert.Null(method.GetCustomAttributes(typeof(HttpPostAttribute), true).Cast<HttpPostAttribute>().Single().Template);
        var legacy = new CreateStaffAccountCommandValidator();
        foreach (var role in new[] { "SuperAdmin", "Customer", "VendorOwner" })
            Assert.False(legacy.Validate(new CreateStaffAccountCommand("Name", "old@admin-create.example.invalid", "0901234567", Password, role)).IsValid);
        foreach (var role in new[] { "Moderator", "Finance", "CustomerCare" })
            Assert.True(legacy.Validate(new CreateStaffAccountCommand("Name", "old@admin-create.example.invalid", "0901234567", Password, role)).IsValid);
    }

    [SqlServerFact]
    public async Task ActualSqlConcurrentProvisioningIsUniqueAtomicAndDoesNotMutateExistingAdmin()
    {
        await using var owned = await CommissionSqlTestDatabase.CreateAsync();
        var hasher = new Hasher();
        var admin = User.Create("SQL Admin", "admin@admin-create-sql.example.invalid", "0901234567", hasher.Hash(Password), UserRole.SuperAdmin);
        owned.Db.Users.Add(admin);
        await owned.Db.SaveChangesAsync();
        var results = await Task.WhenAll(Enumerable.Range(0, 4).Select(async _ =>
        {
            await using var context = owned.CreateContext();
            try
            {
                await new CreateUserCommandHandler(context, new Current(admin.Id, "SuperAdmin"), hasher)
                    .Handle(Input("VendorOwner", "same@admin-create-sql.example.invalid"), default);
                return true;
            }
            catch (DomainException) { return false; }
        }));
        Assert.Equal(1, results.Count(success => success));
        await using var check = owned.CreateContext();
        Assert.Equal(2, await check.Users.CountAsync());
        Assert.Single(await check.Vendors.ToListAsync());
        Assert.Single(await check.AuditLogs.Where(log => log.Action == "User.Create").ToListAsync());
        var preserved = await check.Users.SingleAsync(user => user.Id == admin.Id);
        Assert.Equal(UserRole.SuperAdmin, preserved.Role);
        Assert.Equal(hasher.Hash(Password), preserved.PasswordHash);
        Assert.Null((await check.Vendors.SingleAsync()).PartnershipPolicyAcceptedAt);
    }

    private sealed class Fixture(SqliteConnection connection, ApplicationDbContext db, User admin) : IAsyncDisposable
    {
        public ApplicationDbContext Db { get; } = db;
        public User Admin { get; } = admin;
        public Hasher Hasher { get; } = new();
        public CreateUserCommandHandler Handler => new(Db, new Current(Admin.Id, "SuperAdmin"), Hasher);
        public static async Task<Fixture> Create(SaveFailure? failure = null)
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection);
            if (failure != null) options.AddInterceptors(failure);
            var db = new ApplicationDbContext(options.Options);
            await db.Database.EnsureCreatedAsync();
            var admin = User.Create("Unchanged Admin", "admin@admin-create.example.invalid", "0901234567", "admin-original-hash", UserRole.SuperAdmin);
            db.Users.Add(admin);
            await db.SaveChangesAsync();
            return new(connection, db, admin);
        }
        public async ValueTask DisposeAsync() { await Db.DisposeAsync(); await connection.DisposeAsync(); }
    }
    private sealed record Current(Guid? UserId, string? Role) : ICurrentUserService { public string? Email => null; }
    private sealed class Hasher : IPasswordHasher
    {
        public string Hash(string password) => "synthetic-hash:" + password;
        public bool Verify(string password, string hash) => Hash(password) == hash;
    }
    private sealed class Jwt : IJwtProvider { public string GenerateToken(User user) => "synthetic-issued-session"; }
    private sealed class SaveFailure : SaveChangesInterceptor
    {
        public bool Armed { get; set; }
        public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result, CancellationToken ct = default)
        {
            if (Armed) throw new InvalidOperationException("Synthetic post-save failure");
            return ValueTask.FromResult(result);
        }
    }
}
