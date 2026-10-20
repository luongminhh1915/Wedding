using Microsoft.Data.SqlClient;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Wedding.Application.Common.Interfaces;
using Wedding.Application.Features.Appointments;
using Wedding.Application.Features.Combos;
using Wedding.Domain.Entities;
using Wedding.Domain.Enums;
using Wedding.Domain.Exceptions;
using Wedding.Infrastructure.Persistence;

namespace Wedding.Tests;

public class BookingComboTests
{
    private static AppointmentType Type(int capacity = 1, decimal deposit = 0) => AppointmentType.Create(Guid.NewGuid(), "Thử váy", null, 60, capacity, "0,1,2,3,4,5,6", TimeSpan.FromHours(9), TimeSpan.FromHours(18), deposit, false);
    private static DateTime Start => DateTime.UtcNow.AddDays(7).Date.AddHours(3); // 10:00 Vietnam

    [Fact]
    public void BookingWindowAndHeadcountAreValidated()
    {
        var type = Type();
        Assert.Throws<DomainException>(() => Appointment.Create(type, Guid.NewGuid(), DateTime.UtcNow.AddHours(11), 2, null, null));
        Assert.Throws<DomainException>(() => Appointment.Create(type, Guid.NewGuid(), DateTime.UtcNow.AddDays(91), 2, null, null));
        Assert.Throws<DomainException>(() => Appointment.Create(type, Guid.NewGuid(), Start, 11, null, null));
        Assert.Throws<DomainException>(() => type.Configure(0, 0, 2, 90, 10, 24, "Store", null, null, null));
    }
    [Fact]
    public void OverlapsAndBuffersConsumeCapacityEvenWhenStartsDiffer()
    {
        var type = Type(); type.Configure(0, 15, 12, 90, 10, 24, "Store", null, null, null);
        var a = Appointment.Create(type, Guid.NewGuid(), Start, 2, null, null);
        Assert.Equal(0, AppointmentScheduling.Remaining(type, Start.AddMinutes(30), new[] { a }, DateTime.UtcNow));
        Assert.Equal(0, AppointmentScheduling.Remaining(type, Start.AddMinutes(60), new[] { a }, DateTime.UtcNow));
        Assert.Equal(1, AppointmentScheduling.Remaining(type, Start.AddMinutes(75), new[] { a }, DateTime.UtcNow));
    }
    [Fact]
    public void ProposedSlotHoldsBothWindowsAndNeedsOtherPartyConsentAtMostTwice()
    {
        var type = Type(); var customer = Guid.NewGuid(); var vendorUser = Guid.NewGuid();
        var a = Appointment.Create(type, customer, Start, 2, null, null);
        a.ProposeReschedule(Start.AddHours(2), customer, type);
        Assert.Equal(0, AppointmentScheduling.Remaining(type, Start, new[] { a }, DateTime.UtcNow));
        Assert.Equal(0, AppointmentScheduling.Remaining(type, Start.AddHours(2), new[] { a }, DateTime.UtcNow));
        Assert.Throws<DomainException>(() => a.AcceptProposal(customer, 60));
        a.AcceptProposal(vendorUser, 60);
        a.ProposeReschedule(Start.AddHours(3), vendorUser, type); a.AcceptProposal(customer, 60);
        Assert.Equal(2, a.RescheduleCount);
        Assert.Throws<DomainException>(() => a.ProposeReschedule(Start.AddHours(4), customer, type));
    }
    [Fact]
    public void DepositReportIsNotPaymentAndVendorCancellationCreatesFullRefundLiability()
    {
        var a = Appointment.Create(Type(deposit: 200000), Guid.NewGuid(), Start, 2, null, null);
        a.ReportDeposit(); Assert.False(a.DepositPaid); Assert.Equal(AppointmentStatus.AwaitingDeposit, a.Status);
        a.MarkDepositPaid(false); a.Cancel("NCC nghỉ đột xuất", true, 24);
        Assert.Equal(200000, a.RefundDue); Assert.True(a.VendorSlaViolation); Assert.Null(a.RefundedAt);
        a.RecordRefund("BANK-REFUND-001"); Assert.NotNull(a.RefundedAt);
    }
    [Theory]
    [InlineData(1, 1)] [InlineData(10, 1)] [InlineData(11, 2)] [InlineData(201, 21)]
    public void TableQuantityAlwaysRoundsUp(int guests, int tables) => Assert.Equal(tables, ServiceItem.QuantityFor("Table", guests));

