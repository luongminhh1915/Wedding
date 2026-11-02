using System.Text.Json;
using Microsoft.Data.SqlClient;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Wedding.Application.Common.Interfaces;
using Wedding.Application.Features.Notifications;
using Wedding.Application.Features.Leads.Queries.GetVendorLeads;
using Wedding.Application.Features.Contracts.Queries.GetVendorContracts;
using Wedding.Domain.Entities;
using Wedding.Domain.Enums;
using Wedding.Domain.Exceptions;
using Wedding.Infrastructure.Persistence;
using Wedding.Infrastructure.Services;

namespace Wedding.Tests;

public class NotificationCenterTests
{
    [Fact]
    public async Task PagesAreStableOwnerOnlyAndUnreadCountIncludesMoreThanFiftyMessages()
    {
        await using var f = await Fixture.Create();
        var sameTime = DateTime.UtcNow.AddDays(-1);
        for (var i = 0; i < 63; i++)
        {
            var notification = Notification.Create(f.Customer.Id, "InApp", "private-destination", $"Customer message {i}");
            if (i < 7) notification.Read();
            f.Db.Notifications.Add(notification); f.Db.Entry(notification).Property(n => n.CreatedAt).CurrentValue = sameTime;
        }
        f.Db.Notifications.Add(Notification.Create(f.Other.Id, "Email", "other-private-destination", "Must stay private"));
        await f.Db.SaveChangesAsync();
        var first = await f.List().Handle(new(1, 8), default);
        var next = await f.List().Handle(new(2, 8), default);
        Assert.Equal(63, first.TotalCount); Assert.Equal(56, first.UnreadCount); Assert.Equal(8, first.TotalPages);
        Assert.Equal(8, first.Items.Count); Assert.Equal(8, next.Items.Count);
        Assert.Empty(first.Items.Select(n => n.Id).Intersect(next.Items.Select(n => n.Id)));
        var expected = await f.Db.Notifications.Where(n => n.UserId == f.Customer.Id).OrderByDescending(n => n.CreatedAt)
            .ThenByDescending(n => n.Id).Take(16).Select(n => n.Id).ToListAsync();
        Assert.Equal(expected, first.Items.Concat(next.Items).Select(n => n.Id));
        Assert.DoesNotContain("private-destination", JsonSerializer.Serialize(first));
        Assert.DoesNotContain("Must stay private", JsonSerializer.Serialize(first));
        var unread = await f.List().Handle(new(1, 5, true), default);
        Assert.Equal(56, unread.TotalCount); Assert.All(unread.Items, n => Assert.Null(n.ReadAt));
        var last = await f.List().Handle(new(int.MaxValue, 8), default);
        Assert.Equal(8, last.Page); Assert.Equal(7, last.Items.Count);
    }

    [Fact]
    public async Task MarkOneIsIdempotentAndMarkAllNeverTouchesAnotherOwner()
    {
        await using var f = await Fixture.Create();
        var first = Notification.Create(f.Customer.Id, "InApp", "", "First");
        var second = Notification.Create(f.Customer.Id, "Email", "", "Second");
        var other = Notification.Create(f.Other.Id, "InApp", "", "Other");
        f.Db.Notifications.AddRange(first, second, other); await f.Db.SaveChangesAsync();
        var mark = new ReadNotificationCommandHandler(f.Db, new Current(f.Customer.Id));
        await Assert.ThrowsAsync<NotFoundException>(() => mark.Handle(new(other.Id), default));
        await Assert.ThrowsAsync<NotFoundException>(() => mark.Handle(new(Guid.NewGuid()), default));
        await mark.Handle(new(first.Id), default); var readAt = first.ReadAt;
        await mark.Handle(new(first.Id), default); Assert.Equal(readAt, first.ReadAt);
        Assert.Equal(1, (await f.List().Handle(new(), default)).UnreadCount);
        await mark.Handle(new(null), default); await mark.Handle(new(null), default);
        f.Db.ChangeTracker.Clear();
        Assert.Equal(2, await f.Db.Notifications.CountAsync(n => n.UserId == f.Customer.Id && n.ReadAt != null));
        Assert.Null((await f.Db.Notifications.SingleAsync(n => n.Id == other.Id)).ReadAt);
        var empty = await f.List().Handle(new(100, 5, true), default);
        Assert.Empty(empty.Items); Assert.Equal(1, empty.Page); Assert.Equal(0, empty.UnreadCount);
    }

