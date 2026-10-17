using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Wedding.Application.Common.Interfaces;
using Wedding.Application.Features.Combos;
using Wedding.Application.Features.Contracts.Commands.ConfirmContract;
using Wedding.Application.Features.Planner;
using Wedding.Application.Features.WeddingTools.Commands.SaveBudgetItem;
using Wedding.Domain.Entities;
using Wedding.Domain.Enums;
using Wedding.Infrastructure.Persistence;

namespace Wedding.Tests;

public class ComboBudgetTests
{
    [Fact]
    public async Task WizardHasExplicitStableProvenanceAndCartReplacesOnlyMatchingSeedEstimate()
    {
        await using var f = await Fixture.Create();
        var seeds = await f.Db.BudgetItems.ToListAsync(); Assert.Equal(14, seeds.Count); Assert.All(seeds, b => Assert.NotNull(b.PlanTemplateKey));
        var price = await f.Price(); Assert.Equal(40000000, price.PlannerReplacedEstimate); Assert.Equal(60000000, price.PlannerReservedBeforeCart); Assert.Equal(36000000, price.PlannerRemaining);
        var original = seeds.Single(b => b.PlanTemplateKey == "venue");
        var order = await f.Submit();
        Assert.Equal(14, await f.Db.BudgetItems.CountAsync());
        var linked = await f.Db.ComboOrderItems.SingleAsync(i => i.ComboOrderId == order.Id);
        Assert.Equal(original.Id, linked.BudgetItemId); Assert.Equal(4000000, original.PlannedCost); Assert.Equal("venue", original.PlanTemplateKey);
        await new CancelComboOrderCommandHandler(f.Db, f.Current).Handle(new(order.Id), default);
        Assert.Equal(14, await f.Db.BudgetItems.CountAsync()); Assert.Equal(40000000, original.PlannedCost); Assert.Equal("venue", original.PlanTemplateKey); Assert.Null(original.Notes);
    }
    [Fact]
    public async Task ManualChangesClearProvenanceAndAreNotOverwrittenEvenAtTheOriginalAmount()
    {
        await using var f = await Fixture.Create(); var venue = await f.VenueBudget();
        await new SaveBudgetItemCommandHandler(f.Db, f.Current).Handle(new(venue.Id, venue.ItemName, 40000000, 0, "Tự lựa chọn ngân sách này"), default);
        Assert.Null(venue.PlanTemplateKey);
        var price = await f.Price(); Assert.Equal(0, price.PlannerReplacedEstimate); Assert.Equal(-4000000, price.PlannerRemaining);
        Assert.Contains(price.Warnings, text => text.Contains("có thể trùng dự toán"));
        var order = await f.Submit(); Assert.Equal(15, await f.Db.BudgetItems.CountAsync()); Assert.Equal(40000000, venue.PlannedCost);
        Assert.NotEqual(venue.Id, (await f.Db.ComboOrderItems.SingleAsync(i => i.ComboOrderId == order.Id)).BudgetItemId);
    }
    [Fact]
    public async Task PaidAndLegacyUnmarkedRowsNeverGetSilentlyReused()
    {
        await using var f = await Fixture.Create(); var venue = await f.VenueBudget();
        venue.UpdateCosts(40000000, 1000000, "Đã chi", true); await f.Db.SaveChangesAsync();
        var order = await f.Submit(); Assert.Equal(15, await f.Db.BudgetItems.CountAsync()); Assert.Equal(1000000, venue.ActualCost);
        await new CancelComboOrderCommandHandler(f.Db, f.Current).Handle(new(order.Id), default);
        Assert.Equal(14, await f.Db.BudgetItems.CountAsync()); Assert.Equal(1000000, venue.ActualCost);
    }
    [Fact]
    public async Task ReservationIncludesOtherPlannedActualOverspendAndUnrepresentedContracts()
    {
        await using var f = await Fixture.Create();
        f.Db.BudgetItems.Add(BudgetItem.Create(f.Customer.Id, null, "Khoản khác vượt dự toán", 2000000, 5000000));
        var lead = Lead.Create(f.Customer.Id, f.Vendor.Id, f.Item.ListingId, f.Customer.PhoneNumber, DateTime.UtcNow.AddDays(10), 100, 10000000, null); f.Db.Leads.Add(lead);
        var contract = BookingContract.Create(lead.Id, null, f.Vendor.Id, f.Customer.Id, 10000000, 2000000, null, DateTime.UtcNow.AddDays(10)); contract.ConfirmByCustomer(); f.Db.BookingContracts.Add(contract);
        await f.Db.SaveChangesAsync();
        var price = await f.Price(); Assert.Equal(75000000, price.PlannerReservedBeforeCart); Assert.Equal(21000000, price.PlannerRemaining);
        var represented = BudgetItem.Create(f.Customer.Id, null, "Hợp đồng đã ghi nhận", 10000000, 2000000); represented.LinkContract(contract.Id); f.Db.BudgetItems.Add(represented); await f.Db.SaveChangesAsync();
        var again = await f.Price(); Assert.Equal(price.PlannerRemaining, again.PlannerRemaining); // Never count a contract twice.
    }
    [Fact]
    public async Task ConfirmedComboContractReusesTheManagedEstimateAndClearsSeedProvenance()
    {
        await using var f = await Fixture.Create(); var venue = await f.VenueBudget(); var order = await f.Submit();
        var item = await f.Db.ComboOrderItems.SingleAsync(i => i.ComboOrderId == order.Id);
        var contract = BookingContract.Create(item.LeadId!.Value, null, f.Vendor.Id, f.Customer.Id, 5000000, 1000000, null, DateTime.UtcNow.AddDays(10)); f.Db.BookingContracts.Add(contract); await f.Db.SaveChangesAsync();
        await new ConfirmContractCommandHandler(f.Db, f.Current).Handle(new(contract.Id), default);
        Assert.Equal(14, await f.Db.BudgetItems.CountAsync()); Assert.Equal(contract.Id, venue.ContractId); Assert.Equal(5000000, venue.PlannedCost); Assert.Equal(1000000, venue.ActualCost); Assert.Null(venue.PlanTemplateKey);
        Assert.Equal(1, await f.Db.BudgetItems.CountAsync(b => b.ContractId == contract.Id));
    }
    [Fact]
    public async Task CancellingNeverRestoresOverAManuallyEditedComboEstimate()
    {
        await using var f = await Fixture.Create(); var venue = await f.VenueBudget(); var order = await f.Submit();
        await new SaveBudgetItemCommandHandler(f.Db, f.Current).Handle(new(venue.Id, "Ngân sách tự sửa", 4500000, 0, "Giữ nguyên của tôi"), default);
        await new CancelComboOrderCommandHandler(f.Db, f.Current).Handle(new(order.Id), default);
        Assert.Equal(4500000, venue.PlannedCost); Assert.Equal("Giữ nguyên của tôi", venue.Notes); Assert.Null(venue.PlanTemplateKey);
    }
    [Fact]
    public async Task DraftDoesNotReserveAndEditorUsesOwnersSharedSeeds()
    {
        await using var f = await Fixture.Create(); var profile = await f.Db.WeddingProfiles.SingleAsync();
        var editor = User.Create("Editor", "editor@test.vn", "0900000002", "hash", UserRole.Customer); f.Db.Users.Add(editor); f.Db.WeddingMembers.Add(WeddingMember.Create(profile.Id, editor.Id, "Editor")); await f.Db.SaveChangesAsync();
        var handler = new SubmitComboOrderCommandHandler(f.Db, new Current(editor.Id), new Notification());
        var draft = await handler.Handle(f.Command with { SaveAsDraft = true }, default); Assert.Equal(14, await f.Db.BudgetItems.CountAsync());
        var price = await ComboPricing.CalculateAsync(f.Db, f.PriceCommand, editor.Id, default); Assert.Equal(36000000, price.PlannerRemaining);
        var submitted = await handler.Handle(f.Command with { DraftId = draft.Id }, default); Assert.Equal(14, await f.Db.BudgetItems.CountAsync());
        var rowId = (await f.Db.ComboOrderItems.SingleAsync(i => i.ComboOrderId == submitted.Id)).BudgetItemId;
        var row = await f.Db.BudgetItems.SingleAsync(b => b.Id == rowId); Assert.Equal(f.Customer.Id, row.CustomerId); Assert.Equal(profile.Id, row.WeddingProfileId);
    }
    [Fact]
    public async Task PasswordlessAccountCanSaveDraftWithoutPhoneButCannotSubmitLeads()
    {
        await using var f = await Fixture.Create("chua-cap-nhat");
        var handler = new SubmitComboOrderCommandHandler(f.Db, f.Current, new Notification());
        var draft = await handler.Handle(f.Command with { SaveAsDraft = true, PhoneNumber = null }, default);
        Assert.Equal("Draft", draft.Status); Assert.Equal(0, await f.Db.Leads.CountAsync()); Assert.Equal(14, await f.Db.BudgetItems.CountAsync());
        await Assert.ThrowsAsync<Wedding.Domain.Exceptions.DomainException>(() => handler.Handle(f.Command with { DraftId = draft.Id, PhoneNumber = null }, default));
        Assert.Equal(1, await f.Db.ComboOrders.CountAsync(o => o.Status == ComboOrderStatus.Draft)); Assert.Equal(0, await f.Db.Leads.CountAsync());
    }
    [Fact]
    public async Task ExpiredComboReleasesManagedReserveAndRestoresWizardSeed()
    {
        await using var f = await Fixture.Create(); var venue = await f.VenueBudget(); var submitted = await f.Submit();
        var order = await f.Db.ComboOrders.Include(o => o.Items).SingleAsync(o => o.Id == submitted.Id); order.ExpirePriceLock();
        foreach (var item in order.Items) await ComboBudgetSynchronization.ReleaseAsync(f.Db, item, default);
        await f.Db.SaveChangesAsync();
        Assert.Equal(40000000, venue.PlannedCost); Assert.Equal("venue", venue.PlanTemplateKey); Assert.Equal(14, await f.Db.BudgetItems.CountAsync());
        Assert.Equal(40000000, (await f.Price()).PlannerReplacedEstimate);
    }
    [Fact]
    public async Task ContractFallbackPreservesManualEstimateAndExplicitlyWarnsOfPotentialDuplication()
    {
        await using var f = await Fixture.Create(); var venue = await f.VenueBudget(); var order = await f.Submit();
        await new SaveBudgetItemCommandHandler(f.Db, f.Current).Handle(new(venue.Id, "Tự lập dự toán", 4500000, 0, "Không tự sửa"), default);
        var item = await f.Db.ComboOrderItems.SingleAsync(i => i.ComboOrderId == order.Id);
        var contract = BookingContract.Create(item.LeadId!.Value, null, f.Vendor.Id, f.Customer.Id, 5000000, 1000000, null, DateTime.UtcNow.AddDays(10)); f.Db.BookingContracts.Add(contract); await f.Db.SaveChangesAsync();
        await new ConfirmContractCommandHandler(f.Db, f.Current).Handle(new(contract.Id), default);
        Assert.Equal(15, await f.Db.BudgetItems.CountAsync()); Assert.Equal(4500000, venue.PlannedCost); Assert.Equal("Không tự sửa", venue.Notes);
        var contractBudget = await f.Db.BudgetItems.SingleAsync(b => b.ContractId == contract.Id); Assert.Contains("trùng dự toán", contractBudget.Notes);
        Assert.True(await f.Db.Notifications.AnyAsync(n => n.UserId == f.Customer.Id && n.Message.Contains("trùng dự toán")));
    }
    [Fact]
    public async Task ConcurrentManualEditCannotBeLostByAStaleAutomaticUpdate()
    {
        await using var f = await Fixture.Create(); var venue = await f.VenueBudget();
        await using var other = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(f.Connection).Options);
        var manual = await other.BudgetItems.SingleAsync(b => b.Id == venue.Id); manual.UpdateCosts(39000000, 0, "Manual concurrent edit", false); await other.SaveChangesAsync();
        venue.UpdateCosts(4000000, 0, "Stale auto synchronization", false, preservePlanTemplate: true);
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => f.Db.SaveChangesAsync());
        var saved = await other.BudgetItems.AsNoTracking().SingleAsync(b => b.Id == venue.Id); Assert.Equal(39000000, saved.PlannedCost); Assert.Equal("Manual concurrent edit", saved.Notes);
    }
    private sealed record Current(Guid Id) : ICurrentUserService { public Guid? UserId => Id; public string? Email => "customer@test.vn"; public string? Role => "Customer"; }
    private sealed class Notification : INotificationSender { public Task SendAsync(string channel, string recipient, string message, CancellationToken cancellationToken = default) => Task.CompletedTask; }
    private sealed class Fixture : IAsyncDisposable
    {
        public SqliteConnection Connection = null!; public ApplicationDbContext Db = null!; public User Customer = null!; public Vendor Vendor = null!; public ServiceItem Item = null!;
        public Current Current => new(Customer.Id);
        public ComboPriceCommand PriceCommand => new(null, "HCM", DateTime.UtcNow.AddDays(10), 100, new() { new(Item.Id, null, null) });
        public SubmitComboOrderCommand Command => new(null, new(), DateTime.UtcNow.AddDays(10), 100, Customer.PhoneNumber, null, "HCM", PriceCommand.Selections, 4000000);
        public Task<ComboPriceDto> Price() => ComboPricing.CalculateAsync(Db, PriceCommand, Customer.Id, default);
        public Task<ComboOrderDto> Submit() => new SubmitComboOrderCommandHandler(Db, Current, new Notification()).Handle(Command, default);
        public Task<BudgetItem> VenueBudget() => Db.BudgetItems.SingleAsync(b => b.PlanTemplateKey == "venue");
        public static async Task<Fixture> Create(string phone = "0900000000")
        {
            var f = new Fixture(); f.Connection = new SqliteConnection("Data Source=:memory:"); await f.Connection.OpenAsync(); f.Db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(f.Connection).Options); await f.Db.Database.EnsureCreatedAsync();
            f.Customer = User.Create("Customer", "customer@test.vn", phone, "hash", UserRole.Customer); f.Db.Users.Add(f.Customer);
            var owner = User.Create("Vendor", "vendor@test.vn", "0900000001", "hash", UserRole.VendorOwner); f.Db.Users.Add(owner);
            f.Vendor = Vendor.Create(owner.Id, "Vendor", "vendor", "Vendor", "0900000001", "HCM", .08m); f.Vendor.SetVerified(true); f.Vendor.ConfigureComboParticipation(true, .08m); f.Db.Vendors.Add(f.Vendor);
            var category = await f.Db.Categories.SingleAsync(c => c.Slug == "tiem-cuoi-venues");
            var listing = Listing.Create(f.Vendor.Id, category.Id, "Venue", "venue", 4000000, 4000000, "Venue", "HCM"); listing.Approve(); f.Db.Listings.Add(listing);
            f.Item = ServiceItem.Create(f.Vendor.Id, listing.Id, category.Id, "Venue package", 4000000, "Package", 1, 1000, "HCM", "Policy"); f.Item.SetDailyCapacity(10); f.Item.Submit(); f.Item.Review(true, null); f.Db.ServiceItems.Add(f.Item); await f.Db.SaveChangesAsync();
            await new SaveWeddingProfileCommandHandler(f.Db, f.Current).Handle(new("Minh", "Anh", DateTime.UtcNow.AddDays(10), "HCM", 100000000, 100, "Modern", null, true), default); return f;
        }
        public async ValueTask DisposeAsync() { await Db.DisposeAsync(); await Connection.DisposeAsync(); }
    }
}
