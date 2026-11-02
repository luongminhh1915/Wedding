using System.Security.Claims;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Wedding.Domain.Entities;
using Wedding.Domain.Enums;
using Wedding.Infrastructure.Persistence;
using Wedding.Infrastructure.Services;
using Wedding.WebApi.Hubs;

namespace Wedding.Tests;

public sealed class ChatRoomAccessTests
{
    [Fact]
    public async Task OnlyLeadParticipantsCanSendAndOnlyVendorIsAQuoteSender()
    {
        await using var f = await Fixture.Create();
        var customer = await f.Access.GetMembershipAsync(Principal(f.Customer), f.Lead.Id, true);
        var vendor = await f.Access.GetMembershipAsync(Principal(f.Owner), f.Lead.Id, true);
        Assert.NotNull(customer); Assert.True(customer.IsCustomer); Assert.False(customer.IsVendor);
        Assert.NotNull(vendor); Assert.True(vendor.IsVendor); Assert.False(vendor.IsCustomer);
        Assert.NotEqual(customer.GroupName, vendor.GroupName);
        Assert.Null(await f.Access.GetMembershipAsync(Principal(f.OtherCustomer), f.Lead.Id, true));
        Assert.Null(await f.Access.GetMembershipAsync(Principal(f.OtherOwner), f.Lead.Id, true));
    }