    [Fact]
    public void ComboDiscountAllocationPreservesDongAndNeverExceedsVendorConsent()
    {
        var ownerVendorId = Guid.NewGuid();
        var lines = Enumerable.Range(0, 7).Select(i => (Guid.NewGuid(), ownerVendorId, Guid.NewGuid(), $"Item{i}", 1000001m)).ToList();
        var order = ComboOrder.Create(Guid.NewGuid(), null, Start, 201, null, lines);
        order.SetPricing(.03m, "HCM", false);
        Assert.Equal(Math.Round(order.Subtotal * .03m), order.Items.Sum(i => i.DiscountShare));
        Assert.Equal(order.Subtotal - order.DiscountAmount, order.Total); Assert.NotNull(order.PriceLockUntil);
        var vendor = Vendor.Create(Guid.NewGuid(), "Vendor", "vendor", "Owner", "0900000000", "HCM", .08m);
        Assert.False(vendor.AllowInCombos); vendor.ConfigureComboParticipation(true, .05m); Assert.Equal(.05m, vendor.MaxComboDiscount);
        Assert.Throws<DomainException>(() => vendor.ConfigureComboParticipation(true, .31m));
        Assert.Throws<DomainException>(() => ComboOrder.Create(Guid.NewGuid(), null, Start, 201, null,
            new List<(Guid, Guid, Guid, string, decimal)> { (Guid.NewGuid(), ownerVendorId, Guid.NewGuid(), "A", 1), (Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "B", 1) }));
        var shares = ComboMoney.AllocateDiscount(new[] { 1m, 1m, 0m }, 1m);
        Assert.Equal(1m, shares.Sum()); Assert.Equal(0m, shares[2]); Assert.All(shares, share => Assert.True(share >= 0));
    }
    [Fact]
    public async Task ServerPricesVariantsAddonsTablesAndRejectsRegionAndUnverifiedVendor()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:"); await connection.OpenAsync();
        await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options); await db.Database.EnsureCreatedAsync();
        var owner = User.Create("Owner", "owner@test.vn", "0900000000", "hash", UserRole.VendorOwner); db.Users.Add(owner);
        var vendor = Vendor.Create(owner.Id, "Venue", "venue", "Owner", "0900000000", "HCM", .08m); vendor.SetVerified(true); vendor.ConfigureComboParticipation(true, .02m); db.Vendors.Add(vendor);
        var selections = new List<ComboSelection>();
        for (var n = 0; n < 3; n++)
        {
            var category = Category.Create($"Category {n}", $"cat{n}", null, null, .08m); db.Categories.Add(category);
            var listing = Listing.Create(vendor.Id, category.Id, $"Listing{n}", $"listing{n}", 1000000, 1000000, "Description", "HCM"); listing.Approve(); db.Listings.Add(listing);
            var item = ServiceItem.Create(vendor.Id, listing.Id, category.Id, $"Service{n}", 1000000, "Table", 1, 1000, "HCM", "Refund policy");
            var variant = ItemVariant.Create(item.Id, "Premium", 100000); var addon = ItemAddOn.Create(item.Id, "Photobooth", 500000, false);
            item.Variants.Add(variant); item.AddOns.Add(addon); item.Submit(); item.Review(true, null); db.ServiceItems.Add(item);
            selections.Add(new(item.Id, new() { variant.Id }, new() { addon.Id }));
        }
        await db.SaveChangesAsync();
        var request = new ComboPriceCommand(null, "HCM", Start, 201, selections);
        var price = await ComboPricing.CalculateAsync(db, request, null, default);
        Assert.All(price.Lines, l => { Assert.Equal(21, l.Quantity); Assert.Equal(23600000, l.Price); });
        Assert.Equal(.02m, price.DiscountPercent); Assert.Equal(price.DiscountAmount, price.Lines.Sum(l => l.DiscountShare));
        await Assert.ThrowsAsync<DomainException>(() => ComboPricing.CalculateAsync(db, request with { Region = "Hanoi" }, null, default));

        // Presets are editable only within their owning vendor. Approved same-vendor replacements
        // and extra categories work; detaching a template must never enable a mixed-vendor cart.
        var specialistOwner = User.Create("Specialist", "specialist@test.vn", "0900000002", "hash", UserRole.VendorOwner); db.Users.Add(specialistOwner);
        var specialist = Vendor.Create(specialistOwner.Id, "Specialist", "specialist", "Specialist", "0900000002", "HCM", .08m);
        specialist.SetVerified(true); specialist.ConfigureComboParticipation(true, .08m); db.Vendors.Add(specialist);
        var defaultItem = await db.ServiceItems.SingleAsync(i => i.Id == selections[0].ServiceItemId);
        var specialistListing = Listing.Create(specialist.Id, defaultItem.CategoryId, "Alternative", "alternative", 500000, 500000, "Alternative", "HCM"); specialistListing.Approve(); db.Listings.Add(specialistListing);
        var alternative = ServiceItem.Create(specialist.Id, specialistListing.Id, defaultItem.CategoryId, "Approved replacement", 500000, "Package", 1, 1000, "HCM", "Policy"); alternative.Submit(); alternative.Review(true, null); db.ServiceItems.Add(alternative);
        var ownListing = Listing.Create(vendor.Id, defaultItem.CategoryId, "Own alternative", "own-alternative", 600000, 600000, "Own alternative", "HCM"); ownListing.Approve(); db.Listings.Add(ownListing);
        var ownAlternative = ServiceItem.Create(vendor.Id, ownListing.Id, defaultItem.CategoryId, "Same vendor replacement", 600000, "Package", 1, 1000, "HCM", "Policy"); ownAlternative.Submit(); ownAlternative.Review(true, null); db.ServiceItems.Add(ownAlternative);
        var template = ComboTemplate.Create("Editable starter", "Popular", "Modern", null, null, 0); template.SetScope(vendor.Id, "HCM", null, true);
        foreach (var selection in selections.Take(2))
        {
            var item = await db.ServiceItems.SingleAsync(i => i.Id == selection.ServiceItemId);
            var slot = ComboTemplateItem.Create(template.Id, item.CategoryId, item.ListingId, false); slot.ConfigureItems(item.Id, "[]"); template.Items.Add(slot);
        }
        db.ComboTemplates.Add(template); await db.SaveChangesAsync();
        var edited = request with { TemplateId = template.Id, Selections = new() { new(ownAlternative.Id, null, null), selections[1], selections[2] } };
        var editedPrice = await ComboPricing.CalculateAsync(db, edited, null, default);
        Assert.Equal(3, editedPrice.Lines.Count); Assert.All(editedPrice.Lines, line => Assert.Equal(vendor.Id, line.VendorId));
        var mixed = edited with { Selections = new() { new(alternative.Id, null, null), selections[1], selections[2] } };
        await Assert.ThrowsAsync<DomainException>(() => ComboPricing.CalculateAsync(db, mixed, null, default));
        await Assert.ThrowsAsync<DomainException>(() => ComboPricing.CalculateAsync(db, mixed with { TemplateId = null }, null, default));
        var separateVendorCart = await ComboPricing.CalculateAsync(db, mixed with { TemplateId = null, Selections = new() { new(alternative.Id, null, null) } }, null, default);
        Assert.Single(separateVendorCart.Lines); Assert.Equal(specialist.Id, separateVendorCart.Lines[0].VendorId);
        template.SetScope(vendor.Id, "HCM", .08m, true); await db.SaveChangesAsync();
        Assert.Equal(3, (await ComboPricing.CalculateAsync(db, edited with { TemplateId = null }, null, default)).Lines.Count);
        vendor.ConfigureComboParticipation(true, .08m); await db.SaveChangesAsync();
        var removedSlot = await ComboPricing.CalculateAsync(db, request with { TemplateId = template.Id, Selections = new() { selections[1] } }, null, default);
        Assert.Equal(0m, removedSlot.DiscountPercent); // Full-service preset discount is not retained after dropping required contents.
        Assert.Contains(removedSlot.Warnings, text => text.Contains("bỏ hạng mục"));

        var emptyCategory = Category.Create("Category without products", "empty-category", null, null, .08m); db.Categories.Add(emptyCategory);
        var legacy = ComboTemplate.Create("Legacy listing-only preset", "Saving", "Modern", null, null, 9);
        legacy.Items.Add(ComboTemplateItem.Create(legacy.Id, defaultItem.CategoryId, defaultItem.ListingId, false)); db.ComboTemplates.Add(legacy); await db.SaveChangesAsync();
        var builder = await new GetComboBuilderQueryHandler(db).Handle(new(null, 201), default);
        Assert.Empty(builder.Categories.Single(c => c.CategoryId == emptyCategory.Id).Options); // Category menu remains complete.
        Assert.DoesNotContain(builder.Templates, t => t.Id == legacy.Id); // No broken, listing-only presets in a real cart.
        var displayed = builder.Templates.Single(t => t.Id == template.Id);
        Assert.Equal(42000000, displayed.EstimatedTotal); Assert.Equal(vendor.BrandName, displayed.OwnerVendorName); Assert.Equal(201, builder.PreviewGuestCount);

        vendor.SetVerified(false); await db.SaveChangesAsync();
        await Assert.ThrowsAsync<DomainException>(() => ComboPricing.CalculateAsync(db, request, null, default));
    }

    [Fact]
    public async Task ComboDraftCanResumeAndSubmissionIsAtomicIfNotificationFails()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:"); await connection.OpenAsync();
        await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options); await db.Database.EnsureCreatedAsync();
        var owner = User.Create("Owner", "owner2@test.vn", "0900000000", "hash", UserRole.VendorOwner); db.Users.Add(owner);
        var customer = User.Create("Customer", "customer2@test.vn", "0900000001", "hash", UserRole.Customer); db.Users.Add(customer);
        var vendor = Vendor.Create(owner.Id, "Studio", "studio", "Owner", "0900000000", "HCM", .08m); vendor.SetVerified(true); vendor.ConfigureComboParticipation(true, .08m); db.Vendors.Add(vendor);
        var category = Category.Create("Photo", "photo", null, null, .08m); db.Categories.Add(category);
        var listing = Listing.Create(vendor.Id, category.Id, "Photo Package", "photo-package", 2000000, 2000000, "Description", "HCM"); listing.Approve(); db.Listings.Add(listing);
        var item = ServiceItem.Create(vendor.Id, listing.Id, category.Id, "Photo", 2000000, "Package", 1, 1000, "HCM", "Refund policy"); item.Submit(); item.Review(true, null); db.ServiceItems.Add(item);
        await db.SaveChangesAsync();
        var selections = new List<ComboSelection> { new(item.Id, null, null) };
        var command = new SubmitComboOrderCommand(null, new(), Start, 100, customer.PhoneNumber, null, "HCM", selections, 2000000, true);
        var handler = new SubmitComboOrderCommandHandler(db, new Current(customer.Id), new Notification());
        var draft = await handler.Handle(command, default);
        Assert.Equal("Draft", draft.Status); Assert.Equal(0, await db.Leads.CountAsync()); Assert.Equal(0, await db.BudgetItems.CountAsync());
        var failing = new SubmitComboOrderCommandHandler(db, new Current(customer.Id), new FailingNotification());
        await Assert.ThrowsAsync<InvalidOperationException>(() => failing.Handle(command with { SaveAsDraft = false, DraftId = draft.Id }, default));
        Assert.Equal(1, await db.ComboOrders.CountAsync()); Assert.Equal(0, await db.Leads.CountAsync()); Assert.Equal(0, await db.BudgetItems.CountAsync());
        var submitted = await handler.Handle(command with { SaveAsDraft = false, DraftId = draft.Id }, default);
        Assert.Equal("Submitted", submitted.Status); Assert.Equal(2, await db.ComboOrders.CountAsync()); Assert.Equal(1, await db.Leads.CountAsync()); Assert.Equal(1, await db.BudgetItems.CountAsync());
        Assert.Equal(ComboOrderStatus.Cancelled, (await db.ComboOrders.SingleAsync(o => o.Id == draft.Id)).Status); // Retain the original draft, never erase revision history.
        Assert.Equal(submitted.Id, (await db.CustomerOrderStates.SingleAsync(s => s.Kind == "combo" && s.EntityId == draft.Id)).SupersededById);
        Assert.NotNull(submitted.Items.Single().VoucherCode);
    }

    [SqlServerFact]
    public async Task SqlServerOneHundredParallelCustomersCannotOverbookCapacityOne()
    {
        var database = $"WeddingPlanTest_{Guid.NewGuid():N}";
        var source = Environment.GetEnvironmentVariable("WEDDING_TEST_SQLSERVER");
        var builder = new SqlConnectionStringBuilder(string.IsNullOrWhiteSpace(source) || source == "1" ? "Server=localhost;Integrated Security=true;TrustServerCertificate=true;Connection Timeout=5" : source) { InitialCatalog = database, MaxPoolSize = 120 };
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlServer(builder.ConnectionString, sql => sql.CommandTimeout(30)).Options;
        var ids = new List<Guid>(); Guid typeId;
        await using var setup = new ApplicationDbContext(options);
        try
        {
            await setup.Database.EnsureCreatedAsync();
            var owner = User.Create("Owner", "owner@test.vn", "0900000001", "hash", UserRole.VendorOwner); setup.Users.Add(owner);
            var vendor = Vendor.Create(owner.Id, "Test Vendor", "test-vendor", "Owner", "0900000001", "HCM", .08m); setup.Vendors.Add(vendor);
            var type = AppointmentType.Create(vendor.Id, "Tư vấn", null, 60, 1, "0,1,2,3,4,5,6", TimeSpan.FromHours(9), TimeSpan.FromHours(18), 0, false); typeId = type.Id; setup.AppointmentTypes.Add(type);
            for (var i = 0; i < 100; i++) { var customer = User.Create($"Customer {i}", $"c{i}@test.vn", $"090000{i:0000}", "hash", UserRole.Customer); setup.Users.Add(customer); ids.Add(customer.Id); }
            await setup.SaveChangesAsync();
            var results = await Task.WhenAll(ids.Select(async uid =>
            {
                await using var db = new ApplicationDbContext(options);
                try { await new BookAppointmentCommandHandler(db, new Current(uid), new Notification()).Handle(new(typeId, Start, 2, null, null), default); return true; }
                catch (DomainException) { return false; }
            }));
            Assert.Equal(1, results.Count(success => success)); Assert.Equal(1, await setup.Appointments.AsNoTracking().CountAsync());
        }
        finally
        {
            // This exact GUID database was created by this test, never an application/user database.
            Assert.StartsWith("WeddingPlanTest_", database); await setup.Database.EnsureDeletedAsync();
        }
    }
    private sealed record Current(Guid Id) : ICurrentUserService { public Guid? UserId => Id; public string? Email => "test@test.vn"; public string? Role => "Customer"; }
    private sealed class Notification : INotificationSender { public Task SendAsync(string channel, string recipient, string message, CancellationToken cancellationToken = default) => Task.CompletedTask; }
    private sealed class FailingNotification : INotificationSender { public Task SendAsync(string channel, string recipient, string message, CancellationToken cancellationToken = default) => throw new InvalidOperationException("Simulated outbox failure"); }
}
public sealed class SqlServerFactAttribute : FactAttribute
{
    public SqlServerFactAttribute() { if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("WEDDING_TEST_SQLSERVER"))) Skip = "Opt in with WEDDING_TEST_SQLSERVER; creates and drops only a fresh WeddingPlanTest_<guid> database."; }
}