    [Fact]
    public async Task ContextLinksUseCurrentOwnershipAndRoleNotMessageTextOrArbitraryUrls()
    {
        await using var f = await Fixture.Create();
        var ownLead = Lead.Create(f.Customer.Id, f.Vendor.Id, null, "0900000000", null, null, null, null);
        var otherLead = Lead.Create(f.Other.Id, f.Vendor.Id, null, "0900000000", null, null, null, null);
        var task = ChecklistTask.Create(f.Customer.Id, "Own task", "3_MONTHS");
        var foreignTask = ChecklistTask.Create(f.Other.Id, "Other task", "3_MONTHS");
        f.Db.Leads.AddRange(ownLead, otherLead); f.Db.ChecklistTasks.AddRange(task, foreignTask);
        var quote = Quote.Create(ownLead.Id, f.Vendor.Id, f.Customer.Id, "[]", 1000000, 0, null, 7, 1);
        var contract = BookingContract.Create(ownLead.Id, null, f.Vendor.Id, f.Customer.Id, 1000000, 0, null, null);
        f.Db.Quotes.Add(quote); f.Db.BookingContracts.Add(contract);
        var own = Notification.Create(f.Customer.Id, "InApp", "", "Owned lead", contextKind: "lead", contextId: ownLead.Id);
        var notOwned = Notification.Create(f.Customer.Id, "InApp", "", "Foreign lead must not link", contextKind: "lead", contextId: otherLead.Id);
        var missing = Notification.Create(f.Customer.Id, "InApp", "", "Deleted lead", contextKind: "lead", contextId: Guid.NewGuid());
        var legacy = Notification.Create(f.Customer.Id, "InApp", "", "https://evil.example/admin/dashboard — old text never becomes a link");
        f.Db.Notifications.AddRange(own, notOwned, missing, legacy,
            Notification.Create(f.Customer.Id, "Email", "", "Quote", contextKind: "quote", contextId: quote.Id),
            Notification.Create(f.Customer.Id, "InApp", "", "Contract", contextKind: "contract", contextId: contract.Id),
            Notification.Create(f.Customer.Id, "InApp", "", "Task", contextKind: "task", contextId: task.Id),
            Notification.Create(f.Customer.Id, "InApp", "", "Foreign task", contextKind: "task", contextId: foreignTask.Id),
            Notification.Create(f.VendorOwner.Id, "InApp", "", "Vendor lead", contextKind: "lead", contextId: ownLead.Id));
        await f.Db.SaveChangesAsync();
        var items = (await f.List().Handle(new(1, 20), default)).Items;
        Assert.Equal($"/customer/dashboard?tab=contracts&section=leads&focus={ownLead.Id:D}&focusKind=lead", items.Single(n => n.Id == own.Id).TargetPath);
        Assert.Null(items.Single(n => n.Id == notOwned.Id).TargetPath); Assert.Null(items.Single(n => n.Id == missing.Id).TargetPath);
        Assert.Null(items.Single(n => n.Id == legacy.Id).TargetPath); Assert.Null(items.Single(n => n.Message == "Foreign task").TargetPath);
        Assert.Contains("section=quotes", items.Single(n => n.Message == "Quote").TargetPath);
        Assert.Contains("focusKind=contract", items.Single(n => n.Message == "Contract").TargetPath);
        Assert.Contains("tab=checklist", items.Single(n => n.Message == "Task").TargetPath);
        var vendor = await f.List(f.VendorOwner.Id, "VendorOwner").Handle(new(), default);
        Assert.StartsWith("/vendor/dashboard?tab=leads", Assert.Single(vendor.Items).TargetPath);
        var wrongRole = await f.List(f.Customer.Id, "Moderator").Handle(new(1, 20), default);
        Assert.All(wrongRole.Items, n => Assert.Null(n.TargetPath));
    }

