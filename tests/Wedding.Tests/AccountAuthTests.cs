using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.Data.SqlClient;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
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
using Wedding.WebApi.Controllers;

namespace Wedding.Tests;

public class AccountAuthTests
{
    private const string Password = "A long test passphrase";
    private static readonly Guid DecorGroupId = Guid.Parse("22222222-2222-2222-2222-222222222202");

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
        await Assert.ThrowsAsync<DomainException>(() => f.Vendor.Handle(new("Other", "couple@example.invalid", "0901234568", Password, "Studio", "HCM", BusinessGroupIds: [DecorGroupId], PartnershipPolicyAccepted: true), default));
        Assert.Single(await f.Db.Users.ToListAsync()); Assert.Empty(await f.Db.Vendors.ToListAsync());
    }

    [Fact]
    public async Task VendorRegistrationCreatesAtomicOneToOneProfileAndUniqueVietnameseSlug()
    {
        await using var f = await Fixture.Create();
        var first = await f.Vendor.Handle(new(" Đại diện ", "OWNER@EXAMPLE.INVALID", "0901234567", Password, " Đám Cưới Đẹp ", " HCM ", BusinessGroupIds: [DecorGroupId], PartnershipPolicyAccepted: true), default);
        var second = await f.Vendor.Handle(new("Owner two", "second@example.invalid", "0901234568", Password, "Đám Cưới Đẹp", "HCM", BusinessGroupIds: [DecorGroupId], PartnershipPolicyAccepted: true), default);
        Assert.Equal("VendorOwner", first.User.Role); Assert.NotNull(first.User.VendorId);
        Assert.Equal(first.User.VendorId, (await f.Login.Handle(new(first.User.Email, Password), default)).User.VendorId);
        Assert.Equal("dam-cuoi-dep", (await f.Db.Vendors.SingleAsync(v => v.Id == first.User.VendorId)).Slug);
        Assert.Equal("dam-cuoi-dep-1", (await f.Db.Vendors.SingleAsync(v => v.Id == second.User.VendorId)).Slug);
        Assert.All(await f.Db.Vendors.ToListAsync(), vendor => Assert.False(vendor.IsVerified));
        Assert.Equal(0m, (await f.Db.Vendors.SingleAsync(v => v.Id == first.User.VendorId)).CommissionRate);
        await Assert.ThrowsAsync<DomainException>(() => f.Vendor.Handle(new("Owner", "bad@example.invalid", "0901234567", Password, "Bad rate", "HCM", .15m, [DecorGroupId], true), default));
        await Assert.ThrowsAsync<DomainException>(() => f.Vendor.Handle(new("Owner", "bad2@example.invalid", "0901234567", Password, "Bad rate", "HCM", .04m, [DecorGroupId], true), default));
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

    [Fact]
    public async Task FacebookUsesSubjectWithoutRequiringEmailOrMergingAnExistingAccount()
    {
        await using var f = await Fixture.Create();
        var existing = f.AddUser(UserRole.VendorOwner, "owner"); await f.Db.SaveChangesAsync();
        var separateIdentity = new FacebookLoginCommandHandler(f.Db, f.Hasher, f.Jwt,
            new Facebook(new("facebook-subject", existing.Email, "Owner", null)));
        var separate = await separateIdentity.Handle(new("verified-by-fake"), default);
        Assert.NotEqual(existing.Id, separate.User.Id);
        Assert.Equal("Customer", separate.User.Role);
        Assert.Empty(separate.User.Email);
        Assert.Equal(UserRole.VendorOwner, existing.Role);
        Assert.Null(existing.FacebookSubject);

        var facebook = new FacebookLoginCommandHandler(f.Db, f.Hasher, f.Jwt,
            new Facebook(new("new-facebook-subject", null, "Facebook customer", null)));
        var result = await facebook.Handle(new("verified-by-fake"), default);
        Assert.Equal("Customer", result.User.Role);
        Assert.Equal(result.User.Id, (await facebook.Handle(new("verified-by-fake"), default)).User.Id);
        var customer = await f.Db.Users.SingleAsync(user => user.Id == result.User.Id);
        Assert.Equal("new-facebook-subject", customer.FacebookSubject);
        Assert.Empty(customer.Email);
        Assert.Equal(customer.Id.ToString(), customer.NotificationRecipient);
        var notificationSender = new Wedding.Infrastructure.Services.DurableNotificationSender(f.Db);
        await notificationSender.SendAsync("Email", customer.NotificationRecipient, "Synthetic private reminder");
        var notification = await f.Db.Notifications.SingleAsync();
        Assert.Equal(customer.Id, notification.UserId);
        Assert.Equal("InApp", notification.Channel);
        Assert.Empty(notification.Destination);
        customer.SetStatus(false); await f.Db.SaveChangesAsync();
        await Assert.ThrowsAsync<DomainException>(() => facebook.Handle(new("verified-by-fake"), default));
        Assert.Equal(3, await f.Db.Users.CountAsync());
    }

    [Fact]
    public async Task GoogleCustomerEmailCollisionCreatesAnIndependentSubjectAccountWithoutManualLinking()
    {
        await using var f = await Fixture.Create();
        var existing = f.AddUser(UserRole.Customer, "old-customer");
        await f.Db.SaveChangesAsync();
        var originalHash = existing.PasswordHash;
        var google = new GoogleLoginCommandHandler(f.Db, f.Hasher, f.Jwt,
            new Google(new("separate-google-subject", existing.Email, "Google customer", null)));
        var login = await google.Handle(new("verified-by-fake"), default);
        Assert.NotEqual(existing.Id, login.User.Id);
        Assert.Empty(login.User.Email);
        Assert.Equal(login.User.Id, (await google.Handle(new("verified-by-fake"), default)).User.Id);
        Assert.Null(existing.GoogleSubject);
        Assert.Equal(originalHash, existing.PasswordHash);
        Assert.Equal(2, await f.Db.Users.CountAsync());
    }

    [Fact]
    public async Task EmailLessFacebookIdentityCanBeAddedByItsAlreadyAuthenticatedCustomer()
    {
        await using var f = await Fixture.Create();
        var customer = f.AddUser(UserRole.Customer, "session-owner");
        await f.Db.SaveChangesAsync();
        var result = await ControllerFor(customer).LinkFacebook(new("verified-by-fake"), f.Db,
            new Facebook(new("email-less-subject", null, "Customer", null)), default);
        Assert.IsType<NoContentResult>(result);
        Assert.Equal("email-less-subject", customer.FacebookSubject);
        Assert.Equal("session-owner@example.invalid", customer.Email);
    }

    [Fact]
    public async Task FacebookAccountLinkAndStatusAreCustomerOnlyAndRequireVerifiedMatchingEmail()
    {
        await using var f = await Fixture.Create();
        var customer = f.AddUser(UserRole.Customer, "couple");
        var anotherCustomer = f.AddUser(UserRole.Customer, "other");
        anotherCustomer.LinkFacebook("already-linked-subject");
        await f.Db.SaveChangesAsync();
        var controller = ControllerFor(customer);

        var initial = Assert.IsType<OkObjectResult>(await controller.FacebookLinkStatus(f.Db, default));
        Assert.Equal("{\"linked\":false}", System.Text.Json.JsonSerializer.Serialize(initial.Value));
        var linked = await controller.LinkFacebook(new("verified-facebook-access-token"), f.Db,
            new Facebook(new("new-subject", customer.Email, "Customer", null)), default);
        Assert.IsType<NoContentResult>(linked);
        Assert.Equal("new-subject", (await f.Db.Users.SingleAsync(user => user.Id == customer.Id)).FacebookSubject);
        var currentStatus = Assert.IsType<OkObjectResult>(await controller.FacebookLinkStatus(f.Db, default));
        Assert.Equal("{\"linked\":true}", System.Text.Json.JsonSerializer.Serialize(currentStatus.Value));

        var mismatch = await controller.LinkFacebook(new("verified-facebook-access-token"), f.Db,
            new Facebook(new("different-subject", "different@example.invalid", "Other", null)), default);
        Assert.IsType<BadRequestObjectResult>(mismatch);
        var collision = await controller.LinkFacebook(new("verified-facebook-access-token"), f.Db,
            new Facebook(new("already-linked-subject", customer.Email, "Customer", null)), default);
        Assert.IsType<BadRequestObjectResult>(collision);
        Assert.Equal("new-subject", (await f.Db.Users.SingleAsync(user => user.Id == customer.Id)).FacebookSubject);

        foreach (var actionName in new[] { nameof(AuthController.FacebookLinkStatus), nameof(AuthController.LinkFacebook) })
        {
            var action = typeof(AuthController).GetMethod(actionName)!;
            Assert.Equal("Customer", action.GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
                .Cast<AuthorizeAttribute>().Single().Roles);
        }
    }

    [Fact]
    public async Task GoogleAccountLinkAndStatusAreCustomerOnlyAndRequireVerifiedMatchingEmail()
    {
        await using var f = await Fixture.Create();
        var customer = f.AddUser(UserRole.Customer, "couple");
        var anotherCustomer = f.AddUser(UserRole.Customer, "other");
        anotherCustomer.LinkGoogle("already-linked-subject");
        await f.Db.SaveChangesAsync();

        var controller = ControllerFor(customer);
        var initial = Assert.IsType<OkObjectResult>(await controller.GoogleLinkStatus(f.Db, default));
        Assert.Equal("{\"linked\":false}", System.Text.Json.JsonSerializer.Serialize(initial.Value));

        var linked = await controller.LinkGoogle(new("verified-google-id-token"), f.Db,
            new Google(new("new-subject", customer.Email, "Customer", null)), default);
        Assert.IsType<NoContentResult>(linked);
        Assert.Equal("new-subject", (await f.Db.Users.SingleAsync(user => user.Id == customer.Id)).GoogleSubject);
        var currentStatus = Assert.IsType<OkObjectResult>(await controller.GoogleLinkStatus(f.Db, default));
        Assert.Equal("{\"linked\":true}", System.Text.Json.JsonSerializer.Serialize(currentStatus.Value));

        var mismatch = await controller.LinkGoogle(new("verified-google-id-token"), f.Db,
            new Google(new("different-subject", "different@example.invalid", "Other", null)), default);
        Assert.IsType<BadRequestObjectResult>(mismatch);
        var collision = await controller.LinkGoogle(new("verified-google-id-token"), f.Db,
            new Google(new("already-linked-subject", customer.Email, "Customer", null)), default);
        Assert.IsType<BadRequestObjectResult>(collision);
        Assert.Equal("new-subject", (await f.Db.Users.SingleAsync(user => user.Id == customer.Id)).GoogleSubject);

        foreach (var actionName in new[] { nameof(AuthController.GoogleLinkStatus), nameof(AuthController.LinkGoogle) })
        {
            var action = typeof(AuthController).GetMethod(actionName)!;
            Assert.Equal("Customer", action.GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
                .Cast<AuthorizeAttribute>().Single().Roles);
        }
    }

    [Fact]
    public async Task PasswordResetStoresOnlyHashedSingleUseTokenAndRevokesOldSessions()
    {
        await using var f = await Fixture.Create();
        var customer = f.AddUser(UserRole.Customer, "google-only");
        customer.UpdatePassword(f.Hasher.Hash(Guid.NewGuid().ToString("N")));
        customer.LinkGoogle("google-subject");
        await f.Db.SaveChangesAsync();
        var delivery = new Delivery();

        var response = await new RequestPasswordResetCommandHandler(f.Db, delivery)
            .Handle(new(customer.Email), default);
        var stored = await f.Db.PasswordResetTokens.SingleAsync();
        Assert.Equal(response.Message, (await new RequestPasswordResetCommandHandler(f.Db, delivery)
            .Handle(new("missing@example.invalid"), default)).Message);
        Assert.Equal(1, delivery.ResetSent);
        Assert.NotNull(delivery.ResetToken);
        Assert.NotEqual(delivery.ResetToken, stored.TokenHash);
        Assert.Equal(64, stored.TokenHash.Length);
        Assert.Equal(customer.Id, stored.UserId);
        Assert.True(stored.ExpiresAt <= DateTime.UtcNow.AddMinutes(31));

        var oldSecurityVersion = customer.SecurityVersion;
        var reset = new ResetPasswordCommandHandler(f.Db, f.Hasher);
        await reset.Handle(new(delivery.ResetToken, "new-password-123", "new-password-123"), default);
        var updated = await f.Db.Users.SingleAsync(user => user.Id == customer.Id);
        Assert.Equal(oldSecurityVersion + 1, updated.SecurityVersion);
        Assert.True(f.Hasher.Verify("new-password-123", updated.PasswordHash));
        Assert.True((await f.Db.PasswordResetTokens.SingleAsync()).UsedAt.HasValue);
        Assert.Contains(await f.Db.AuditLogs.ToListAsync(), audit => audit.Action == "Auth.PasswordReset");
        await Assert.ThrowsAsync<DomainException>(() => reset.Handle(
            new(delivery.ResetToken, "another-password-123", "another-password-123"), default));
    }

    [Fact]
    public async Task PasswordResetRequestDoesNotEnumerateAndRespectsCooldown()
    {
        await using var f = await Fixture.Create();
        var customer = f.AddUser(UserRole.Customer, "couple"); await f.Db.SaveChangesAsync();
        var delivery = new Delivery();
        var handler = new RequestPasswordResetCommandHandler(f.Db, delivery);

        var known = await handler.Handle(new(customer.Email), default);
        var unknown = await handler.Handle(new("unknown@example.invalid"), default);
        var throttled = await handler.Handle(new(customer.Email), default);

        Assert.Equal(known.Message, unknown.Message);
        Assert.Equal(known.Message, throttled.Message);
        Assert.Equal(1, delivery.ResetSent);
        Assert.Single(await f.Db.PasswordResetTokens.ToListAsync());
    }

    [Fact]
    public async Task FailedRecoveryEmailDoesNotInvalidateThePreviouslyDeliveredLink()
    {
        await using var f = await Fixture.Create();
        var customer = f.AddUser(UserRole.Customer, "couple"); await f.Db.SaveChangesAsync();
        var delivery = new Delivery();
        var handler = new RequestPasswordResetCommandHandler(f.Db, delivery);
        await handler.Handle(new(customer.Email), default);
        var first = await f.Db.PasswordResetTokens.SingleAsync();
        f.Db.Entry(first).Property(token => token.CreatedAt).CurrentValue = DateTime.UtcNow.AddMinutes(-2);
        await f.Db.SaveChangesAsync();

        delivery.FailPasswordReset = true;
        await handler.Handle(new(customer.Email), default);

        var tokens = await f.Db.PasswordResetTokens.OrderBy(token => token.CreatedAt).ToListAsync();
        Assert.Equal(2, tokens.Count);
        Assert.Null(tokens[0].UsedAt);
        Assert.True(tokens[1].UsedAt.HasValue);
    }

    [Fact]
    public async Task PasswordResetRejectsExpiredTokensWithoutChangingTheCredential()
    {
        await using var f = await Fixture.Create();
        var customer = f.AddUser(UserRole.Customer, "couple"); await f.Db.SaveChangesAsync();
        var originalHash = customer.PasswordHash;
        var token = PasswordResetToken.Create(customer.Id, out var rawToken);
        f.Db.PasswordResetTokens.Add(token);
        f.Db.Entry(token).Property(value => value.ExpiresAt).CurrentValue = DateTime.UtcNow.AddMinutes(-1);
        await f.Db.SaveChangesAsync();

        var handler = new ResetPasswordCommandHandler(f.Db, f.Hasher);
        await Assert.ThrowsAsync<DomainException>(() => handler.Handle(new(rawToken, "new-password-123", "new-password-123"), default));

        var unchanged = await f.Db.Users.SingleAsync(user => user.Id == customer.Id);
        Assert.Equal(originalHash, unchanged.PasswordHash);
        Assert.Equal(0, unchanged.SecurityVersion);
        Assert.Null((await f.Db.PasswordResetTokens.SingleAsync()).UsedAt);
    }

    [Fact]
    public async Task PasswordResetIsDisabledUntilDeliveryAndPublicUrlAreConfigured()
    {
        await using var f = await Fixture.Create();
        var customer = f.AddUser(UserRole.Customer, "couple"); await f.Db.SaveChangesAsync();
        var delivery = new Delivery { PasswordRecoveryEnabled = false };

        var response = await new RequestPasswordResetCommandHandler(f.Db, delivery).Handle(new(customer.Email), default);

        Assert.Contains("Nếu email", response.Message);
        Assert.Equal(0, delivery.ResetSent);
        Assert.Empty(await f.Db.PasswordResetTokens.ToListAsync());
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
    private static AuthController ControllerFor(User user)
    {
        var controller = new AuthController(null!);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity([
                    new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                    new Claim(ClaimTypes.Role, user.Role.ToString())
                ], "unit-test"))
            }
        };
        return controller;
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
    private sealed record Facebook(FacebookIdentity Identity) : IFacebookIdentityVerifier
    {
        public bool IsConfigured => true;
        public string? AppId => "test-facebook-app";
        public string? GraphApiVersion => "v99.0";
        public Task<FacebookIdentity> VerifyAsync(string accessToken, CancellationToken ct) => Task.FromResult(Identity);
    }
    private sealed class Delivery : IOtpDelivery, IPasswordResetDelivery
    {
        public bool EmailEnabled => true;
        public bool PasswordRecoveryEnabled { get; set; } = true;
        public int Sent { get; private set; }
        public int ResetSent { get; private set; }
        public string? ResetToken { get; private set; }
        public bool FailPasswordReset { get; set; }
        public Task SendEmailAsync(string target, string code, CancellationToken ct) { Sent++; return Task.CompletedTask; }
        public Task SendPasswordResetAsync(string target, string rawToken, CancellationToken ct)
        { ResetSent++; ResetToken = rawToken; return FailPasswordReset ? Task.FromException(new DomainException("synthetic delivery failure")) : Task.CompletedTask; }
    }
}
