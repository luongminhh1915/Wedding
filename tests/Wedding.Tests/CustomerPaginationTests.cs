using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Wedding.Application.Common.Interfaces;
using Wedding.Application.Features.Combos;
using Wedding.Application.Features.CustomerOrders;
using Wedding.Application.Features.Leads.DTOs;
using Wedding.Application.Features.Quotes;
using Wedding.Application.Features.Contracts.DTOs;
using Wedding.Domain.Entities;
using Wedding.Domain.Enums;
using Wedding.Infrastructure.Persistence;

namespace Wedding.Tests;

public class CustomerPaginationTests
{
    [Fact]
    public async Task CatalogPagesHydrateCartOutsideThePageAndRetainSearchAndVendor()
    {
        await using var f = await Fixture.Create();
        await Catalog(f, "newest");
    }
    private static async Task Catalog(Fixture f, string sort)
    {
        var handler = new GetComboCatalogPageQueryHandler(f.Db);
        var presets = await handler.Handle(new(PageSize: 2), default);
        var presetNext = await handler.Handle(new(Page: 2, PageSize: 2), default);
        Assert.Equal(7, presets.TotalCount); Assert.Equal(2, presets.Builder.Templates.Count);
        Assert.Empty(presets.Builder.Templates.Select(t => t.Id).Intersect(presetNext.Builder.Templates.Select(t => t.Id)));
        Assert.All(presets.Builder.Templates, t => Assert.Equal(f.Vendor.Id, t.OwnerVendorId));
        var hydrated = await handler.Handle(new(PageSize: 2, TemplateId: f.Templates[6].Id), default);
        Assert.Equal(f.Templates[6].Id, hydrated.SelectedTemplate!.Id);
        Assert.Contains(hydrated.SelectedOptions, i => i.ServiceItemId == f.Items[6].Id);
        var request = new GetComboCatalogPageQuery(f.Category.Id, f.Vendor.Id, Search: "Service", Sort: sort, PageSize: 2, SelectedItemIds: [f.Items[0].Id]);
        var first = await handler.Handle(request, default);
        var second = await handler.Handle(request with { Page = 2 }, default);
        Assert.Equal(7, first.TotalCount); Assert.Equal(2, first.Builder.Categories.SelectMany(c => c.Options).Count());
        Assert.Empty(first.Builder.Categories.SelectMany(c => c.Options).Select(i => i.ServiceItemId).Intersect(second.Builder.Categories.SelectMany(c => c.Options).Select(i => i.ServiceItemId)));
        Assert.Contains(first.SelectedOptions, i => i.ServiceItemId == f.Items[0].Id);
        Assert.All(second.Builder.Categories.SelectMany(c => c.Options), i => Assert.Equal(f.Vendor.Id, i.VendorId));
        var empty = await handler.Handle(request with { Search = "No such title" }, default); Assert.Equal(0, empty.TotalCount); Assert.Equal(1, empty.Page);
        var clamped = await handler.Handle(request with { Page = 999, PageSize = 999 }, default); Assert.Equal(10, clamped.PageSize); Assert.Equal(1, clamped.Page);
        f.Owner.SetStatus(false); await f.Db.SaveChangesAsync();
        var hidden = await handler.Handle(request, default); Assert.Empty(hidden.SelectedOptions); Assert.Equal(0, hidden.TotalCount); Assert.Empty(hidden.Builder.Vendors!);
    }
    [Fact]
    public async Task SavedCombosAreOwnerScopedFilteredPagedAndNotificationCanLocateAnArchive()
    {
        await using var f = await Fixture.Create(); await Saved(f);
    }
    private static async Task Saved(Fixture f)
    {
        var archived = f.Orders[0]; var state = CustomerOrderState.Create(f.Customer.Id, "combo", archived.Id); state.Archive(true); f.Db.CustomerOrderStates.Add(state); await f.Db.SaveChangesAsync();
        var handler = new GetMyComboOrdersPageQueryHandler(f.Db, f.Current);
        var first = await handler.Handle(new(PageSize: 2), default); var second = await handler.Handle(new(Page: 2, PageSize: 2), default);
        Assert.Equal(6, first.TotalCount); Assert.Equal(2, first.Items.Count); Assert.Empty(first.Items.Select(i => i.Id).Intersect(second.Items.Select(i => i.Id)));
        var target = await handler.Handle(new(PageSize: 2, Filter: "active", FocusId: archived.Id), default);
        Assert.Equal("archived", target.Filter); Assert.True(target.FocusFound); Assert.Equal(archived.Id, Assert.Single(target.Items).Id);
        var other = await new GetMyComboOrdersPageQueryHandler(f.Db, new Current(Guid.NewGuid())).Handle(new(FocusId: archived.Id), default); Assert.Empty(other.Items); Assert.False(other.FocusFound);
    }
    [Fact]
    public async Task OrderPagesKeepGroupsTogetherAndFiltersOperateBeforePaging()
    {
        await using var f = await Fixture.Create(); await Groups(f);
    }
    private static async Task Groups(Fixture f)
    {
        var handler = new GetCustomerOrderPageQueryHandler(f.Db, f.Current);
        var first = await handler.Handle(new("lead", PageSize: 2), default); var second = await handler.Handle(new("lead", Page: 2, PageSize: 2), default);
        Assert.Equal(7, first.TotalCount); var rows = Assert.IsType<List<LeadDto>>(first.Items); Assert.Equal(4, rows.Count);
        Assert.Equal(4, first.Metadata.Count); Assert.Empty(rows.Select(i => i.Id).Intersect(Assert.IsType<List<LeadDto>>(second.Items).Select(i => i.Id)));
        var lead = await f.Db.Leads.FirstAsync(); lead.Cancel("Test cancellation only"); await f.Db.SaveChangesAsync();
        var closed = await handler.Handle(new("lead", Filter: "closed"), default); Assert.Equal(1, closed.TotalCount); Assert.Equal(lead.Id, Assert.Single(Assert.IsType<List<LeadDto>>(closed.Items)).Id);
        var state = CustomerOrderState.Create(f.Customer.Id, "lead", lead.Id); state.Archive(true); f.Db.CustomerOrderStates.Add(state); await f.Db.SaveChangesAsync();
        var focused = await handler.Handle(new("lead", PageSize: 1, Filter: "active", FocusId: lead.Id), default); Assert.Equal("archived", focused.Filter); Assert.Equal(lead.Id, Assert.Single(Assert.IsType<List<LeadDto>>(focused.Items)).Id);
        var foreign = await new GetCustomerOrderPageQueryHandler(f.Db, new Current(Guid.NewGuid())).Handle(new("lead", FocusId: lead.Id), default); Assert.Equal(0, foreign.TotalCount); Assert.False(foreign.FocusFound);
        var scoped = await CustomerOrderJourney.LoadAsync(f.Db, f.Customer.Id, default, [f.Orders[1].Id]); Assert.Single(scoped, i => i.Kind == "combo"); Assert.Equal(2, scoped.Count(i => i.Kind == "lead"));
        foreach (var related in await f.Db.Leads.ToListAsync()) {
            f.Db.Quotes.Add(Quote.Create(related.Id, f.Vendor.Id, f.Customer.Id, "[]", 1000000, 100000, "Synthetic terms", 7, 1));
            f.Db.BookingContracts.Add(BookingContract.Create(related.Id, null, f.Vendor.Id, f.Customer.Id, 1000000, 100000, null, DateTime.UtcNow.AddDays(30).Date));
        }
        await f.Db.SaveChangesAsync();
        var quotes = await handler.Handle(new("quote", Page: 2, PageSize: 2, Filter: "pending"), default); Assert.Equal(7, quotes.TotalCount); Assert.Equal(4, Assert.IsType<List<QuoteDto>>(quotes.Items).Count);
        var contracts = await handler.Handle(new("contract", Page: 2, PageSize: 2, Filter: "pending"), default); Assert.Equal(7, contracts.TotalCount); Assert.Equal(4, Assert.IsType<List<ContractDto>>(contracts.Items).Count);
    }
    [SqlServerFact]
    public async Task SqlServerCatalogSortingAndGroupedPagingMatchRealProvider()
    {
        await using var f = await Fixture.Create(sql: true);
        await Groups(f); await Saved(f);
        var page = await new GetComboCatalogPageQueryHandler(f.Db).Handle(new(f.Category.Id, f.Vendor.Id, Sort: "price-desc", PageSize: 2), default);
        Assert.Equal(f.Items.Max(i => i.BasePrice), page.Builder.Categories.SelectMany(c => c.Options).First().Price);
        await Catalog(f, "price-desc");
    }
    private sealed record Current(Guid Id) : ICurrentUserService { public Guid? UserId => Id; public string? Email => null; public string? Role => "Customer"; }
    private sealed class Fixture : IAsyncDisposable
    {
        public ApplicationDbContext Db = null!; private SqliteConnection? connection; private string? databaseName;
        public User Customer = null!, Owner = null!; public Vendor Vendor = null!; public Category Category = null!; public List<ServiceItem> Items = []; public List<ComboOrder> Orders = []; public List<ComboTemplate> Templates = [];
        public Current Current => new(Customer.Id);
        public static async Task<Fixture> Create(bool sql = false)
        {
            var f = new Fixture(); var options = new DbContextOptionsBuilder<ApplicationDbContext>();
            if (sql) { f.databaseName = $"WeddingPlanTest_Pagination_{Guid.NewGuid():N}"; options.UseSqlServer($"Server=localhost;Database={f.databaseName};Integrated Security=true;TrustServerCertificate=true;Connection Timeout=5"); }
            else { f.connection = new SqliteConnection("Data Source=:memory:"); await f.connection.OpenAsync(); options.UseSqlite(f.connection); }
            f.Db = new(options.Options); if (sql) await f.Db.Database.MigrateAsync(); else await f.Db.Database.EnsureCreatedAsync();
            f.Customer = User.Create("Synthetic Customer", "customer@pagination.test", "0900000000", "hash", UserRole.Customer); f.Owner = User.Create("Synthetic Owner", "owner@pagination.test", "0900000001", "hash", UserRole.VendorOwner); f.Db.Users.AddRange(f.Customer, f.Owner);
            f.Vendor = Vendor.Create(f.Owner.Id, "Pagination Shop", "pagination-shop", "Owner", f.Owner.PhoneNumber, "HCM", .08m); f.Vendor.SetVerified(true); f.Vendor.ConfigureComboParticipation(true, .08m); f.Db.Vendors.Add(f.Vendor);
            f.Category = await f.Db.Categories.SingleAsync(c => c.Slug == "tiem-cuoi-venues"); var photo = await f.Db.Categories.SingleAsync(c => c.Slug == "quay-chup-photo-video");
            var date = DateTime.UtcNow.AddDays(30).Date;
            for (var n = 0; n < 7; n++) {
                var listing = Listing.Create(f.Vendor.Id, f.Category.Id, $"Service {n}", $"service-{n}", 1000000 + n, 1000000 + n, "Synthetic test service", "HCM"); listing.Approve(); f.Db.Listings.Add(listing);
                var item = ServiceItem.Create(f.Vendor.Id, listing.Id, f.Category.Id, $"Service {n}", 1000000 + n, "Package", 1, 1000, "HCM", "Policy"); item.Submit(); item.Review(true, null); f.Db.ServiceItems.Add(item); f.Items.Add(item);
                var template = ComboTemplate.Create($"Preset {n}", "Popular", "Modern", "Synthetic preset", null, n); var slot = ComboTemplateItem.Create(template.Id, f.Category.Id, listing.Id, false); slot.ConfigureItems(item.Id, "[]"); template.Items.Add(slot); template.SetScope(f.Vendor.Id, "HCM", 0, true); f.Db.ComboTemplates.Add(template); f.Templates.Add(template);
                var order = ComboOrder.Create(f.Customer.Id, null, date, 100, "Synthetic fixture", [(listing.Id, f.Vendor.Id, f.Category.Id, item.Name, item.BasePrice), (listing.Id, f.Vendor.Id, photo.Id, "Photo", 1000000)]); order.SetPricing(0, "HCM", true); f.Db.ComboOrders.Add(order); f.Orders.Add(order);
                foreach (var line in order.Items) { var lead = Lead.Create(f.Customer.Id, f.Vendor.Id, listing.Id, f.Customer.PhoneNumber, date, 100, 1000000, "Synthetic test consultation"); lead.AttachSource("Combo", order.Id); line.AttachLead(lead.Id); f.Db.Leads.Add(lead); }
            }
            await f.Db.SaveChangesAsync(); return f;
        }
        public async ValueTask DisposeAsync() { if (databaseName != null) { Assert.StartsWith("WeddingPlanTest_Pagination_", databaseName); await Db.Database.EnsureDeletedAsync(); } await Db.DisposeAsync(); if (connection != null) await connection.DisposeAsync(); }
    }
}