    [Fact]
    public async Task OrderChangeRequestLinksRespectVendorCustomerAndFinanceAccess()
    {
        await using var f = await Fixture.Create();
        var finance = User.Create("Finance", "finance@example.invalid", "0900000000", "hash", UserRole.Finance);
        f.Db.Users.Add(finance);
        var request = OrderChangeRequest.Create(f.Customer.Id, f.Vendor.Id, "contract", Guid.NewGuid(), "Change", "A valid reason for changing", null, true);
        var noFinance = OrderChangeRequest.Create(f.Customer.Id, f.Vendor.Id, "lead", Guid.NewGuid(), "Cancellation", "A valid reason for cancelling", null, false);
        f.Db.OrderChangeRequests.AddRange(request, noFinance);
        foreach (var id in new[] { f.Customer.Id, f.VendorOwner.Id, finance.Id, f.Other.Id })
            f.Db.Notifications.Add(Notification.Create(id, "InApp", "", "Request", contextKind: "order-request", contextId: request.Id));
        f.Db.Notifications.Add(Notification.Create(finance.Id, "InApp", "", "Not for finance", contextKind: "order-request", contextId: noFinance.Id));
        await f.Db.SaveChangesAsync();
        Assert.Contains("section=requests", Assert.Single((await f.List().Handle(new(), default)).Items).TargetPath);
        Assert.StartsWith("/vendor/dashboard?tab=change-requests", Assert.Single((await f.List(f.VendorOwner.Id, "VendorOwner").Handle(new(), default)).Items).TargetPath);
        var financeItems = (await f.List(finance.Id, "Finance").Handle(new(), default)).Items;
        Assert.StartsWith("/admin/dashboard?tab=change-requests", financeItems.Single(n => n.Message == "Request").TargetPath);
        Assert.Null(financeItems.Single(n => n.Message == "Not for finance").TargetPath);
        Assert.Null(Assert.Single((await f.List(f.Other.Id).Handle(new(), default)).Items).TargetPath);
    }

    [Fact]
    public async Task VendorNotificationFocusCanLoadOlderRowsOutsideFirstTwentyButNeverAnotherVendorsRows()
    {
        await using var f = await Fixture.Create();
        var ownLeads = new List<Lead>(); var ownContracts = new List<BookingContract>();
        for (var i = 0; i < 25; i++)
        {
            var lead = Lead.Create(f.Customer.Id, f.Vendor.Id, null, "0900000000", null, null, null, null);
            var contract = BookingContract.Create(lead.Id, null, f.Vendor.Id, f.Customer.Id, 1000000, 0, null, null);
            f.Db.Leads.Add(lead); f.Db.BookingContracts.Add(contract);
            f.Db.Entry(lead).Property(x => x.CreatedAt).CurrentValue = DateTime.UtcNow.AddDays(-30 + i);
            f.Db.Entry(contract).Property(x => x.CreatedAt).CurrentValue = DateTime.UtcNow.AddDays(-30 + i);
            f.Db.Entry(contract).Property(x => x.ContractCode).CurrentValue = $"NOTIFICATION-{i}";
            ownLeads.Add(lead); ownContracts.Add(contract);
        }
        var otherOwner = User.Create("Other owner", "other-vendor@example.invalid", "0900000000", "hash", UserRole.VendorOwner);
        var otherVendor = Vendor.Create(otherOwner.Id, "Other vendor", "other-vendor", "Other owner", "0900000000", "Hà Nội", .1m);
        var foreignLead = Lead.Create(f.Other.Id, otherVendor.Id, null, "0900000000", null, null, null, null);
        var foreignContract = BookingContract.Create(foreignLead.Id, null, otherVendor.Id, f.Other.Id, 1000000, 0, null, null);
        f.Db.Users.Add(otherOwner); f.Db.Vendors.Add(otherVendor); f.Db.Leads.Add(foreignLead); f.Db.BookingContracts.Add(foreignContract);
        await f.Db.SaveChangesAsync();
        var leads = new GetVendorLeadsQueryHandler(f.Db, new Current(f.VendorOwner.Id, "VendorOwner"));
        var contracts = new GetVendorContractsQueryHandler(f.Db, new Current(f.VendorOwner.Id, "VendorOwner"));
        var firstLeads = await leads.Handle(new(), default); var firstContracts = await contracts.Handle(new(), default);
        Assert.Equal(20, firstLeads.Count); Assert.Equal(20, firstContracts.Count);
        Assert.DoesNotContain(firstLeads, x => x.Id == ownLeads[0].Id); Assert.DoesNotContain(firstContracts, x => x.Id == ownContracts[0].Id);
        Assert.Equal(ownLeads[0].Id, Assert.Single(await leads.Handle(new("Cancelled", 500, 5, ownLeads[0].Id), default)).Id);
        Assert.Equal(ownContracts[0].Id, Assert.Single(await contracts.Handle(new("Cancelled", 500, 5, ownContracts[0].Id), default)).Id);
        Assert.Empty(await leads.Handle(new(FocusId: foreignLead.Id), default));
        Assert.Empty(await contracts.Handle(new(FocusId: foreignContract.Id), default));
        Assert.Empty(await leads.Handle(new(FocusId: Guid.NewGuid()), default));
        Assert.Empty(await contracts.Handle(new(FocusId: Guid.NewGuid()), default));
    }

