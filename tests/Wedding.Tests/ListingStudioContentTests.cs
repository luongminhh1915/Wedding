using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Wedding.Application.Common.Interfaces;
using Wedding.Application.Features.Listings;
using Wedding.Application.Features.Listings.Commands.CreateListing;
using Wedding.Application.Features.Listings.Commands.UpdateListing;
using Wedding.Domain.Entities;
using Wedding.Domain.Enums;
using Wedding.Domain.Exceptions;

namespace Wedding.Tests;

public sealed class ListingStudioContentTests
{
    private const string Image = "/uploads/listings/0123456789abcdef0123456789abcdef.webp";
    private const string External = "https://studio.example.invalid/wedding.jpg";
    private const string AlbumId = "a67787c2-9ce6-41b4-85a1-e681d22efb42";

    private static ServiceDetailsDto Content() => new(ShortDescription: "Existing short description",
        Collections: [new(AlbumId.ToUpperInvariant(), "  Ngày vui ngoài trời  ", ["  " + Image + "  ", External])],
        LatestUpdate: new("  Bộ ảnh mới trong mùa cưới  ", Image));

    [Fact]
    public void StudioContentRoundTripsInExistingJsonWithoutChangingOtherServiceDetails()
    {
        var json = ListingServiceDetails.NormalizeAndSerialize(Content());
        var result = ListingServiceDetails.Read(json);
        Assert.NotNull(result);
        Assert.Equal("Existing short description", result.ShortDescription);
        var album = Assert.Single(result.Collections!);
        Assert.Equal(AlbumId, album.Id); Assert.Equal("Ngày vui ngoài trời", album.Name);
        Assert.Equal(new[] { Image, External }, album.Images);
        Assert.Equal("Bộ ảnh mới trong mùa cưới", result.LatestUpdate?.Content);
        Assert.Equal(Image, result.LatestUpdate?.ImageUrl);
        Assert.Equal(json, ListingServiceDetails.NormalizeAndSerialize(result));
    }

    [Fact]
    public void OldJsonHasNoFabricatedAlbumsOrLatestUpdate()
    {
        var old = ListingServiceDetails.Read("{\"shortDescription\":\"Preserved legacy introduction\"}");
        Assert.NotNull(old); Assert.Equal("Preserved legacy introduction", old.ShortDescription);
        Assert.Empty(old.Collections!); Assert.Null(old.LatestUpdate);
        var cleared = ListingServiceDetails.Read(ListingServiceDetails.NormalizeAndSerialize(new()));
        Assert.NotNull(cleared); Assert.Empty(cleared.Collections!); Assert.Null(cleared.LatestUpdate);
        Assert.Null(ListingServiceDetails.Read(ListingServiceDetails.NormalizeAndSerialize(new(LatestUpdate: new("  ", "  "))))?.LatestUpdate);
    }

    [Theory]
    [InlineData("Text only", "")]
    [InlineData("", Image)]
    [InlineData("Text and picture", External)]
    public void LatestUpdateSupportsTextOnlyImageOnlyOrBoth(string text, string image)
    {
        var read = ListingServiceDetails.Read(ListingServiceDetails.NormalizeAndSerialize(new(LatestUpdate: new(text, image))));
        Assert.Equal(text, read?.LatestUpdate?.Content); Assert.Equal(image, read?.LatestUpdate?.ImageUrl);
    }

    [Theory]
    [InlineData("blob:https://studio.example.invalid/temporary")]
    [InlineData("data:image/png;base64,AA")]
    [InlineData("javascript:alert(1)")]
    [InlineData("http://studio.example.invalid/wedding.jpg")]
    [InlineData("https://user:secret@studio.example.invalid/image.jpg")]
    [InlineData("/uploads/articles/0123456789abcdef0123456789abcdef.webp")]
    [InlineData("/uploads/listings/../private/password.json")]
    [InlineData("/uploads/listings/0123456789abcdef0123456789abcdef.svg")]
    [InlineData("https://studio.example.invalid/%252e%252e/private")]
    [InlineData("https://studio.example.invalid/a b.jpg")]
    public void UnsavedOrUnsafeImageReferencesCannotBePersisted(string image)
    {
        var details = Content() with { Collections = [new(AlbumId, "Album", [image])] };
        Assert.Throws<DomainException>(() => ListingServiceDetails.NormalizeAndSerialize(details));
        Assert.Throws<DomainException>(() => ListingServiceDetails.NormalizeAndSerialize(new(LatestUpdate: new("", image))));
        Assert.Null(ListingServiceDetails.Read(JsonSerializer.Serialize(details)));
    }

