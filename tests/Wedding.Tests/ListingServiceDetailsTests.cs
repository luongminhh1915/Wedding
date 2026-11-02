using System.Reflection;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Wedding.Application.Common.Interfaces;
using Wedding.Application.Features.Listings;
using Wedding.Application.Features.Listings.Commands.CreateListing;
using Wedding.Application.Features.Listings.Commands.UpdateListing;
using Wedding.Application.Features.Listings.Queries.GetListingDetail;
using Wedding.Domain.Entities;
using Wedding.Domain.Enums;
using Wedding.Domain.Exceptions;
using Wedding.Infrastructure.Persistence;

namespace Wedding.Tests;

public class ListingServiceDetailsTests
{
    private const string LocalImage = "/uploads/listings/0123456789abcdef0123456789abcdef.png";
    private const string Description = "Synthetic full description retained independently from structured service details.";
    private const string Cancellation = "Existing cancellation agreement must not be replaced by service-detail metadata.";

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task CreateReloadDetailAndUpdatePersistOrdinaryAndFullServiceMetadata(bool fullService)
    {
        await using var f = await Fixture.Create();
        var details = Details(fullService);
        var command = f.CreateCommand(details) with { CategoryId = fullService ? f.Planning.Id : f.Photo.Id };
        var id = await f.CreateHandler().Handle(command, default);
        await using (var reload = f.OpenContext())
        {
            var stored = await reload.Listings.AsNoTracking().SingleAsync(item => item.Id == id);
            Assert.Equal(ListingStatus.Draft, stored.Status);
            Assert.Equal(ListingServiceDetails.NormalizeAndSerialize(details), stored.ServiceDetailsJson);
            var detail = await new GetListingDetailQueryHandler(reload, new Current(f.Owner.Id)).Handle(new(id), default);
            Assert.NotNull(detail.ServiceDetails);
            Assert.Equal(stored.ServiceDetailsJson, ListingServiceDetails.NormalizeAndSerialize(detail.ServiceDetails));
            Assert.Equal(Description, detail.Description); Assert.Equal(stored.CancellationPolicy, detail.CancellationPolicy);
            Assert.Equal(command.ImageUrls, detail.Media.Select(media => media.MediaUrl));
            Assert.Equal(fullService, detail.ServiceDetails.FullService != null);
        }
        var replacement = Details(fullService) with { ShortDescription = "Updated typed introduction", Notes = "Updated operational notes" };
        await f.UpdateHandler().Handle(new(id, "Updated created service", 1000, 2000, Description, "Hà Nội", ServiceDetails: replacement), default);
        await using var final = f.OpenContext();
        var updated = await new GetListingDetailQueryHandler(final, new Current(f.Owner.Id)).Handle(new(id), default);
        Assert.Equal("Updated created service", updated.Title);
        Assert.Equal(ListingServiceDetails.NormalizeAndSerialize(replacement), ListingServiceDetails.NormalizeAndSerialize(updated.ServiceDetails));
        Assert.Equal(command.ImageUrls, updated.Media.Select(media => media.MediaUrl));
        Assert.Empty(await final.Commissions.ToListAsync());
    }

    [Fact]
    public async Task LegacyNullDetailsRemainReadableAndOmittedUpdatePreservesRichDetailsPolicyAndMedia()
    {
        await using var f = await Fixture.Create();
        var legacy = await f.Detail();
        Assert.Null(legacy.ServiceDetails); Assert.Null(f.Listing.ServiceDetailsJson);
        Assert.Null(ListingServiceDetails.Read(null)); Assert.Null(ListingServiceDetails.NormalizeAndSerialize(null));
        var details = Details(true);
        f.Listing.SetServiceDetailsJson(ListingServiceDetails.NormalizeAndSerialize(details));
        await f.Db.SaveChangesAsync();
        var beforeMedia = await f.Db.ListingMedias.AsNoTracking().Where(item => item.ListingId == f.Listing.Id).OrderBy(item => item.DisplayOrder).ToListAsync();
        var beforeJson = f.Listing.ServiceDetailsJson;
        await f.UpdateHandler().Handle(f.UpdateCommand() with { Title = "Edited title only", MinPrice = 1200 }, default);
        await using var reload = f.OpenContext();
        var stored = await reload.Listings.AsNoTracking().SingleAsync(item => item.Id == f.Listing.Id);
        Assert.Equal("Edited title only", stored.Title); Assert.Equal(1200, stored.MinPrice);
        Assert.Equal(beforeJson, stored.ServiceDetailsJson); Assert.Equal(Description, stored.Description);
        Assert.Equal(Cancellation, stored.CancellationPolicy);
        var media = await reload.ListingMedias.AsNoTracking().Where(item => item.ListingId == f.Listing.Id).OrderBy(item => item.DisplayOrder).ToListAsync();
        Assert.Equal(beforeMedia.Select(item => item.Id), media.Select(item => item.Id));
        Assert.Equal(beforeMedia.Select(item => item.MediaUrl), media.Select(item => item.MediaUrl));
        Assert.Equal(beforeMedia.Select(item => item.IsFeatured), media.Select(item => item.IsFeatured));
    }