    [Fact]
    public async Task TypedSenderPersistsContextAndOldSenderRemainsSupported()
    {
        await using var f = await Fixture.Create();
        var sender = new DurableNotificationSender(f.Db); var id = Guid.NewGuid();
        await sender.SendContextAsync("ZNS", f.Customer.Id.ToString(), "Appointment update", "appointment", id);
        await sender.SendAsync("InApp", f.Customer.Email, "Legacy update");
        await sender.SendContextAsync("Email", "unknown@example.invalid", "Must not be sent", "appointment", id);
        var messages = await f.Db.Notifications.ToListAsync(); Assert.Equal(2, messages.Count);
        var context = messages.Single(n => n.ContextId == id); Assert.Equal("Email", context.Channel); Assert.Equal("appointment", context.ContextKind);
        Assert.Equal(f.Customer.Id, context.UserId); Assert.Null(messages.Single(n => n.Message == "Legacy update").ContextKind);
    }

    [Fact]
    public async Task InvalidPagingAndUnauthenticatedReadsAreRejected()
    {
        await using var f = await Fixture.Create();
        foreach (var request in new[] { new GetNotificationsQuery(0), new(-1), new(1, 0), new(1, 21), new(1, int.MaxValue) })
            await Assert.ThrowsAsync<DomainException>(() => f.List().Handle(request, default));
        await Assert.ThrowsAsync<DomainException>(() => new GetNotificationsQueryHandler(f.Db, new Current(null)).Handle(new(), default));
        await Assert.ThrowsAsync<DomainException>(() => new ReadNotificationCommandHandler(f.Db, new Current(null)).Handle(new(null), default));
        Assert.Throws<DomainException>(() => Notification.Create(f.Customer.Id, "InApp", "", "Invalid", contextKind: "https://evil.example", contextId: Guid.NewGuid()));
        Assert.Throws<DomainException>(() => Notification.Create(f.Customer.Id, "InApp", "", "Invalid", contextKind: "lead"));
        Assert.Throws<DomainException>(() => Notification.Create(f.Customer.Id, "InApp", "", "Invalid", contextKind: "lead", contextId: Guid.Empty));
    }

