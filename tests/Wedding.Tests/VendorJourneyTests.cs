using System.Data.Common;
using System.Reflection;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Wedding.Application.Common.Interfaces;
using Wedding.Application.Features.VendorJourney;
using Wedding.Domain.Entities;
using Wedding.Domain.Enums;
using Wedding.Domain.Exceptions;
using Wedding.Infrastructure.Persistence;
using Wedding.WebApi.Controllers;

namespace Wedding.Tests;

public class VendorJourneyTests
{
    [Fact]
    public async Task NewShopReturnsRealEmptyMilestonesWithoutInventingApprovalOrTransactions()
    {
        await using var f = await Fixture.Create();
        f.Db.ChangeTracker.Clear(); f.Capture.Start();
        var summary = await f.Read();
        Assert.Equal("Own journey shop", summary.VendorName);
        Assert.False(summary.IsVerified); Assert.Null(summary.LatestVerificationStatus);
        Assert.Null(summary.TaxProfileStatus); Assert.False(summary.PolicyAccepted); Assert.False(summary.AllowInCombos);
        Assert.Equal(new VendorJourneySummaryDto("Own journey shop", false, null, null, false, false,
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0), summary);
        Assert.Empty(f.Db.ChangeTracker.Entries());
        Assert.Single(f.Capture.Statements);
        Assert.DoesNotContain(f.Capture.Statements, statement => statement.Contains("INSERT ") || statement.Contains("UPDATE ") || statement.Contains("DELETE "));
    }