    [Fact]
    public async Task ExplicitReplacementClearsOmittedMetadataWithoutRewritingContractOrFinancialHistory()
    {
        await using var f = await Fixture.Create();
        f.Listing.SetServiceDetailsJson(ListingServiceDetails.NormalizeAndSerialize(Details(true)));
        var customer = User.Create("Synthetic contracted customer", "customer@details.example.invalid", "0901234569", "customer-hash", UserRole.Customer);
        f.Db.Users.Add(customer);
        var lead = Lead.Create(customer.Id, f.Vendor.Id, f.Listing.Id, "0901234569", DateTime.UtcNow.AddDays(100), 100, 5000000, null);
        f.Db.Leads.Add(lead);
        var contract = BookingContract.Create(lead.Id, null, f.Vendor.Id, customer.Id, 5000000, 1000000, null, lead.WeddingDate);
        contract.ConfirmByCustomer(); contract.SnapshotCommissionRate(.08m); f.Db.BookingContracts.Add(contract);
        var commission = Commission.Create(contract.Id, f.Vendor.Id, CommissionPeriod.Period1_Deposit, .08m, 200000, DateTime.UtcNow.AddDays(10));
        commission.MarkPaid("SYNTHETIC-UNCHANGED-RECEIPT"); f.Db.Commissions.Add(commission);
        await f.Db.SaveChangesAsync();
        var beforeFinance = await f.FinancialSnapshot();
        var beforeMedia = await f.Db.ListingMedias.AsNoTracking().OrderBy(item => item.DisplayOrder).Select(item => item.Id).ToListAsync();
        var replacement = new ServiceDetailsDto(ShortDescription: "Replacement summary", IncludedItems: "One new inclusion");
        await f.UpdateHandler().Handle(f.UpdateCommand() with { ServiceDetails = replacement }, default);
        var detail = await f.Detail(); Assert.NotNull(detail.ServiceDetails);
        Assert.Equal("Replacement summary", detail.ServiceDetails.ShortDescription);
        Assert.Equal("One new inclusion", detail.ServiceDetails.IncludedItems);
        Assert.Null(detail.ServiceDetails.FullService); Assert.Empty(detail.ServiceDetails.Highlights);
        Assert.NotNull(detail.ServiceDetails.PortfolioUrls); Assert.Empty(detail.ServiceDetails.PortfolioUrls);
        Assert.Equal(Cancellation, detail.CancellationPolicy);
        Assert.Equal(beforeFinance, await f.FinancialSnapshot());
        Assert.Equal(beforeMedia, await f.Db.ListingMedias.AsNoTracking().OrderBy(item => item.DisplayOrder).Select(item => item.Id).ToListAsync());
    }

    [Fact]
    public void MetadataSerializationTrimsInputAndNormalizesNullPortfolioToAnEmptyList()
    {
        var input = new ServiceDetailsDto(ShortDescription: "  Summary  ", PriceUnit: "  package  ", IncludedItems: "  Included  ", PortfolioUrls: null);
        var serialized = ListingServiceDetails.NormalizeAndSerialize(input);
        var read = ListingServiceDetails.Read(serialized); Assert.NotNull(read);
        Assert.Equal("Summary", read.ShortDescription); Assert.Equal("package", read.PriceUnit); Assert.Equal("Included", read.IncludedItems);
        Assert.NotNull(read.PortfolioUrls); Assert.Empty(read.PortfolioUrls);
        Assert.Equal(serialized, ListingServiceDetails.NormalizeAndSerialize(read));
    }

    [Fact]
    public void InvalidStoredMetadataFallsBackWithoutBreakingLegacyDetailReads()
    {
        Assert.Null(ListingServiceDetails.Read("{malformed-json"));
        Assert.Null(ListingServiceDetails.Read(new string('a', 60001)));
        Assert.Null(ListingServiceDetails.Read(JsonSerializer.Serialize(new ServiceDetailsDto(VideoUrl: "javascript:alert(1)"))));
        Assert.Equal("Case-insensitive stored metadata", ListingServiceDetails.Read("{\"shortDescription\":\"Case-insensitive stored metadata\"}")?.ShortDescription);
    }

