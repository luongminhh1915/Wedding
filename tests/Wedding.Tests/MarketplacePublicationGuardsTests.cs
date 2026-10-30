using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Wedding.Application.Common.Interfaces;
using Wedding.Application.Features.Combos;
using Wedding.Application.Features.Reviews.Commands.SubmitReview;
using Wedding.Application.Features.Reviews.DTOs;
using Wedding.Domain.Entities;
using Wedding.Domain.Enums;
using Wedding.Domain.Exceptions;
using Wedding.Infrastructure.Persistence;

namespace Wedding.Tests;

public class MarketplacePublicationGuardsTests
{
    private const string Content = "Dịch vụ tư vấn chu đáo, thông tin rõ ràng và hỗ trợ rất tận tình.";

    [Theory]
    [InlineData(ListingStatus.Draft)]
    [InlineData(ListingStatus.PendingApproval)]
    [InlineData(ListingStatus.Rejected)]
    [InlineData(ListingStatus.Hidden)]
    public async Task CustomerCannotReviewOrDiscoverUnpublishedListingThroughKnownId(ListingStatus status)
    {
        await using var f = await Fixture.Create();
        f.Listing.SetStatus(status);
        await f.Db.SaveChangesAsync();
        f.Db.ChangeTracker.Clear();

        var handler = new SubmitReviewCommandHandler(f.Db, new Current(f.Customer.Id));
        var hidden = await Assert.ThrowsAsync<NotFoundException>(() => handler.Handle(new(null, f.Listing.Id, 5, Content), default));
        var missing = await Assert.ThrowsAsync<NotFoundException>(() => handler.Handle(new(null, Guid.NewGuid(), 5, Content), default));

        Assert.Equal(missing.Message, hidden.Message);
        Assert.DoesNotContain(f.Listing.Title, hidden.Message);
        Assert.Equal(0, await f.Db.Reviews.CountAsync());
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task CustomerCannotReviewListingOfInactiveVendorOrDisabledOwner(bool disableVendor)
    {
        await using var f = await Fixture.Create();
        if (disableVendor) f.Db.Entry(f.Vendor).Property(v => v.IsActive).CurrentValue = false;
        else f.Owner.SetStatus(false);
        await f.Db.SaveChangesAsync();
        f.Db.ChangeTracker.Clear();

        var hidden = await Assert.ThrowsAsync<NotFoundException>(() => new SubmitReviewCommandHandler(f.Db, new Current(f.Customer.Id))
            .Handle(new(null, f.Listing.Id, 5, Content), default));

        Assert.DoesNotContain(f.Listing.Title, hidden.Message);
        Assert.Equal(0, await f.Db.Reviews.CountAsync());
    }

    [Fact]
    public async Task PublicListingCannotReceiveNewConsultationReviewWithoutACompletedContract()
    {
        await using var f = await Fixture.Create();
        f.Db.ChangeTracker.Clear();
        await Assert.ThrowsAsync<DomainException>(() => new SubmitReviewCommandHandler(f.Db, new Current(f.Customer.Id))
            .Handle(new(null, f.Listing.Id, 5, Content), default));
        Assert.Equal(0, await f.Db.Reviews.CountAsync());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ExistingCartCannotPriceOrSubmitAfterVendorOwnerIsDisabled(bool saveAsDraft)
    {
        await using var f = await Fixture.Create();
        var priceRequest = f.PriceRequest();
        var previousPrice = await ComboPricing.CalculateAsync(f.Db, priceRequest, f.Customer.Id, default);
        f.Owner.SetStatus(false);
        await f.Db.SaveChangesAsync();
        f.Db.ChangeTracker.Clear();

        await Assert.ThrowsAsync<DomainException>(() => ComboPricing.CalculateAsync(f.Db, priceRequest, f.Customer.Id, default));
        var submit = new SubmitComboOrderCommand(null, new(), priceRequest.WeddingDate, priceRequest.GuestCount,
            f.Customer.PhoneNumber, null, priceRequest.Region, priceRequest.Selections, previousPrice.Total, saveAsDraft);
        await Assert.ThrowsAsync<DomainException>(() => new SubmitComboOrderCommandHandler(f.Db, new Current(f.Customer.Id), new Notifications())
            .Handle(submit, default));

        Assert.Equal(0, await f.Db.ComboOrders.CountAsync());
        Assert.Equal(0, await f.Db.ComboOrderItems.CountAsync());
        Assert.Equal(0, await f.Db.Leads.CountAsync());
        Assert.Equal(0, await f.Db.BudgetItems.CountAsync());
    }

    [Fact]
    public async Task ActiveOwnerCanStillPriceAndSubmitTheSameEligibleService()
    {
        await using var f = await Fixture.Create();
        f.Db.ChangeTracker.Clear();
        var request = f.PriceRequest();
        var price = await ComboPricing.CalculateAsync(f.Db, request, f.Customer.Id, default);
        var submitted = await new SubmitComboOrderCommandHandler(f.Db, new Current(f.Customer.Id), new Notifications())
            .Handle(new(null, new(), request.WeddingDate, request.GuestCount, f.Customer.PhoneNumber, null,
                request.Region, request.Selections, price.Total), default);

        Assert.Equal(2000000m, price.Total);
        Assert.Equal(price.Total, submitted.Total);
        Assert.Equal("Submitted", submitted.Status);
        Assert.Equal(1, await f.Db.ComboOrders.CountAsync());
        Assert.Equal(1, await f.Db.Leads.CountAsync());
    }

    private sealed record Current(Guid Id) : ICurrentUserService
    {
        public Guid? UserId => Id;
        public string? Email => null;
        public string? Role => "Customer";
    }

    private sealed class Notifications : INotificationSender
    {
        public Task SendAsync(string channel, string to, string message, CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class Fixture(SqliteConnection connection, ApplicationDbContext db) : IAsyncDisposable
    {
        public ApplicationDbContext Db { get; } = db;
        public User Customer { get; private set; } = null!;
        public User Owner { get; private set; } = null!;
        public Vendor Vendor { get; private set; } = null!;
        public Listing Listing { get; private set; } = null!;
        public ServiceItem Item { get; private set; } = null!;
        public ComboPriceCommand PriceRequest() => new(null, "HCM", DateTime.UtcNow.AddDays(20), 100, new() { new(Item.Id, null, null) });

        public static async Task<Fixture> Create()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options);
            await db.Database.EnsureCreatedAsync();
            var f = new Fixture(connection, db);
            f.Owner = User.Create("Owner", "owner@publication.test", "0900000001", "hash", UserRole.VendorOwner);
            f.Customer = User.Create("Customer", "customer@publication.test", "0900000002", "hash", UserRole.Customer);
            db.Users.AddRange(f.Owner, f.Customer);
            f.Vendor = Vendor.Create(f.Owner.Id, "Studio", "publication-studio", "Owner", "0900000001", "HCM", .08m);
            f.Vendor.SetVerified(true);
            f.Vendor.ConfigureComboParticipation(true, .08m);
            db.Vendors.Add(f.Vendor);
            var category = Category.Create("Photo", "publication-photo", null, null, .08m);
            db.Categories.Add(category);
            f.Listing = Listing.Create(f.Vendor.Id, category.Id, "Private identifying service title", "publication-listing", 2000000, 2000000, "Description", "HCM");
            f.Listing.Approve();
            db.Listings.Add(f.Listing);
            f.Item = ServiceItem.Create(f.Vendor.Id, f.Listing.Id, category.Id, "Photo", 2000000, "Package", 1, 1000, "HCM", "Refund policy");
            f.Item.Submit();
            f.Item.Review(true, null);
            db.ServiceItems.Add(f.Item);
            await db.SaveChangesAsync();
            return f;
        }

        public async ValueTask DisposeAsync()
        {
            await Db.DisposeAsync();
            await connection.DisposeAsync();
        }
    }
}