    [Fact]
    public async Task VerificationUsesNewestSubmissionRatherThanAnOlderApprovalAndExposesNoDocuments()
    {
        await using var f = await Fixture.Create();
        var old = VendorVerification.Create(f.Vendor.Id, "private-license-number", "[\"private-document\"]", "private-verification-note");
        old.Review(f.OtherOwner.Id, true, "private-admin-note");
        f.Db.Entry(old).Property(item => item.CreatedAt).CurrentValue = DateTime.UtcNow.AddDays(-1);
        var latest = VendorVerification.Create(f.Vendor.Id, "new-private-license-number", "[]", null);
        var foreign = VendorVerification.Create(f.OtherVendor.Id, "foreign-private-license-number", "[]", null);
        foreign.Review(f.OtherOwner.Id, false, "foreign private rejection");
        f.Vendor.SetVerified(true);
        var tax = VendorTaxProfile.Create(f.Vendor.Id); tax.Review(true);
        f.Db.VendorTaxProfiles.Add(tax); f.Db.VendorVerifications.AddRange(old, latest, foreign);
        await f.Db.SaveChangesAsync();
        var summary = await f.Read();
        Assert.True(summary.IsVerified); Assert.Equal("Submitted", summary.LatestVerificationStatus);
        Assert.Equal("Approved", summary.TaxProfileStatus);
        var json = JsonSerializer.Serialize(summary);
        Assert.DoesNotContain("license", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("private", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("bank", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("CommissionRate", json);
    }

    [Theory]
    [InlineData(VerificationStatus.Draft)] [InlineData(VerificationStatus.Submitted)]
    [InlineData(VerificationStatus.Approved)] [InlineData(VerificationStatus.Rejected)] [InlineData(VerificationStatus.Suspended)]
    public async Task TaxProfileStatusReflectsDatabaseIncludingDraftAndSuspension(VerificationStatus status)
    {
        await using var f = await Fixture.Create();
        var tax = VendorTaxProfile.Create(f.Vendor.Id);
        f.Db.Entry(tax).Property(item => item.Status).CurrentValue = status;
        f.Db.VendorTaxProfiles.Add(tax); await f.Db.SaveChangesAsync();
        Assert.Equal(status.ToString(), (await f.Read()).TaxProfileStatus);
    }

    [Theory]
    [InlineData(true, "policy-v1", true)] [InlineData(false, "policy-v1", false)]
    [InlineData(true, null, false)] [InlineData(true, "   ", false)]
    public async Task PolicyAcceptanceNeedsBothTimestampAndNonblankVersion(bool timestamp, string? version, bool accepted)
    {
        await using var f = await Fixture.Create();
        f.Db.Entry(f.Vendor).Property(item => item.PartnershipPolicyAcceptedAt).CurrentValue = timestamp ? DateTime.UtcNow : null;
        f.Db.Entry(f.Vendor).Property(item => item.PartnershipPolicyVersion).CurrentValue = version;
        f.Vendor.ConfigureComboParticipation(true, .03m); await f.Db.SaveChangesAsync();
        var summary = await f.Read();
        Assert.Equal(accepted, summary.PolicyAccepted); Assert.True(summary.AllowInCombos);
    }

    [Fact]
    public async Task ListingCountsRespectActivityAndLatestRevisionWithoutCountingVersionRowsTwice()
    {
        await using var f = await Fixture.Create();
        await AssertListingCounts(f);
    }

    private static async Task AssertListingCounts(Fixture f)
    {
        f.AddListing(ListingStatus.Draft);
        f.AddListing(ListingStatus.Active);
        var inactive = f.AddListing(ListingStatus.Active); inactive.SetActivity(false);
        f.AddListing(ListingStatus.Hidden);
        f.AddListing(ListingStatus.PendingApproval);
        f.AddListing(ListingStatus.Rejected);
        var livePending = f.AddListing(ListingStatus.Active);
        var firstPending = ListingRevision.Create(livePending.Id, 1, "{}", f.Owner.Id, true); firstPending.Supersede();
        var latestPending = ListingRevision.Create(livePending.Id, 2, "{}", f.Owner.Id, true);
        var liveRejected = f.AddListing(ListingStatus.Active);
        var rejected = ListingRevision.Create(liveRejected.Id, 1, "{}", f.Owner.Id, true); rejected.Review(false, f.OtherOwner.Id, "Please correct content");
        var oldRejectedNowDraft = f.AddListing(ListingStatus.Rejected);
        var older = ListingRevision.Create(oldRejectedNowDraft.Id, 1, "{}", f.Owner.Id, true); older.Review(false, f.OtherOwner.Id, "Old rejection");
        var draft = ListingRevision.Create(oldRejectedNowDraft.Id, 2, "{}", f.Owner.Id);
        f.Db.ListingRevisions.AddRange(firstPending, latestPending, rejected, older, draft);
        foreach (var status in Enum.GetValues<ListingStatus>()) f.AddListing(status, f.OtherVendor);
        await f.Db.SaveChangesAsync();
        var summary = await f.Read();
        Assert.Equal(9, summary.ListingCount); Assert.Equal(3, summary.PublicListingCount);
        Assert.Equal(2, summary.PendingListingCount); Assert.Equal(2, summary.RejectedListingCount);
        Assert.Equal("Own journey shop", summary.VendorName);
    }

    [Fact]
    public async Task ApprovedServiceAndComboCountsDoNotPretendToBePublicAvailability()
    {
        await using var f = await Fixture.Create();
        var listing = f.AddListing(ListingStatus.Draft);
        foreach (var status in new[] { "Draft", "Pending", "Approved", "Rejected" })
        {
            var item = ServiceItem.Create(f.Vendor.Id, listing.Id, f.Category.Id, "Own item", 1000, "Package", 1, 100, "Hà Nội", "Policy");
            f.Db.Entry(item).Property(value => value.ModerationStatus).CurrentValue = status;
            if (status == "Approved") f.Db.Entry(item).Property(value => value.IsActive).CurrentValue = false;
            f.Db.ServiceItems.Add(item);
            f.AddCombo(f.Vendor, status);
        }
        var foreignListing = f.AddListing(ListingStatus.Active, f.OtherVendor);
        var foreignItem = ServiceItem.Create(f.OtherVendor.Id, foreignListing.Id, f.Category.Id, "Foreign item", 5000, "Package", 1, 100, "Hà Nội", "Policy");
        foreignItem.Submit(); foreignItem.Review(true, null); f.Db.ServiceItems.Add(foreignItem);
        f.AddCombo(f.OtherVendor, "Approved");
        // Unowned legacy templates are not attributed to the current shop.
        f.Db.ComboTemplates.Add(ComboTemplate.Create("Platform legacy template", "Popular", "Classic", null, null, 0));
        await f.Db.SaveChangesAsync();
        var summary = await f.Read();
        Assert.Equal(4, summary.ServiceItemCount); Assert.Equal(1, summary.ApprovedServiceItemCount);
        Assert.Equal(4, summary.ComboCount); Assert.Equal(1, summary.ApprovedComboCount); Assert.Equal(1, summary.PendingComboCount);
        Assert.False(summary.AllowInCombos); Assert.Equal(0, summary.PublicListingCount);
    }

    [Fact]
    public async Task PendingComboCountFollowsLatestProposalWhileApprovedBaselineRemainsApproved()
    {
        await using var f = await Fixture.Create();
        await AssertComboRevisionCounts(f);
    }

    private static async Task AssertComboRevisionCounts(Fixture f)
    {
        var live = f.AddCombo(f.Vendor, "Approved");
        var older = ComboTemplateRevision.Create(live.Id, 1, "{}", f.Owner.Id, "VendorOwner", true); older.Supersede();
        var current = ComboTemplateRevision.Create(live.Id, 2, "{}", f.Owner.Id, "VendorOwner", true);
        var legacyPending = f.AddCombo(f.Vendor, "Pending");
        var newerDraft = ComboTemplateRevision.Create(legacyPending.Id, 1, "{}", f.Owner.Id, "VendorOwner", false);
        var foreign = f.AddCombo(f.OtherVendor, "Pending");
        f.Db.ComboTemplateRevisions.AddRange(older, current, newerDraft,
            ComboTemplateRevision.Create(foreign.Id, 1, "{}", f.OtherOwner.Id, "VendorOwner", true));
        await f.Db.SaveChangesAsync();
        var summary = await f.Read();
        Assert.Equal(2, summary.ComboCount); Assert.Equal(1, summary.ApprovedComboCount); Assert.Equal(1, summary.PendingComboCount);
    }

    [Fact]
    public async Task InboxQuotesContractsChangesAndUpcomingAppointmentsAreScopedToOwnShopAndCurrentStatuses()
    {
        await using var f = await Fixture.Create();
        foreach (var vendor in new[] { f.Vendor, f.OtherVendor })
        {
            var leads = new List<Lead>();
            foreach (var status in Enum.GetValues<LeadStatus>())
            {
                var lead = Lead.Create(f.Customer.Id, vendor.Id, null, "0901234567", null, null, null, "Private customer message");
                f.Db.Entry(lead).Property(item => item.Status).CurrentValue = status;
                f.Db.Leads.Add(lead); leads.Add(lead);
            }
            f.Db.Quotes.Add(Quote.Create(leads[0].Id, vendor.Id, f.Customer.Id, "[]", 10000, 1000, null, 7, 1));
            f.Db.BookingContracts.Add(BookingContract.Create(leads[1].Id, null, vendor.Id, f.Customer.Id, 10000, 1000, null, null));
            foreach (var status in new[] { "PendingVendor", "PendingFinance", "Acknowledged", "Rejected" })
            {
                var change = OrderChangeRequest.Create(f.Customer.Id, vendor.Id, "contract", Guid.NewGuid(), "Change", "Please update this order", null, false);
                f.Db.Entry(change).Property(item => item.Status).CurrentValue = status; f.Db.OrderChangeRequests.Add(change);
            }
            var type = AppointmentType.Create(vendor.Id, "Visit", null, 60, 1, "1,2,3,4,5,6,0", TimeSpan.FromHours(9), TimeSpan.FromHours(18), 0, true);
            f.Db.AppointmentTypes.Add(type);
            foreach (var status in Enum.GetValues<AppointmentStatus>())
            {
                var appointment = Appointment.Create(type, f.Customer.Id, DateTime.UtcNow.AddDays(3), 2, null, null);
                f.Db.Entry(appointment).Property(item => item.Status).CurrentValue = status; f.Db.Appointments.Add(appointment);
            }
            var past = Appointment.Create(type, f.Customer.Id, DateTime.UtcNow.AddDays(2), 2, null, null);
            f.Db.Entry(past).Property(item => item.StartAt).CurrentValue = DateTime.UtcNow.AddDays(-1); f.Db.Appointments.Add(past);
        }
        await f.Db.SaveChangesAsync(); f.Db.ChangeTracker.Clear(); f.Capture.Start();
        var summary = await f.Read();
        Assert.Equal(1, summary.NewLeadCount); Assert.Equal(2, summary.ActiveLeadCount);
        Assert.Equal(1, summary.QuoteCount); Assert.Equal(1, summary.ContractCount);
        Assert.Equal(1, summary.PendingOrderChangeCount); Assert.Equal(5, summary.UpcomingAppointmentCount);
        Assert.Single(f.Capture.Statements); Assert.Empty(f.Db.ChangeTracker.Entries());
        Assert.DoesNotContain("Private customer message", JsonSerializer.Serialize(summary));
    }

    [Theory]
    [InlineData(null)] [InlineData("Customer")] [InlineData("Moderator")] [InlineData("Finance")]
    [InlineData("CustomerCare")] [InlineData("SuperAdmin")] [InlineData("vendorowner")]
    public async Task OtherClaimedRolesCannotReadTheSummary(string? role)
    {
        await using var f = await Fixture.Create(); f.Capture.Start();
        await Assert.ThrowsAsync<DomainException>(() => f.Read(new Current(f.Owner.Id, role)));
        Assert.Empty(f.Capture.Statements);
    }

    [Theory]
    [InlineData("missing-id")] [InlineData("empty-id")] [InlineData("unknown-id")]
    [InlineData("missing-shop")] [InlineData("disabled-user")] [InlineData("disabled-shop")] [InlineData("changed-role")]
    public async Task PersistedOwnershipRoleAndActivityAreRequiredEvenWithVendorOwnerClaim(string situation)
    {
        await using var f = await Fixture.Create();
        Guid? id = f.Owner.Id;
        if (situation == "missing-id") id = null;
        if (situation == "empty-id") id = Guid.Empty;
        if (situation == "unknown-id") id = Guid.NewGuid();
        if (situation == "missing-shop") id = f.Customer.Id;
        if (situation == "disabled-user") f.Owner.SetStatus(false);
        if (situation == "disabled-shop") f.Db.Entry(f.Vendor).Property(item => item.IsActive).CurrentValue = false;
        if (situation == "changed-role") f.Db.Entry(f.Owner).Property(item => item.Role).CurrentValue = UserRole.Customer;
        await f.Db.SaveChangesAsync();
        await Assert.ThrowsAsync<DomainException>(() => f.Read(new Current(id)));
    }

    [Theory]
    [InlineData(null, false)] [InlineData("Customer", false)] [InlineData("Moderator", false)]
    [InlineData("Finance", false)] [InlineData("CustomerCare", false)] [InlineData("SuperAdmin", false)] [InlineData("VendorOwner", true)]
    public async Task ControllerEndpointRequiresAuthenticatedVendorOwnerWithoutOwnerIdParameters(string? role, bool allowed)
    {
        using var services = new ServiceCollection().AddLogging().AddAuthorizationCore().BuildServiceProvider();
        var controller = typeof(VendorJourneyController);
        var method = controller.GetMethod(nameof(VendorJourneyController.GetMine))!;
        Assert.Equal("api/vendor-journey", controller.GetCustomAttribute<RouteAttribute>()?.Template);
        Assert.Equal("me", method.GetCustomAttribute<HttpGetAttribute>()?.Template);
        Assert.DoesNotContain(controller.GetCustomAttributes(), attribute => attribute is AllowAnonymousAttribute);
        Assert.DoesNotContain(method.GetCustomAttributes(), attribute => attribute is AllowAnonymousAttribute);
        Assert.DoesNotContain(typeof(GetVendorJourneyQuery).GetProperties(), property => property.Name is "VendorId" or "UserId");
        Assert.Equal(typeof(CancellationToken), Assert.Single(method.GetParameters()).ParameterType);
        var data = controller.GetCustomAttributes<AuthorizeAttribute>().Concat(method.GetCustomAttributes<AuthorizeAttribute>()).Cast<IAuthorizeData>();
        var policy = await AuthorizationPolicy.CombineAsync(services.GetRequiredService<IAuthorizationPolicyProvider>(), data);
        Assert.NotNull(policy);
        var principal = role == null ? new ClaimsPrincipal(new ClaimsIdentity()) :
            new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Role, role)], "SyntheticAuthentication"));
        Assert.Equal(allowed, (await services.GetRequiredService<IAuthorizationService>().AuthorizeAsync(principal, null, policy)).Succeeded);
    }

    [SqlServerFact]
    public async Task SqlServerTranslatesLatestRevisionCountsAsBoundedReadOnlyAggregateProjection()
    {
        await using var f = await Fixture.Create(sql: true);
        await AssertListingCounts(f); await AssertComboRevisionCounts(f);
        f.Db.ChangeTracker.Clear(); f.Capture.Start();
        await f.Read();
        var statement = Assert.Single(f.Capture.Statements);
        Assert.Contains("COUNT(*)", statement, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("TOP(1)", statement, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("SELECT *", statement, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(f.Db.ChangeTracker.Entries());
    }

    private sealed record Current(Guid? UserId, string? Role = "VendorOwner") : ICurrentUserService { public string? Email => null; }

    private sealed class CommandCapture : DbCommandInterceptor
    {
        private bool enabled;
        public List<string> Statements { get; } = [];
        public void Start() { Statements.Clear(); enabled = true; }
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,
            CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            if (enabled) Statements.Add(command.CommandText);
            return ValueTask.FromResult(result);
        }
    }

    private sealed class Fixture(ApplicationDbContext db, SqliteConnection? connection, string? databaseName, CommandCapture capture) : IAsyncDisposable
    {
        public ApplicationDbContext Db { get; } = db;
        public CommandCapture Capture { get; } = capture;
        public User Owner { get; private set; } = null!;
        public User OtherOwner { get; private set; } = null!;
        public User Customer { get; private set; } = null!;
        public Vendor Vendor { get; private set; } = null!;
        public Vendor OtherVendor { get; private set; } = null!;
        public Category Category { get; private set; } = null!;
        public Task<VendorJourneySummaryDto> Read(ICurrentUserService? actor = null) =>
            new GetVendorJourneyQueryHandler(Db, actor ?? new Current(Owner.Id)).Handle(new(), default);

        public static async Task<Fixture> Create(bool sql = false)
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>();
            SqliteConnection? connection = null; string? databaseName = null;
            if (sql)
            {
                databaseName = $"WeddingPlanTest_VendorJourney_{Guid.NewGuid():N}";
                var source = Environment.GetEnvironmentVariable("WEDDING_TEST_SQLSERVER");
                var builder = new SqlConnectionStringBuilder(string.IsNullOrWhiteSpace(source) || source == "1"
                    ? "Server=localhost;Integrated Security=true;TrustServerCertificate=true;Connection Timeout=5" : source)
                    { InitialCatalog = databaseName };
                options.UseSqlServer(builder.ConnectionString);
            }
            else
            {
                connection = new SqliteConnection("Data Source=:memory:"); await connection.OpenAsync(); options.UseSqlite(connection);
            }
            var capture = new CommandCapture(); options.AddInterceptors(capture);
            var f = new Fixture(new ApplicationDbContext(options.Options), connection, databaseName, capture);
            try
            {
                if (sql) await f.Db.Database.MigrateAsync(); else await f.Db.Database.EnsureCreatedAsync();
                f.Owner = User.Create("Own synthetic owner", "owner@journey.example.invalid", "0901234567", "hash", UserRole.VendorOwner);
                f.OtherOwner = User.Create("Foreign synthetic owner", "other@journey.example.invalid", "0901234568", "hash", UserRole.VendorOwner);
                f.Customer = User.Create("Synthetic customer", "customer@journey.example.invalid", "0901234569", "hash", UserRole.Customer);
                f.Db.Users.AddRange(f.Owner, f.OtherOwner, f.Customer);
                f.Vendor = Vendor.Create(f.Owner.Id, "Own journey shop", "own-journey-shop", "Owner", "0901234567", "Hà Nội", .08m);
                f.OtherVendor = Vendor.Create(f.OtherOwner.Id, "Foreign journey shop", "foreign-journey-shop", "Other", "0901234568", "Hà Nội", .08m);
                f.Db.Vendors.AddRange(f.Vendor, f.OtherVendor);
                f.Category = Category.Create("Synthetic journey category", "journey-category", null, null, .08m);
                f.Db.Categories.Add(f.Category); await f.Db.SaveChangesAsync(); return f;
            }
            catch { await f.DisposeAsync(); throw; }
        }

        public Listing AddListing(ListingStatus status, Vendor? vendor = null)
        {
            var item = Listing.Create((vendor ?? Vendor).Id, Category.Id, "Journey listing", $"journey-listing-{Guid.NewGuid():N}", 10000, 20000, "Synthetic description", "Hà Nội");
            if (status == ListingStatus.Rejected) item.Reject("Synthetic rejection");
            else if (status != ListingStatus.Draft) item.SetStatus(status);
            Db.Listings.Add(item); return item;
        }

        public ComboTemplate AddCombo(Vendor vendor, string status)
        {
            var item = ComboTemplate.Create("Journey combo", "Popular", "Classic", null, null, 0);
            item.SetScope(vendor.Id, "Hà Nội", null, status == "Approved");
            if (status is "Draft" or "Rejected") item.StageUnpublished(vendor.Id, status);
            if (status == "Approved") item.SetActivity(false);
            Db.ComboTemplates.Add(item); return item;
        }

        public async ValueTask DisposeAsync()
        {
            if (databaseName != null)
            {
                Assert.StartsWith("WeddingPlanTest_VendorJourney_", databaseName);
                await Db.Database.EnsureDeletedAsync();
            }
            await Db.DisposeAsync(); if (connection != null) await connection.DisposeAsync();
        }
    }
}