    [Theory]
    [InlineData("ShortDescription", 500)] [InlineData("PriceUnit", 80)]
    [InlineData("IncludedItems", 4000)] [InlineData("ExcludedItems", 4000)] [InlineData("Duration", 4000)]
    [InlineData("Process", 4000)] [InlineData("ServiceOptions", 4000)] [InlineData("AdditionalFees", 4000)]
    [InlineData("Terms", 4000)] [InlineData("ReschedulePolicy", 4000)] [InlineData("Highlights", 4000)]
    [InlineData("CustomerRequirements", 4000)] [InlineData("Notes", 4000)]
    [InlineData("FullService.Scope", 4000)] [InlineData("FullService.Capacity", 4000)]
    [InlineData("FullService.Stages", 4000)] [InlineData("FullService.Outsourcing", 4000)]
    [InlineData("FullService.ManagementFeeDescription", 4000)] [InlineData("FullService.PackagePriceDescription", 4000)]
    [InlineData("FullService.ExtraCostTerms", 4000)] [InlineData("FullService.PaymentTerms", 4000)]
    public void EveryMetadataTextFieldAcceptsItsBoundaryAndRejectsAnExtraCharacter(string field, int limit)
    {
        Assert.NotNull(ListingServiceDetails.NormalizeAndSerialize(WithText(field, new string('a', limit))));
        Assert.Throws<DomainException>(() => ListingServiceDetails.NormalizeAndSerialize(WithText(field, new string('a', limit + 1))));
    }

    [Theory]
    [InlineData("javascript:alert(1)")] [InlineData("data:text/plain;base64,AA")]
    [InlineData("blob:https://example.invalid/temp")] [InlineData("//example.invalid/portfolio")]
    [InlineData("/uploads/listings/0123456789abcdef0123456789abcdef.png")]
    [InlineData("ftp://example.invalid/file")] [InlineData("https://user:password@example.invalid/file")]
    [InlineData("https://example.invalid/a/../private")] [InlineData("https://example.invalid/a/%2e%2e/private")]
    [InlineData("https://example.invalid/a/%252e%252e/private")]
    [InlineData("https://example.invalid/port\nfolio")] [InlineData("https://example.invalid/a\\private")]
    public void UnsafePortfolioAndVideoReferencesAreRejected(string url)
    {
        Assert.Throws<DomainException>(() => ListingServiceDetails.NormalizeAndSerialize(new ServiceDetailsDto(PortfolioUrls: [url])));
        Assert.Throws<DomainException>(() => ListingServiceDetails.NormalizeAndSerialize(new ServiceDetailsDto(VideoUrl: url)));
    }

    [Fact]
    public void EncodedOrdinarySpacesInAbsolutePortfolioAndVideoUrlsRemainVisibleAfterReload()
    {
        const string url = "https://portfolio.example.invalid/my%20portfolio.pdf";
        var stored = ListingServiceDetails.NormalizeAndSerialize(new ServiceDetailsDto(PortfolioUrls: [url], VideoUrl: url));
        var details = ListingServiceDetails.Read(stored);
        Assert.NotNull(details);
        Assert.Equal(url, Assert.Single(details.PortfolioUrls!));
        Assert.Equal(url, details.VideoUrl);
    }

    [Fact]
    public void PortfolioCountUrlLengthAndOverallMetadataLimitsAreEnforced()
    {
        var validUrls = Enumerable.Range(0, 10).Select(index => $"https://portfolio.example.invalid/{index}.jpg").ToList();
        var valid = new ServiceDetailsDto(PortfolioUrls: validUrls, VideoUrl: "https://www.youtube.com/watch?v=synthetic12");
        Assert.NotNull(ListingServiceDetails.NormalizeAndSerialize(valid));
        Assert.Throws<DomainException>(() => ListingServiceDetails.NormalizeAndSerialize(valid with { PortfolioUrls = [.. validUrls, "https://portfolio.example.invalid/extra.jpg"] }));
        const string prefix = "https://portfolio.example.invalid/";
        var maxUrl = prefix + new string('a', 1000 - prefix.Length);
        Assert.NotNull(ListingServiceDetails.NormalizeAndSerialize(new ServiceDetailsDto(PortfolioUrls: [maxUrl], VideoUrl: maxUrl)));
        Assert.Throws<DomainException>(() => ListingServiceDetails.NormalizeAndSerialize(new ServiceDetailsDto(PortfolioUrls: [maxUrl + "a"])));
        Assert.Throws<DomainException>(() => ListingServiceDetails.NormalizeAndSerialize(new ServiceDetailsDto(VideoUrl: maxUrl + "a")));
        Assert.Throws<DomainException>(() => ListingServiceDetails.NormalizeAndSerialize(new ServiceDetailsDto(PortfolioUrls: [null!])));

        var oversized = new ServiceDetailsDto(ShortDescription: new string('a', 500), PriceUnit: new string('a', 80),
            IncludedItems: new string('a', 4000), ExcludedItems: new string('a', 4000), Duration: new string('a', 4000),
            Process: new string('a', 4000), ServiceOptions: new string('a', 4000), AdditionalFees: new string('a', 4000),
            Terms: new string('a', 4000), ReschedulePolicy: new string('a', 4000), Highlights: new string('a', 4000),
            CustomerRequirements: new string('a', 4000), Notes: new string('a', 4000), FullService: new(
                new string('a', 4000), new string('a', 4000), new string('a', 4000), new string('a', 4000),
                new string('a', 4000), new string('a', 4000), new string('a', 4000), new string('a', 4000)));
        Assert.Throws<DomainException>(() => ListingServiceDetails.NormalizeAndSerialize(oversized));
        var listing = Listing.Create(Guid.NewGuid(), Guid.NewGuid(), "Synthetic", "synthetic", 1, 2, Description, "Hà Nội");
        Assert.Throws<DomainException>(() => listing.SetServiceDetailsJson(new string('a', 60001)));
    }