    [Fact]
    public void AlbumAndUpdateBoundsAreEnforcedBeforeAnyWrite()
    {
        Assert.Throws<DomainException>(() => ListingServiceDetails.NormalizeAndSerialize(new(Collections: [new(AlbumId, "", [Image])])));
        Assert.Throws<DomainException>(() => ListingServiceDetails.NormalizeAndSerialize(new(Collections: [new(AlbumId, new('x', 101), [Image])])));
        Assert.Throws<DomainException>(() => ListingServiceDetails.NormalizeAndSerialize(new(Collections: [new("not-a-guid", "Album", [Image])])));
        Assert.Throws<DomainException>(() => ListingServiceDetails.NormalizeAndSerialize(new(Collections: [new(Guid.Empty.ToString(), "Album", [Image])])));
        Assert.Throws<DomainException>(() => ListingServiceDetails.NormalizeAndSerialize(new(Collections: [new(AlbumId, "Album", [])])));
        Assert.Throws<DomainException>(() => ListingServiceDetails.NormalizeAndSerialize(new(Collections: [new(AlbumId, "Album", [Image, "  " + Image + "  "])])));
        Assert.Throws<DomainException>(() => ListingServiceDetails.NormalizeAndSerialize(new(Collections: [new(AlbumId, "One", [Image]), new(AlbumId.ToUpperInvariant(), "Two", [External])])));
        Assert.Throws<DomainException>(() => ListingServiceDetails.NormalizeAndSerialize(new(Collections: Enumerable.Range(0, 11).Select(i => new ServiceCollectionDto(Guid.NewGuid().ToString(), "Album", [Image])).ToList())));
        Assert.Throws<DomainException>(() => ListingServiceDetails.NormalizeAndSerialize(new(Collections: [new(AlbumId, "Album", Enumerable.Range(0, 11).Select(i => $"https://studio.example.invalid/{i}.png").ToList())])));
        Assert.Throws<DomainException>(() => ListingServiceDetails.NormalizeAndSerialize(new(LatestUpdate: new(new('x', 4001), ""))));
        Assert.NotNull(ListingServiceDetails.NormalizeAndSerialize(new(Collections: [new(AlbumId, new('x', 100), [Image])], LatestUpdate: new(new('x', 4000), ""))));
    }

    [SqlServerFact]
    public async Task ActualSqlCreateReloadAndReplaceStudioContentUsesExistingColumnWithoutChangingPricingOrMedia()
    {
        await using var owned = await CommissionSqlTestDatabase.CreateAsync();
        var db = owned.Db;
        var owner = User.Create("Synthetic Studio Owner", "studio-owner@fixture.example.invalid", "0900000941", "synthetic-hash", UserRole.VendorOwner);
        var vendor = Vendor.CreateWithCategoryDefaults(owner.Id, "Synthetic Studio Shop", "synthetic-studio-shop", owner.FullName, owner.PhoneNumber,
            "Hà Nội", BusinessGroupPolicy.ServiceCategoryRegistrationVersion);
        db.Users.Add(owner); db.Vendors.Add(vendor); await db.SaveChangesAsync();
        var category = await db.Categories.SingleAsync(c => c.Slug == "quay-chup-photo-video");
        var actor = new Current(owner.Id);
        var id = await new CreateListingCommandHandler(db, actor).Handle(new(category.Id, "Synthetic Studio Service", 1000, 2000,
            "Synthetic studio description", "Hà Nội", [Image], Content()), default);
        await using (var reload = owned.CreateContext())
        {
            var listing = await reload.Listings.AsNoTracking().SingleAsync(l => l.Id == id);
            var detail = ListingServiceDetails.Read(listing.ServiceDetailsJson);
            Assert.Equal("Ngày vui ngoài trời", Assert.Single(detail!.Collections!).Name);
            Assert.Equal(Image, detail.LatestUpdate?.ImageUrl);
            Assert.Equal(1000, listing.MinPrice); Assert.Equal(2000, listing.MaxPrice);
        }
        await new UpdateListingCommandHandler(db, actor).Handle(new(id, "Synthetic Studio Service", 1000, 2000,
            "Synthetic studio description", "Hà Nội", ServiceDetails: new(Collections: [], LatestUpdate: new("Text-only update", ""))), default);
        await using var final = owned.CreateContext();
        var stored = await final.Listings.AsNoTracking().SingleAsync(l => l.Id == id);
        var updated = ListingServiceDetails.Read(stored.ServiceDetailsJson);
        Assert.Empty(updated!.Collections!); Assert.Equal("Text-only update", updated.LatestUpdate?.Content); Assert.Empty(updated.LatestUpdate!.ImageUrl);
        Assert.Equal(Image, Assert.Single(await final.ListingMedias.Where(m => m.ListingId == id).ToListAsync()).MediaUrl);
        Assert.Equal(0m, (await final.Vendors.SingleAsync(v => v.Id == vendor.Id)).CommissionRate);
        Assert.Empty(await final.Commissions.ToListAsync()); Assert.Empty(await final.BookingContracts.ToListAsync());
    }

    private sealed record Current(Guid? UserId) : ICurrentUserService { public string? Role => "VendorOwner"; public string? Email => null; }
}