    [SqlServerFact]
    public async Task AdditiveMigrationPreservesExistingNotificationAndCanBeReplayed()
    {
        var database = $"WeddingPlanTest_Notifications_{Guid.NewGuid():N}";
        var source = Environment.GetEnvironmentVariable("WEDDING_TEST_SQLSERVER");
        var builder = new SqlConnectionStringBuilder(string.IsNullOrWhiteSpace(source) || source == "1"
            ? "Server=localhost;Integrated Security=true;TrustServerCertificate=true;Connection Timeout=5" : source) { InitialCatalog = database };
        await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlServer(builder.ConnectionString).Options);
        try
        {
            await db.GetService<IMigrator>().MigrateAsync("20261017154425_ChangePasswordSecurityVersion");
            var uid = Guid.NewGuid(); var id = Guid.NewGuid(); var at = DateTime.UtcNow.AddDays(-2);
            await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO Notifications (Id, UserId, Channel, Destination, Message, NextAttemptAt, Attempts, CreatedAt) VALUES ({id}, {uid}, {"InApp"}, {"existing-private-address"}, {"Existing notification"}, {at}, {0}, {at})");
            await db.Database.MigrateAsync(); await db.Database.MigrateAsync(); Assert.False(db.Database.HasPendingModelChanges());
            var persisted = await db.Notifications.SingleAsync(); Assert.Equal(id, persisted.Id); Assert.Equal(at, persisted.CreatedAt);
            Assert.Null(persisted.ContextId); Assert.Null(persisted.ContextKind); Assert.Null(persisted.ReadAt);
            var page = await new GetNotificationsQueryHandler(db, new Current(uid)).Handle(new(), default);
            Assert.Equal(1, page.UnreadCount); Assert.Null(Assert.Single(page.Items).TargetPath);
            await new ReadNotificationCommandHandler(db, new Current(uid)).Handle(new(null), default);
            Assert.Equal(0, (await new GetNotificationsQueryHandler(db, new Current(uid)).Handle(new(), default)).UnreadCount);
            var customer = User.Create("SQL customer", "sql-customer@example.invalid", "0900000000", "unchanged-hash", UserRole.Customer);
            var other = User.Create("SQL other", "sql-other@example.invalid", "0900000000", "other-hash", UserRole.Customer);
            var owner = User.Create("SQL owner", "sql-vendor@example.invalid", "0900000000", "vendor-hash", UserRole.VendorOwner);
            var vendor = Vendor.Create(owner.Id, "SQL Sample Vendor", "sql-sample-vendor", "SQL owner", "0900000000", "Hà Nội", .1m);
            var lead = Lead.Create(customer.Id, vendor.Id, null, "0900000000", null, null, null, null);
            db.Users.AddRange(customer, other, owner); db.Vendors.Add(vendor); db.Leads.Add(lead);
            var context = Notification.Create(customer.Id, "InApp", "", "SQL owned lead", contextKind: "lead", contextId: lead.Id);
            var foreign = Notification.Create(other.Id, "InApp", "", "SQL foreign context", contextKind: "lead", contextId: lead.Id);
            db.Notifications.AddRange(context, foreign); await db.SaveChangesAsync();
            var customerPage = await new GetNotificationsQueryHandler(db, new Current(customer.Id)).Handle(new(1, 5, true), default);
            Assert.Contains($"focus={lead.Id:D}", Assert.Single(customerPage.Items).TargetPath);
            Assert.Equal(lead.Id, Assert.Single(await new GetVendorLeadsQueryHandler(db, new Current(owner.Id, "VendorOwner")).Handle(new(FocusId: lead.Id), default)).Id);
            Assert.Null(Assert.Single((await new GetNotificationsQueryHandler(db, new Current(other.Id)).Handle(new(), default)).Items).TargetPath);
            await new ReadNotificationCommandHandler(db, new Current(customer.Id)).Handle(new(null), default);
            Assert.Equal(1, (await new GetNotificationsQueryHandler(db, new Current(other.Id)).Handle(new(), default)).UnreadCount);
        }
        finally { Assert.StartsWith("WeddingPlanTest_Notifications_", database); await db.Database.EnsureDeletedAsync(); }
    }

    private sealed class Fixture(SqliteConnection connection, ApplicationDbContext db) : IAsyncDisposable
    {
        public ApplicationDbContext Db { get; } = db;
        public User Customer { get; } = User.Create("Customer", "customer@example.invalid", "0900000000", "hash", UserRole.Customer);
        public User Other { get; } = User.Create("Other", "other@example.invalid", "0900000000", "hash", UserRole.Customer);
        public User VendorOwner { get; } = User.Create("Owner", "vendor@example.invalid", "0900000000", "hash", UserRole.VendorOwner);
        public Vendor Vendor { get; private set; } = null!;
        public static async Task<Fixture> Create()
        {
            var connection = new SqliteConnection("Data Source=:memory:"); await connection.OpenAsync();
            var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options);
            await db.Database.EnsureCreatedAsync(); var fixture = new Fixture(connection, db);
            db.Users.AddRange(fixture.Customer, fixture.Other, fixture.VendorOwner);
            fixture.Vendor = Vendor.Create(fixture.VendorOwner.Id, "Sample vendor", "sample-vendor", "Owner", "0900000000", "Hà Nội", .1m);
            db.Vendors.Add(fixture.Vendor); await db.SaveChangesAsync(); return fixture;
        }
        public GetNotificationsQueryHandler List(Guid? id = null, string role = "Customer") => new(Db, new Current(id ?? Customer.Id, role));
        public async ValueTask DisposeAsync() { await Db.DisposeAsync(); await connection.DisposeAsync(); }
    }
    private sealed record Current(Guid? UserId, string? Role = "Customer") : ICurrentUserService { public string? Email => null; }
}