    [Theory]
    [InlineData(UserRole.SuperAdmin, true)]
    [InlineData(UserRole.Moderator, true)]
    [InlineData(UserRole.Finance, false)]
    [InlineData(UserRole.CustomerCare, false)]
    [InlineData(UserRole.Customer, false)]
    [InlineData(UserRole.VendorOwner, false)]
    public async Task NonParticipantReadRolesExactlyMatchHttpPolicyAndNeverGainSendPermission(UserRole role, bool canRead)
    {
        await using var f = await Fixture.Create();
        var actor = f.AddUser(role); await f.Db.SaveChangesAsync();
        Assert.Equal(canRead, await f.Access.GetMembershipAsync(Principal(actor), f.Lead.Id, false) is not null);
        Assert.Null(await f.Access.GetMembershipAsync(Principal(actor), f.Lead.Id, true));
        var groups = await f.Access.GetCurrentRecipientGroupsAsync(f.Lead.Id);
        Assert.Equal(canRead, groups.Any(group => group.Contains($"user-{actor.Id:N}-", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task InvalidUnknownUnauthenticatedAndUnrelatedRoomsAreAllDenied()
    {
        await using var f = await Fixture.Create();
        Assert.Null(await f.Access.GetMembershipAsync(null, f.Lead.Id, false));
        var anonymous = new ClaimsPrincipal(new ClaimsIdentity(Principal(f.Customer).Claims));
        Assert.Null(await f.Access.GetMembershipAsync(anonymous, f.Lead.Id, false));
        Assert.Null(await f.Access.GetMembershipAsync(Principal(f.Customer), Guid.Empty, false));
        Assert.Null(await f.Access.GetMembershipAsync(Principal(f.Customer), Guid.NewGuid(), false));
        Assert.Null(await f.Access.GetMembershipAsync(Principal(f.OtherCustomer), f.Lead.Id, false));
        Assert.Empty(await f.Access.GetCurrentRecipientGroupsAsync(Guid.NewGuid()));
        Assert.Empty(await f.Access.GetCurrentRecipientGroupsAsync(Guid.Empty));
    }

    [Fact]
    public async Task EveryInvocationRechecksActiveAccountAndCurrentPasswordVersion()
    {
        await using var f = await Fixture.Create();
        var before = Principal(f.Customer);
        var membership = await f.Access.GetMembershipAsync(before, f.Lead.Id, false);
        Assert.NotNull(membership);
        f.Customer.UpdatePassword("new-hash"); await f.Db.SaveChangesAsync();

        Assert.Null(await f.Access.GetMembershipAsync(before, f.Lead.Id, false));
        Assert.Null(await f.Access.GetMembershipAsync(before, f.Lead.Id, true));
        var after = await f.Access.GetMembershipAsync(Principal(f.Customer), f.Lead.Id, false);
        Assert.NotNull(after); Assert.NotEqual(membership.GroupName, after.GroupName);
        var recipients = await f.Access.GetCurrentRecipientGroupsAsync(f.Lead.Id);
        Assert.DoesNotContain(membership.GroupName, recipients); Assert.Contains(after.GroupName, recipients);
        Assert.DoesNotContain(recipients, group => group == $"lead-{f.Lead.Id}" || group == $"lead-{f.Lead.Id:N}");

        f.Customer.SetStatus(false); await f.Db.SaveChangesAsync();
        Assert.Null(await f.Access.GetMembershipAsync(Principal(f.Customer), f.Lead.Id, false));
        Assert.DoesNotContain(after.GroupName, await f.Access.GetCurrentRecipientGroupsAsync(f.Lead.Id));
    }

    [Fact]
    public async Task RoleChangesCannotReuseOldSessionOrReceiveThroughOldGroups()
    {
        await using var f = await Fixture.Create();
        var moderator = f.AddUser(UserRole.Moderator); await f.Db.SaveChangesAsync();
        var before = Principal(moderator);
        var room = await f.Access.GetMembershipAsync(before, f.Lead.Id, false); Assert.NotNull(room);

        // Simulate an administrative role change, including one which did not
        // increment the version: the group role component still revokes it.
        f.Db.Entry(moderator).Property(user => user.Role).CurrentValue = UserRole.SuperAdmin;
        await f.Db.SaveChangesAsync();
        Assert.Null(await f.Access.GetMembershipAsync(before, f.Lead.Id, false));
        var changed = await f.Access.GetMembershipAsync(Principal(moderator), f.Lead.Id, false); Assert.NotNull(changed);
        Assert.NotEqual(room.GroupName, changed.GroupName);
        var groups = await f.Access.GetCurrentRecipientGroupsAsync(f.Lead.Id);
        Assert.DoesNotContain(room.GroupName, groups); Assert.Contains(changed.GroupName, groups);

        f.Db.Entry(moderator).Property(user => user.Role).CurrentValue = UserRole.Finance;
        await f.Db.SaveChangesAsync();
        Assert.Null(await f.Access.GetMembershipAsync(Principal(moderator), f.Lead.Id, false));
        Assert.DoesNotContain(changed.GroupName, await f.Access.GetCurrentRecipientGroupsAsync(f.Lead.Id));
    }

    [Fact]
    public async Task ChangedLeadOwnershipStopsSendingToThePreviousOwner()
    {
        await using var f = await Fixture.Create();
        var oldRoom = await f.Access.GetMembershipAsync(Principal(f.Owner), f.Lead.Id, false); Assert.NotNull(oldRoom);
        var otherVendor = Vendor.Create(f.OtherOwner.Id, "Other shop", "other-shop", "Other", "0901234567", "HCM", .05m);
        f.Db.Vendors.Add(otherVendor); await f.Db.SaveChangesAsync();
        f.Db.Entry(f.Lead).Property(lead => lead.VendorId).CurrentValue = otherVendor.Id;
        await f.Db.SaveChangesAsync();
        Assert.Null(await f.Access.GetMembershipAsync(Principal(f.Owner), f.Lead.Id, false));
        Assert.DoesNotContain(oldRoom.GroupName, await f.Access.GetCurrentRecipientGroupsAsync(f.Lead.Id));
        Assert.NotNull(await f.Access.GetMembershipAsync(Principal(f.OtherOwner), f.Lead.Id, true));
    }

    [Fact]
    public async Task MissingDuplicateOrForgedSessionClaimsNeverAuthorizeRoomMembership()
    {
        await using var f = await Fixture.Create();
        var claims = Principal(f.Customer).Claims.ToArray();
        foreach (var claimType in new[] { ClaimTypes.NameIdentifier, ClaimTypes.Role, "security_version" })
        {
            Assert.Null(await f.Access.GetMembershipAsync(Authenticated(claims.Where(claim => claim.Type != claimType)), f.Lead.Id, false));
            Assert.Null(await f.Access.GetMembershipAsync(Authenticated(claims.Append(claims.Single(claim => claim.Type == claimType))), f.Lead.Id, false));
        }
        foreach (var value in new[] { "-1", "00", "+0", " 0", "0 ", "1", "2147483648", "not-a-version" })
        {
            var forged = claims.Where(claim => claim.Type != "security_version").Append(new Claim("security_version", value));
            Assert.Null(await f.Access.GetMembershipAsync(Authenticated(forged), f.Lead.Id, false));
        }
        Assert.Null(await f.Access.GetMembershipAsync(Authenticated(claims.Where(claim => claim.Type != ClaimTypes.Role)
            .Append(new Claim(ClaimTypes.Role, "SuperAdmin"))), f.Lead.Id, false));
    }

    [Fact]
    public async Task HubJoinLeaveAndSendUseSameGenericDenialAndNeverTouchAnUnauthorizedGroup()
    {
        await using var f = await Fixture.Create();
        var groups = new RecordingGroups();
        var hub = new ChatHub(null!, NullLogger<ChatHub>.Instance, f.Access)
        { Context = new Caller(Principal(f.OtherCustomer)), Groups = groups };
        var failures = new List<HubException>();
        failures.Add(await Assert.ThrowsAsync<HubException>(() => hub.JoinLeadRoom("invalid-guid")));
        failures.Add(await Assert.ThrowsAsync<HubException>(() => hub.JoinLeadRoom(Guid.NewGuid().ToString())));
        failures.Add(await Assert.ThrowsAsync<HubException>(() => hub.JoinLeadRoom(f.Lead.Id.ToString())));
        failures.Add(await Assert.ThrowsAsync<HubException>(() => hub.LeaveLeadRoom(f.Lead.Id.ToString())));
        failures.Add(await Assert.ThrowsAsync<HubException>(() => hub.SendMessage(f.Lead.Id, "Forbidden")));
        Assert.Single(failures.Select(error => error.Message).Distinct());
        Assert.Empty(groups.Added); Assert.Empty(groups.Removed);
    }

    [Fact]
    public async Task HubCanonicalizesGuidGroupsAndChecksDatabaseAgainAfterLongLivedSocketWasJoined()
    {
        await using var f = await Fixture.Create();
        var groups = new RecordingGroups();
        var hub = new ChatHub(null!, NullLogger<ChatHub>.Instance, f.Access)
        { Context = new Caller(Principal(f.Customer)), Groups = groups };
        await hub.JoinLeadRoom(f.Lead.Id.ToString("B").ToUpperInvariant());
        await hub.LeaveLeadRoom(f.Lead.Id.ToString("N"));
        Assert.Single(groups.Added); Assert.Equal(groups.Added, groups.Removed);

        f.Customer.UpdatePassword("replacement-hash"); await f.Db.SaveChangesAsync();
        await Assert.ThrowsAsync<HubException>(() => hub.JoinLeadRoom(f.Lead.Id.ToString()));
        await Assert.ThrowsAsync<HubException>(() => hub.LeaveLeadRoom(f.Lead.Id.ToString()));
        await Assert.ThrowsAsync<HubException>(() => hub.SendMessage(f.Lead.Id, "Old socket must not send"));
        Assert.Single(groups.Added); Assert.Single(groups.Removed);
        Assert.DoesNotContain(groups.Added[0], await f.Access.GetCurrentRecipientGroupsAsync(f.Lead.Id));
    }

    [Fact]
    public async Task StaffCanReadButHubCannotSendOrQuoteForThemAndCustomersCannotQuote()
    {
        await using var f = await Fixture.Create();
        var staff = f.AddUser(UserRole.Moderator); await f.Db.SaveChangesAsync();
        var groups = new RecordingGroups();
        var hub = new ChatHub(null!, NullLogger<ChatHub>.Instance, f.Access)
        { Context = new Caller(Principal(staff)), Groups = groups };
        await hub.JoinLeadRoom(f.Lead.Id.ToString());
        Assert.Single(groups.Added);
        await Assert.ThrowsAsync<HubException>(() => hub.SendMessage(f.Lead.Id, "Not a participant"));
        hub.Context = new Caller(Principal(f.Customer));
        await Assert.ThrowsAsync<HubException>(() => hub.SendMessage(f.Lead.Id, "Forged quote", true, 100m));
    }

    private static ClaimsPrincipal Principal(User user) => Authenticated(
    [
        new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
        new Claim(ClaimTypes.Role, user.Role.ToString()),
        new Claim("security_version", user.SecurityVersion.ToString(System.Globalization.CultureInfo.InvariantCulture))
    ]);

    private static ClaimsPrincipal Authenticated(IEnumerable<Claim> claims) => new(new ClaimsIdentity(claims, "test"));

    private sealed class Caller(ClaimsPrincipal principal) : HubCallerContext
    {
        public override string ConnectionId => "qa-only-connection";
        public override string? UserIdentifier => principal.FindFirstValue(ClaimTypes.NameIdentifier);
        public override ClaimsPrincipal User => principal;
        public override IDictionary<object, object?> Items { get; } = new Dictionary<object, object?>();
        public override IFeatureCollection Features { get; } = new FeatureCollection();
        public override CancellationToken ConnectionAborted => CancellationToken.None;
        public override void Abort() { }
    }

    private sealed class RecordingGroups : IGroupManager
    {
        public List<string> Added { get; } = [];
        public List<string> Removed { get; } = [];
        public Task AddToGroupAsync(string connectionId, string groupName, CancellationToken cancellationToken = default)
        { Added.Add(groupName); return Task.CompletedTask; }
        public Task RemoveFromGroupAsync(string connectionId, string groupName, CancellationToken cancellationToken = default)
        { Removed.Add(groupName); return Task.CompletedTask; }
    }

    private sealed class Fixture : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;
        public ApplicationDbContext Db { get; }
        public ChatRoomAccess Access { get; }
        public User Customer { get; }
        public User Owner { get; }
        public User OtherCustomer { get; }
        public User OtherOwner { get; }
        public Lead Lead { get; }

        private Fixture(SqliteConnection connection, ApplicationDbContext db)
        {
            _connection = connection; Db = db; Access = new ChatRoomAccess(db);
            Customer = AddUser(UserRole.Customer); Owner = AddUser(UserRole.VendorOwner);
            OtherCustomer = AddUser(UserRole.Customer); OtherOwner = AddUser(UserRole.VendorOwner);
            var vendor = Vendor.Create(Owner.Id, "Shop", "shop", "Owner", "0901234567", "HCM", .05m);
            Db.Vendors.Add(vendor);
            Lead = Lead.Create(Customer.Id, vendor.Id, null, "0901234567", null, null, null, null);
            Db.Leads.Add(Lead);
        }

        public User AddUser(UserRole role)
        {
            var user = User.Create("Chat actor", $"{Guid.NewGuid():N}@example.invalid", "0901234567", "hash", role);
            Db.Users.Add(user); return user;
        }

        public static async Task<Fixture> Create()
        {
            var connection = new SqliteConnection("Data Source=:memory:"); await connection.OpenAsync();
            var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options);
            await db.Database.EnsureCreatedAsync();
            var fixture = new Fixture(connection, db); await db.SaveChangesAsync(); return fixture;
        }

        public async ValueTask DisposeAsync() { await Db.DisposeAsync(); await _connection.DisposeAsync(); }
    }
}