    [Fact]
    public async Task InvalidMetadataCannotPartiallyChangeTextPricesAlbumPolicyOrExistingDetails()
    {
        await using var f = await Fixture.Create();
        f.Listing.SetServiceDetailsJson(ListingServiceDetails.NormalizeAndSerialize(Details(true))); await f.Db.SaveChangesAsync();
        var before = await f.ListingSnapshot();
        var invalid = new[]
        {
            new ServiceDetailsDto(ShortDescription: new string('a', 501)),
            new ServiceDetailsDto(PriceUnit: new string('a', 81)),
            new ServiceDetailsDto(PortfolioUrls: ["javascript:alert(1)"]),
            new ServiceDetailsDto(VideoUrl: "https://name:password@example.invalid/video"),
            new ServiceDetailsDto(FullService: new(Scope: new string('a', 4001)))
        };
        foreach (var details in invalid)
        {
            await Assert.ThrowsAsync<DomainException>(() => f.UpdateHandler().Handle(f.UpdateCommand() with
            {
                Title = "Must not persist", Description = "Must not replace description", MinPrice = 1200, MaxPrice = 3000,
                Location = "Must not replace location", ImageUrls = ["https://cdn.example.invalid/replacement.jpg"], ServiceDetails = details
            }, default));
            Assert.Equal(before, await f.ListingSnapshot());
            await Assert.ThrowsAsync<DomainException>(() => f.CreateHandler().Handle(f.CreateCommand(details), default));
            Assert.Single(await f.Db.Listings.AsNoTracking().ToListAsync());
            Assert.Equal(before, await f.ListingSnapshot());
        }
    }

    [Fact]
    public async Task FailureAfterSavingRollsBackCreateAndUpdateTogetherWithMetadataMediaAndAudit()
    {
        await using var f = await Fixture.Create();
        f.Listing.SetServiceDetailsJson(ListingServiceDetails.NormalizeAndSerialize(Details(false))); await f.Db.SaveChangesAsync();
        var before = await f.ListingSnapshot(); var audits = await f.Db.AuditLogs.CountAsync();
        await using (var failing = new FailingAfterSaveContext(f.Options))
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() => new CreateListingCommandHandler(failing, new Current(f.Owner.Id))
                .Handle(f.CreateCommand(Details(true)), default));
        }
        Assert.Single(await f.Db.Listings.AsNoTracking().ToListAsync()); Assert.Equal(before, await f.ListingSnapshot());
        await using (var failing = new FailingAfterSaveContext(f.Options))
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() => new UpdateListingCommandHandler(failing, new Current(f.Owner.Id))
                .Handle(f.UpdateCommand() with { Title = "Must roll back", ServiceDetails = Details(true), ImageUrls = ["https://cdn.example.invalid/replacement.jpg"] }, default));
        }
        Assert.Equal(before, await f.ListingSnapshot()); Assert.Equal(audits, await f.Db.AuditLogs.CountAsync());
    }

    [Fact]
    public async Task OnlyOwnerCanReplaceDetailsWhileAnotherVendorCreatesOnlyItsOwnDraft()
    {
        await using var f = await Fixture.Create();
        var before = await f.ListingSnapshot();
        await Assert.ThrowsAsync<NotFoundException>(() => f.UpdateHandler(new Current(f.OtherOwner.Id))
            .Handle(f.UpdateCommand() with { ServiceDetails = Details(true) }, default));
        await Assert.ThrowsAsync<NotFoundException>(() => new GetListingDetailQueryHandler(f.Db, new Current(f.OtherOwner.Id))
            .Handle(new(f.Listing.Id), default));
        Assert.Equal(before, await f.ListingSnapshot());
        var ownId = await f.CreateHandler(new Current(f.OtherOwner.Id)).Handle(f.CreateCommand(Details(false)), default);
        var own = await f.Db.Listings.AsNoTracking().SingleAsync(item => item.Id == ownId);
        Assert.Equal(f.OtherVendor.Id, own.VendorId); Assert.Equal(ListingStatus.Draft, own.Status);
        Assert.Equal(before, await f.ListingSnapshot());
    }

    [Theory]
    [InlineData("Customer")] [InlineData("Moderator")] [InlineData("Finance")]
    [InlineData("SuperAdmin")] [InlineData("CustomerCare")] [InlineData(null)]
    public async Task DirectHandlersDoNotTrustAnOwningUserIdWithTheWrongRole(string? role)
    {
        await using var f = await Fixture.Create();
        var before = await f.ListingSnapshot(); var actor = new Current(f.Owner.Id, role);
        await Assert.ThrowsAsync<DomainException>(() => f.CreateHandler(actor).Handle(f.CreateCommand(Details(false)), default));
        await Assert.ThrowsAsync<DomainException>(() => f.UpdateHandler(actor).Handle(f.UpdateCommand() with { ServiceDetails = Details(true) }, default));
        Assert.Equal(before, await f.ListingSnapshot()); Assert.Single(await f.Db.Listings.AsNoTracking().ToListAsync());
    }

    [Theory]
    [InlineData("missing-actor")] [InlineData("disabled-owner")] [InlineData("disabled-shop")] [InlineData("changed-db-role")]
    public async Task MissingOrInactiveActorsCannotCreateOrReplaceMetadata(string condition)
    {
        await using var f = await Fixture.Create();
        if (condition == "disabled-owner") f.Owner.SetStatus(false);
        if (condition == "disabled-shop") f.Db.Entry(f.Vendor).Property(item => item.IsActive).CurrentValue = false;
        if (condition == "changed-db-role") f.Db.Entry(f.Owner).Property(item => item.Role).CurrentValue = UserRole.Customer;
        await f.Db.SaveChangesAsync();
        var actor = new Current(condition == "missing-actor" ? null : f.Owner.Id);
        var before = await f.ListingSnapshot();
        await Assert.ThrowsAsync<DomainException>(() => f.CreateHandler(actor).Handle(f.CreateCommand(Details(false)), default));
        await Assert.ThrowsAsync<DomainException>(() => f.UpdateHandler(actor).Handle(f.UpdateCommand() with { ServiceDetails = Details(true) }, default));
        Assert.Equal(before, await f.ListingSnapshot()); Assert.Single(await f.Db.Listings.AsNoTracking().ToListAsync());
    }

    [SqlServerFact]
    public async Task SqlUpgradeKeepsLegacyListingMediaCancellationUsersVendorAndConfirmedCommissionSnapshot()
    {
        var database = $"WeddingPlanTest_ListingDetails_{Guid.NewGuid():N}";
        var source = Environment.GetEnvironmentVariable("WEDDING_TEST_SQLSERVER");
        var builder = new SqlConnectionStringBuilder(string.IsNullOrWhiteSpace(source) || source == "1"
            ? "Server=localhost;Integrated Security=true;TrustServerCertificate=true;Connection Timeout=5" : source)
            { InitialCatalog = database };
        await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlServer(builder.ConnectionString).Options);
        try
        {
            await db.GetService<IMigrator>().MigrateAsync("20261018171507_VendorBusinessGroups");
            var ownerId = Guid.NewGuid(); var customerId = Guid.NewGuid(); var vendorId = Guid.NewGuid();
            var listingId = Guid.NewGuid(); var mediaId = Guid.NewGuid(); var leadId = Guid.NewGuid();
            var contractId = Guid.NewGuid(); var contractVersion = Guid.NewGuid();
            var categoryId = Guid.Parse("11111111-1111-1111-1111-111111111103");
            var createdAt = DateTime.UtcNow.AddDays(-10); var confirmedAt = createdAt.AddDays(1); var weddingDate = DateTime.UtcNow.AddDays(100);
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO Users (Id,FullName,Email,PhoneNumber,PasswordHash,SecurityVersion,Role,IsActive,CreatedAt)
                VALUES ({ownerId},{"Synthetic legacy owner"},{"legacy-owner@details.example.invalid"},{"0901234567"},{"unchanged-owner-hash"},{3},{"VendorOwner"},{true},{createdAt});
                INSERT INTO Users (Id,FullName,Email,PhoneNumber,PasswordHash,SecurityVersion,Role,IsActive,CreatedAt)
                VALUES ({customerId},{"Synthetic legacy customer"},{"legacy-customer@details.example.invalid"},{"0901234569"},{"unchanged-customer-hash"},{2},{"Customer"},{true},{createdAt});
                INSERT INTO Vendors (Id,UserId,BrandName,Slug,ContactPerson,Hotline,City,CommissionRate,RatingAvg,TotalReviews,IsActive,IsVerified,IsTopRated,AllowInCombos,MaxComboDiscount,CreatedAt)
                VALUES ({vendorId},{ownerId},{"Synthetic legacy shop"},{"synthetic-legacy-details-shop"},{"Legacy owner"},{"0901234567"},{"Bình Dương"},{.10m},{0m},{0},{true},{false},{false},{false},{0m},{createdAt});
                INSERT INTO Listings (Id,VendorId,CategoryId,Title,Slug,MinPrice,MaxPrice,Description,Location,Status,ViewCount,FavoriteCount,CancellationPolicy,CreatedAt)
                VALUES ({listingId},{vendorId},{categoryId},{"Original legacy service"},{"original-legacy-details-service"},{1000m},{2000m},{Description},{"Hà Nội"},{"Draft"},{17},{5},{Cancellation},{createdAt});
                INSERT INTO ListingMedias (Id,ListingId,MediaUrl,MediaType,DisplayOrder,IsFeatured,CreatedAt)
                VALUES ({mediaId},{listingId},{LocalImage},{"image"},{0},{true},{createdAt});
                INSERT INTO Leads (Id,CustomerId,VendorId,ListingId,RawPhoneNumber,MaskedPhoneNumber,IsPhoneUnlocked,Status,SlaDeadline,CreatedAt,Source)
                VALUES ({leadId},{customerId},{vendorId},{listingId},{"0901234569"},{"0901***569"},{true},{"Contracted"},{createdAt.AddHours(2)},{createdAt},{"Listing"});
                INSERT INTO BookingContracts (Id,ContractCode,LeadId,VendorId,CustomerId,ContractValue,DepositAmount,WeddingDate,Status,VerificationDeadline,ConfirmedAt,CommissionRateSnapshot,CancellationPolicySnapshot,RevisionCount,Version,AppointmentDepositCredit,CreatedAt)
                VALUES ({contractId},{"LEGACY-LISTING-DETAILS-CONTRACT"},{leadId},{vendorId},{customerId},{100000000m},{30000000m},{weddingDate},{"Confirmed"},{createdAt.AddHours(72)},{confirmedAt},{.10m},{Cancellation},{3},{contractVersion},{1000000m},{createdAt});
                """);
            await db.Database.MigrateAsync(); await db.Database.MigrateAsync();
            Assert.False(db.Database.HasPendingModelChanges()); Assert.Empty(await db.Database.GetPendingMigrationsAsync());
            var listing = await db.Listings.AsNoTracking().SingleAsync(item => item.Id == listingId);
            Assert.Null(listing.ServiceDetailsJson); Assert.Equal(Description, listing.Description);
            Assert.Equal(Cancellation, listing.CancellationPolicy); Assert.Equal(17, listing.ViewCount); Assert.Equal(5, listing.FavoriteCount);
            Assert.Equal(createdAt, listing.CreatedAt); Assert.Equal(1000m, listing.MinPrice); Assert.Equal(2000m, listing.MaxPrice);
            var media = Assert.Single(await db.ListingMedias.AsNoTracking().ToListAsync());
            Assert.Equal(mediaId, media.Id); Assert.Equal(LocalImage, media.MediaUrl); Assert.True(media.IsFeatured);
            var legacy = await new GetListingDetailQueryHandler(db, new Current(ownerId)).Handle(new(listingId), default);
            Assert.Null(legacy.ServiceDetails); Assert.Equal(Cancellation, legacy.CancellationPolicy);

            var details = Details(true);
            await new UpdateListingCommandHandler(db, new Current(ownerId)).Handle(new(listingId, listing.Title,
                listing.MinPrice, listing.MaxPrice, listing.Description, listing.Location, ServiceDetails: details), default);
            db.ChangeTracker.Clear();
            var reloaded = await new GetListingDetailQueryHandler(db, new Current(ownerId)).Handle(new(listingId), default);
            Assert.Equal(ListingServiceDetails.NormalizeAndSerialize(details), ListingServiceDetails.NormalizeAndSerialize(reloaded.ServiceDetails));
            Assert.Equal(Description, reloaded.Description); Assert.Equal(Cancellation, reloaded.CancellationPolicy);
            Assert.Equal(mediaId, Assert.Single(reloaded.Media).Id); Assert.Equal(LocalImage, reloaded.Media[0].MediaUrl);
            var owner = await db.Users.AsNoTracking().SingleAsync(item => item.Id == ownerId);
            var customer = await db.Users.AsNoTracking().SingleAsync(item => item.Id == customerId);
            Assert.Equal("unchanged-owner-hash", owner.PasswordHash); Assert.Equal(3, owner.SecurityVersion); Assert.Equal(UserRole.VendorOwner, owner.Role);
            Assert.Equal("unchanged-customer-hash", customer.PasswordHash); Assert.Equal(2, customer.SecurityVersion);
            var vendor = await db.Vendors.AsNoTracking().SingleAsync(item => item.Id == vendorId);
            Assert.Equal(.10m, vendor.CommissionRate); Assert.Equal("Bình Dương", vendor.City);
            var contract = await db.BookingContracts.AsNoTracking().SingleAsync(item => item.Id == contractId);
            Assert.Equal(.10m, contract.CommissionRateSnapshot); Assert.Equal(100000000m, contract.ContractValue);
            Assert.Equal(30000000m, contract.DepositAmount); Assert.Equal(1000000m, contract.AppointmentDepositCredit);
            Assert.Equal(confirmedAt, contract.ConfirmedAt); Assert.Equal(contractVersion, contract.Version);
            Assert.Equal(3, contract.RevisionCount); Assert.Equal(Cancellation, contract.CancellationPolicySnapshot);
            Assert.Equal(ContractStatus.Confirmed, contract.Status); Assert.Empty(await db.Commissions.AsNoTracking().ToListAsync());
        }
        finally
        {
            Assert.StartsWith("WeddingPlanTest_ListingDetails_", database);
            await db.Database.EnsureDeletedAsync();
        }
    }

    private static ServiceDetailsDto WithText(string field, string value)
    {
        var details = new ServiceDetailsDto(); object target = details;
        if (field.StartsWith("FullService.", StringComparison.Ordinal))
        {
            var full = new FullServiceDetailsDto(); details = details with { FullService = full };
            target = full; field = field["FullService.".Length..];
        }
        var property = target.GetType().GetProperty(field, BindingFlags.Public | BindingFlags.Instance);
        Assert.NotNull(property); property.SetValue(target, value); return details;
    }

    private static ServiceDetailsDto Details(bool fullService) => new(
        ShortDescription: "Concise service introduction", PriceUnit: "gói", IncludedItems: "Consultation; delivery; follow-up",
        ExcludedItems: "Third-party expenses not committed here", Duration: "Two preparation sessions",
        Process: "Consultation → preparation → delivery", ServiceOptions: "Standard or extended coverage",
        AdditionalFees: "Extra activities require a written quote", Terms: "Subject to the approved service description",
        ReschedulePolicy: "Request a mutually agreed date", PortfolioUrls: ["https://portfolio.example.invalid/work-one.jpg", "https://portfolio.example.invalid/work-two.jpg"],
        VideoUrl: "https://www.youtube.com/watch?v=synthetic12", Highlights: "Dedicated coordinator",
        CustomerRequirements: "Confirm venue access", Notes: "Operational information only",
        FullService: fullService ? new("Planning and event coordination", "Up to the agreed guest count", "Concept; preparation; wedding day",
            "External vendors require explicit approval", "Management fee quoted separately", "Package price provided in a written quote",
            "Extras require mutual agreement", "Payment terms agreed in the customer contract") : null);

    private sealed record Current(Guid? UserId, string? Role = "VendorOwner") : ICurrentUserService { public string? Email => null; }
    private sealed class FailingAfterSaveContext(DbContextOptions<ApplicationDbContext> options) : ApplicationDbContext(options)
    {
        public override async Task<int> SaveChangesAsync(CancellationToken ct = default)
        {
            await base.SaveChangesAsync(ct); throw new InvalidOperationException("Synthetic failure after saving, before transaction commit.");
        }
    }

    private sealed class Fixture(SqliteConnection connection, ApplicationDbContext db) : IAsyncDisposable
    {
        public ApplicationDbContext Db { get; } = db;
        public DbContextOptions<ApplicationDbContext> Options => new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options;
        public User Owner { get; private set; } = null!;
        public User OtherOwner { get; private set; } = null!;
        public Vendor Vendor { get; private set; } = null!;
        public Vendor OtherVendor { get; private set; } = null!;
        public Category Photo { get; private set; } = null!;
        public Category Planning { get; private set; } = null!;
        public Listing Listing { get; private set; } = null!;
        public ApplicationDbContext OpenContext() => new(Options);
        public CreateListingCommandHandler CreateHandler(Current? actor = null) => new(Db, actor ?? new Current(Owner.Id));
        public UpdateListingCommandHandler UpdateHandler(Current? actor = null) => new(Db, actor ?? new Current(Owner.Id));
        public CreateListingCommand CreateCommand(ServiceDetailsDto? details) => new(Photo.Id, "Created synthetic service", 1000, 2000,
            Description, "Hà Nội", [LocalImage, "https://cdn.example.invalid/second.jpg"], details);
        public UpdateListingCommand UpdateCommand() => new(Listing.Id, "Original synthetic service", 1000, 2000, Description, "Hà Nội");
        public Task<Wedding.Application.Features.Listings.DTOs.ListingDto> Detail() => new GetListingDetailQueryHandler(Db, new Current(Owner.Id)).Handle(new(Listing.Id), default);

        public async Task<string> ListingSnapshot()
        {
            await using var reload = OpenContext();
            var listing = await reload.Listings.AsNoTracking().SingleAsync(item => item.Id == Listing.Id);
            var media = await reload.ListingMedias.AsNoTracking().Where(item => item.ListingId == Listing.Id)
                .OrderBy(item => item.DisplayOrder).ThenBy(item => item.Id).ToListAsync();
            return JsonSerializer.Serialize(new { listing.Id, listing.Title, listing.Description, listing.MinPrice, listing.MaxPrice,
                listing.Location, listing.Status, listing.ServiceDetailsJson, listing.CancellationPolicy, listing.UpdatedAt,
                Media = media.Select(item => new { item.Id, item.MediaUrl, item.DisplayOrder, item.IsFeatured }) });
        }
        public async Task<string> FinancialSnapshot()
        {
            await using var reload = OpenContext();
            var contracts = await reload.BookingContracts.AsNoTracking().OrderBy(item => item.Id).ToListAsync();
            var commissions = await reload.Commissions.AsNoTracking().OrderBy(item => item.Id).ToListAsync();
            var vendors = await reload.Vendors.AsNoTracking().OrderBy(item => item.Id).Select(item => new { item.Id, item.CommissionRate }).ToListAsync();
            return JsonSerializer.Serialize(new { Contracts = contracts.Select(item => new { item.Id, item.ContractValue, item.DepositAmount,
                item.CommissionRateSnapshot, item.ConfirmedAt, item.Version, item.Status }), Commissions = commissions.Select(item => new {
                    item.Id, item.CommissionRate, item.CommissionAmount, item.PaidAmount, item.PaymentReferenceCode, item.Status, item.PaidAt, item.Version }), Vendors = vendors });
        }
        public static async Task<Fixture> Create()
        {
            var connection = new SqliteConnection("Data Source=:memory:"); await connection.OpenAsync();
            var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options);
            await db.Database.EnsureCreatedAsync(); var f = new Fixture(connection, db);
            f.Owner = User.Create("Synthetic owner", "owner@details.example.invalid", "0901234567", "owner-hash", UserRole.VendorOwner);
            f.OtherOwner = User.Create("Other synthetic owner", "other@details.example.invalid", "0901234568", "other-hash", UserRole.VendorOwner);
            db.Users.AddRange(f.Owner, f.OtherOwner);
            f.Vendor = Vendor.Create(f.Owner.Id, "Synthetic shop", "synthetic-details-shop", "Owner", "0901234567", "Hà Nội", .08m);
            f.OtherVendor = Vendor.Create(f.OtherOwner.Id, "Other synthetic shop", "other-details-shop", "Other", "0901234568", "Hà Nội", .08m);
            db.Vendors.AddRange(f.Vendor, f.OtherVendor);
            // Creation now requires the explicit business scope; legacy read/edit behavior is covered separately.
            db.VendorBusinessGroups.AddRange(VendorBusinessGroup.Create(f.Vendor.Id, BusinessGroupPolicy.PhotoBridalMakeupId),
                VendorBusinessGroup.Create(f.Vendor.Id, BusinessGroupPolicy.FullServicePlanningId),
                VendorBusinessGroup.Create(f.OtherVendor.Id, BusinessGroupPolicy.PhotoBridalMakeupId));
            f.Photo = await db.Categories.SingleAsync(item => item.Slug == "quay-chup-photo-video");
            f.Planning = await db.Categories.SingleAsync(item => item.Slug == "wedding-planner");
            f.Listing = Listing.Create(f.Vendor.Id, f.Photo.Id, "Original synthetic service", "original-details-service", 1000, 2000, Description, "Hà Nội");
            f.Listing.SetCancellationPolicy(Cancellation); db.Listings.Add(f.Listing);
            db.ListingMedias.AddRange(ListingMedia.Create(f.Listing.Id, LocalImage, null, isFeatured: true),
                ListingMedia.Create(f.Listing.Id, "https://cdn.example.invalid/second.jpg", null, displayOrder: 1));
            await db.SaveChangesAsync(); return f;
        }
        public async ValueTask DisposeAsync() { await Db.DisposeAsync(); await connection.DisposeAsync(); }
    }
}
