using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Data.SqlClient;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Wedding.Application.Common.Interfaces;
using Wedding.Application.Features.PrivateServiceAgreements;
using Wedding.Application.Features.Combos;
using Wedding.Application.Features.Listings;
using Wedding.Domain.Entities;
using Wedding.Domain.Enums;
using Wedding.Domain.Exceptions;
using Wedding.Infrastructure.Persistence;
using Wedding.WebApi.Controllers;

namespace Wedding.Tests;

public class PrivateServiceAgreementTests
{
    [Theory]
    [InlineData("combo")] [InlineData("contract")]
    public async Task ContentOnlyAgreementRequiresCustomerAndPreservesOriginalMoneyAndContent(string kind)
    {
        await using var f = await Fixture.Create(kind: kind);
        await ContentScenario(f);
    }

    private static async Task ContentScenario(Fixture f)
    {
        var money = await f.OriginalSnapshot(); var before = await f.Get(f.CustomerActor);
        var pending = await f.Propose();
        Assert.False(pending.CanPropose); Assert.Equal(before.EffectiveContent, pending.EffectiveContent);
        var customer = await f.Get(f.CustomerActor); Assert.True(customer.CanRespond);
        await Assert.ThrowsAsync<DomainException>(() => f.Respond(f.OwnerActor, customer, true));
        var accepted = await f.Respond(f.CustomerActor, customer, true);
        Assert.Equal("Accepted", accepted.History.Single().Status);
        Assert.Equal("Agreed replacement", accepted.EffectiveContent.Single().Name);
        Assert.Equal(before.OriginalContent, accepted.OriginalContent);
        Assert.Equal(money, await f.OriginalSnapshot());
        Assert.Equal(2, await f.Db.Notifications.CountAsync());
        Assert.Equal(2, await f.Db.AuditLogs.CountAsync(x => x.Action.StartsWith("PrivateServices.")));
        Assert.All(await f.Db.Notifications.ToListAsync(), n => { Assert.Equal(f.Kind, n.ContextKind); Assert.Equal(f.TargetId, n.ContextId); });
        await Assert.ThrowsAsync<DomainException>(() => f.Respond(f.CustomerActor, accepted, true));
        Assert.Empty(await f.Db.Commissions.ToListAsync()); Assert.Empty(await f.Db.PaymentTransactions.ToListAsync());
        Assert.Empty(await f.Db.ComboTemplates.ToListAsync());
    }

    [Fact]
    public async Task RejectedAndWithdrawnProposalsRemainInHistoryAndDoNotReplaceAcceptedContent()
    {
        await using var f = await Fixture.Create();
        await f.Propose(); var accepted = await f.Respond(f.CustomerActor, await f.Get(f.CustomerActor), true);
        var next = await f.Propose("Not accepted");
        var rejected = await f.Respond(f.CustomerActor, next, false);
        Assert.Equal("Agreed replacement", rejected.EffectiveContent.Single().Name);
        var third = await f.Propose("Withdrawn content");
        var withdrawn = await f.Respond(f.OwnerActor, third, false, true);
        Assert.Equal(new[] { "Withdrawn", "Rejected", "Accepted" }, withdrawn.History.Select(x => x.Status));
        Assert.Equal(new[] { 3, 2, 1 }, withdrawn.History.Select(x => x.Sequence));
        Assert.Equal("Agreed replacement", withdrawn.EffectiveContent.Single().Name);
        Assert.Equal(accepted.OriginalContent, withdrawn.OriginalContent);
    }

    [Fact]
    public async Task PendingAndStaleVersionsCannotBeOverwrittenOrConfirmed()
    {
        await using var f = await Fixture.Create(); var old = await f.Get(f.OwnerActor);
        await f.Propose();
        await Assert.ThrowsAsync<DomainException>(() => f.Propose());
        var pending = await f.Get(f.CustomerActor);
        await Assert.ThrowsAsync<DomainException>(() => f.Respond(f.CustomerActor, old, true, proposalId: pending.History.Single().Id));
        Assert.Equal("PendingCustomer", (await f.Get(f.CustomerActor)).History.Single().Status);
    }

    [Fact]
    public async Task OriginalComboContentUsesFrozenScopeAndLeadExpiryInvalidatesPendingAcceptance()
    {
        await using var f = await Fixture.Create(); var item = f.Order.Items.Single();
        var snapshot = new ComboComponentSnapshotDto(Guid.NewGuid(), item.ListingId, f.Vendor.Id, item.CategoryId, "Frozen source", "Frozen listing", 1_000_000, "Package", 1, 1, 1000, "HCM", "Frozen cancellation", "Frozen description", null, [],
            new ServiceDetailsDto(IncludedItems: "10 giờ chụp và 200 ảnh", ExcludedItems: "Không gồm vé máy bay", Duration: "10 giờ"), [], [], "original-version");
        item.FreezeComponent(ComboFixedPackages.Serialize(snapshot)); await f.Db.SaveChangesAsync();
        var original = await f.Get(f.CustomerActor);
        Assert.Contains("10 giờ chụp và 200 ảnh", original.OriginalContent.Single().Details);
        Assert.Contains("Frozen description", original.OriginalContent.Single().Details);
        Assert.Contains("Frozen cancellation", original.OriginalContent.Single().Details);
        await f.Propose(); f.Lead.Expire(); await f.Db.SaveChangesAsync();
        var expired = await f.Get(f.CustomerActor); Assert.False(expired.CanRespond);
        await Assert.ThrowsAsync<DomainException>(() => f.Respond(f.CustomerActor, expired, true));
        var withdrawn = await f.Respond(f.OwnerActor, await f.Get(f.OwnerActor), false, true);
        Assert.Equal("Withdrawn", withdrawn.History.Single().Status);
    }

    [Fact]
    public async Task AcceptedComboScopeRequiresFreshQuoteOrContractAndIsReadOnlyReferenceForContract()
    {
        await using var f = await Fixture.Create(); var before = DateTime.UtcNow.AddMinutes(-1);
        await f.Propose();
        await Assert.ThrowsAsync<DomainException>(() => PrivateAgreementContractHandoff.EnsureReviewedAsync(f.Db, f.Lead, DateTime.UtcNow, default));
        await f.Respond(f.CustomerActor, await f.Get(f.CustomerActor), true);
        await Assert.ThrowsAsync<DomainException>(() => PrivateAgreementContractHandoff.EnsureReviewedAsync(f.Db, f.Lead, before, default));
        await PrivateAgreementContractHandoff.EnsureReviewedAsync(f.Db, f.Lead, DateTime.UtcNow.AddSeconds(1), default);
        var contract = BookingContract.Create(f.Lead.Id, null, f.Vendor.Id, f.Customer.Id, 1_000_000, 200_000, null, DateTime.UtcNow.AddDays(10)); contract.ConfirmByCustomer(); contract.SnapshotCommissionRate(.08m); f.Db.BookingContracts.Add(contract); await f.Db.SaveChangesAsync();
        var w = await new GetPrivateAgreementQueryHandler(f.Db, f.CustomerActor).Handle(new("contract", contract.Id), default);
        Assert.Equal(f.Order.Id, w.SourceComboId); Assert.Equal("Agreed replacement", Assert.Single(w.SourceComboContent!).Name);
        Assert.NotEqual(w.SourceComboContent, w.EffectiveContent); Assert.Equal(1_000_000, contract.ContractValue);
    }

    [Theory]
    [InlineData("combo")] [InlineData("contract")]
    public async Task ClosingTargetBlocksConfirmationButAllowsVendorToWithdrawStalePendingProposal(string kind)
    {
        await using var f = await Fixture.Create(kind: kind); await f.Propose();
        if (kind == "combo") f.Order.Cancel(); else f.Contract!.Cancel("Agreed cancellation elsewhere");
        await f.Db.SaveChangesAsync();
        var blocked = await f.Get(f.CustomerActor); Assert.False(blocked.CanRespond); Assert.NotNull(blocked.BlockedReason);
        await Assert.ThrowsAsync<DomainException>(() => f.Respond(f.CustomerActor, blocked, true));
        var withdrawn = await f.Respond(f.OwnerActor, await f.Get(f.OwnerActor), false, true);
        Assert.Equal("Withdrawn", withdrawn.History.Single().Status);
        Assert.False(withdrawn.CanPropose);
    }

    [Theory]
    [InlineData("intruder-customer")] [InlineData("intruder-vendor")] [InlineData("forged-role")]
    [InlineData("moderator")] [InlineData("admin")] [InlineData("inactive")]
    public async Task ActorDatabaseRoleOwnershipAndActivityAreEnforced(string variant)
    {
        await using var f = await Fixture.Create();
        var actor = variant switch
        {
            "intruder-customer" => new Actor(f.OtherCustomer.Id, "Customer"),
            "intruder-vendor" => new Actor(f.OtherOwner.Id, "VendorOwner"),
            "forged-role" => new Actor(f.Customer.Id, "VendorOwner"),
            "moderator" => new Actor(f.Staff.Id, "Moderator"),
            "admin" => new Actor(f.Staff.Id, "SuperAdmin"),
            _ => f.OwnerActor
        };
        if (variant == "inactive") { f.Db.Entry(f.Owner).Property(x => x.IsActive).CurrentValue = false; await f.Db.SaveChangesAsync(); }
        await Assert.ThrowsAnyAsync<Exception>(() => f.Get(actor));
        Assert.Empty(await f.Db.PrivateServiceAgreements.ToListAsync());
    }

    [Theory]
    [InlineData("empty")] [InlineData("quantity")] [InlineData("reason")] [InlineData("name")] [InlineData("terms")]
    public async Task InvalidContentIsRejectedAtomically(string invalid)
    {
        await using var f = await Fixture.Create(); var workspace = await f.Get(f.OwnerActor);
        var command = new ProposePrivateAgreementCommand(f.Kind, f.TargetId, workspace.CurrentVersion, [new("New", 1, "")], "Agreed reason", "Terms");
        command = invalid switch
        {
            "empty" => command with { Items = [] }, "quantity" => command with { Items = [new("New", 0, "")] },
            "reason" => command with { Note = " " }, "name" => command with { Items = [new(new string('x', 201), 1, "")] },
            _ => command with { Terms = new string('x', 4001) }
        };
        await Assert.ThrowsAsync<DomainException>(() => new ProposePrivateAgreementCommandHandler(f.Db, f.OwnerActor).Handle(command, default));
        Assert.Empty(await f.Db.PrivateServiceAgreements.ToListAsync()); Assert.Empty(await f.Db.Notifications.ToListAsync());
    }

    [Fact]
    public async Task ContractedComboCannotReceiveParallelAmendmentAndWrongProposalTargetIsDenied()
    {
        await using var f = await Fixture.Create(kind: "contract");
        var combo = await new GetPrivateAgreementQueryHandler(f.Db, f.OwnerActor).Handle(new("combo", f.Order.Id), default);
        Assert.False(combo.CanPropose); Assert.Contains("hợp đồng", combo.BlockedReason);
        await f.Propose(); var w = await f.Get(f.CustomerActor);
        await Assert.ThrowsAsync<NotFoundException>(() => f.Respond(f.CustomerActor, w, true, proposalId: Guid.NewGuid()));
    }

    [Fact]
    public async Task ComboWorkspaceRequiresExplicitLeadContextWhenOrderContainsMultipleConversations()
    {
        await using var f = await Fixture.Create();
        var categoryId = f.Order.Items.Single().CategoryId;
        var firstListing = await f.Db.Listings.SingleAsync(x => x.Id == f.Order.Items.Single().ListingId);
        var secondListing = Listing.Create(f.Vendor.Id, categoryId, "Second combo service", "second-" + Guid.NewGuid().ToString("N"), 1_000_000, 1_000_000, "Second scope", "HCM");
        secondListing.Approve();
        var order = ComboOrder.Create(f.Customer.Id, null, DateTime.UtcNow.AddDays(10), 100, "Two-conversation combo",
            [(firstListing.Id, f.Vendor.Id, categoryId, "Original title", 1_000_000),
             (secondListing.Id, f.Vendor.Id, categoryId, "Second combo service", 1_000_000)], fixedPackage: true);
        var firstLead = Lead.Create(f.Customer.Id, f.Vendor.Id, firstListing.Id, "0901234567", DateTime.UtcNow.AddDays(10), 100, 1_000_000, "First combo lead");
        var secondLead = Lead.Create(f.Customer.Id, f.Vendor.Id, secondListing.Id, "0901234567", DateTime.UtcNow.AddDays(10), 100, 1_000_000, "Second combo lead");
        firstLead.Accept(); firstLead.AttachSource("Combo", order.Id);
        secondLead.Accept(); secondLead.AttachSource("Combo", order.Id);
        order.Items.Single(item => item.ListingId == firstListing.Id).AttachLead(firstLead.Id);
        order.Items.Single(item => item.ListingId == secondListing.Id).AttachLead(secondLead.Id);
        f.Db.Listings.Add(secondListing); f.Db.Leads.AddRange(firstLead, secondLead); f.Db.ComboOrders.Add(order);
        await f.Db.SaveChangesAsync();

        var handler = new GetPrivateAgreementQueryHandler(f.Db, f.CustomerActor);
        var unscoped = await handler.Handle(new("combo", order.Id), default);
        Assert.Null(unscoped.LeadId);
        Assert.Equal(2, unscoped.LeadOptions!.Count);
        Assert.Contains(unscoped.LeadOptions, option => option.LeadId == firstLead.Id && option.ListingTitle == "Original title");
        Assert.Contains(unscoped.LeadOptions, option => option.LeadId == secondLead.Id && option.ListingTitle == "Second combo service");

        var first = await handler.Handle(new("combo", order.Id, firstLead.Id), default);
        var second = await handler.Handle(new("combo", order.Id, secondLead.Id), default);
        Assert.Equal(firstLead.Id, first.LeadId);
        Assert.Equal(secondLead.Id, second.LeadId);
        await Assert.ThrowsAsync<NotFoundException>(() => handler.Handle(new("combo", order.Id, f.Lead.Id), default));
        await Assert.ThrowsAsync<NotFoundException>(() => handler.Handle(new("combo", order.Id, Guid.NewGuid()), default));
    }

    [Theory]
    [InlineData("body")] [InlineData("delete")] [InlineData("final-response")]
    public async Task OriginalProposalAndFinalResponseCannotBeRewrittenThroughEf(string tamper)
    {
        await using var f = await Fixture.Create(); await f.Propose();
        if (tamper == "final-response") await f.Respond(f.CustomerActor, await f.Get(f.CustomerActor), true);
        var row = await f.Db.PrivateServiceAgreements.SingleAsync();
        if (tamper == "delete") f.Db.PrivateServiceAgreements.Remove(row);
        else if (tamper == "body") f.Db.Entry(row).Property(x => x.ItemsJson).CurrentValue = "[]";
        else f.Db.Entry(row).Property(x => x.Response).CurrentValue = "Rewritten response";
        await Assert.ThrowsAsync<InvalidOperationException>(() => f.Db.SaveChangesAsync());
    }

    [Fact]
    public void ApiWritePermissionsAreExplicitAndProposalDoesNotContainFinancialFields()
    {
        var controller = typeof(PrivateServiceAgreementsController);
        Assert.Contains(controller.GetCustomAttributes(typeof(AuthorizeAttribute), false).Cast<AuthorizeAttribute>(), a => a.Roles == "Customer,VendorOwner");
        foreach (var (method, role) in new[] { ("Propose", "VendorOwner"), ("Respond", "Customer"), ("Withdraw", "VendorOwner") })
            Assert.Contains(controller.GetMethod(method)!.GetCustomAttributes(typeof(AuthorizeAttribute), false).Cast<AuthorizeAttribute>(), a => a.Roles == role);
        Assert.Equal(new[] { "ExpectedVersion", "Items", "Note", "Terms" }, typeof(PrivateServiceAgreementsController.ProposalBody).GetProperties().Select(x => x.Name));
    }

    [SqlServerFact]
    public async Task SqlUpgradeIsAdditiveReplayableAndPersistsContentWithoutChangingOriginalContract()
    {
        var database = $"WeddingPlanTest_PrivateServices_{Guid.NewGuid():N}";
        var source = Environment.GetEnvironmentVariable("WEDDING_TEST_SQLSERVER");
        var builder = new SqlConnectionStringBuilder(string.IsNullOrWhiteSpace(source) || source == "1"
            ? "Server=localhost;Integrated Security=true;TrustServerCertificate=true;Connection Timeout=5" : source) { InitialCatalog = database };
        await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlServer(builder.ConnectionString).Options);
        try
        {
            await db.Database.MigrateAsync(); await db.Database.MigrateAsync(); Assert.False(db.Database.HasPendingModelChanges());
            await using var f = await Fixture.Create(db, "contract"); await ContentScenario(f);
            db.ChangeTracker.Clear();
            var persisted = await f.Get(f.CustomerActor); Assert.Equal("Accepted", persisted.History.Single().Status);
            Assert.Equal(.08m, (await db.BookingContracts.SingleAsync()).CommissionRateSnapshot);
        }
        finally { Assert.StartsWith("WeddingPlanTest_PrivateServices_", database); await db.Database.EnsureDeletedAsync(); }
    }

    private sealed record Actor(Guid? UserId, string? Role) : ICurrentUserService { public string? Email => null; }
    private sealed class Fixture(ApplicationDbContext db, SqliteConnection? sqlite, string kind) : IAsyncDisposable
    {
        public ApplicationDbContext Db => db; public string Kind => kind;
        public User Owner = null!, Customer = null!, OtherCustomer = null!, OtherOwner = null!, Staff = null!;
        public Vendor Vendor = null!; public ComboOrder Order = null!; public BookingContract? Contract; public Lead Lead = null!;
        public Guid TargetId => kind == "contract" ? Contract!.Id : Order.Id;
        public Actor OwnerActor => new(Owner.Id, "VendorOwner"); public Actor CustomerActor => new(Customer.Id, "Customer");
        public Task<PrivateAgreementWorkspace> Get(Actor actor) => new GetPrivateAgreementQueryHandler(db, actor).Handle(new(kind, TargetId), default);
        public async Task<PrivateAgreementWorkspace> Propose(string name = "Agreed replacement")
        { var w = await Get(OwnerActor); return await new ProposePrivateAgreementCommandHandler(db, OwnerActor).Handle(new(kind, TargetId, w.CurrentVersion, [new(name, 2, "Private agreed scope")], "Customer asked in chat", "Same total; written content agreement only."), default); }
        public Task<PrivateAgreementWorkspace> Respond(Actor actor, PrivateAgreementWorkspace w, bool accept, bool withdraw = false, Guid? proposalId = null)
            => new RespondPrivateAgreementCommandHandler(db, actor).Handle(new(kind, TargetId, proposalId ?? w.History.First().Id, w.CurrentVersion, accept, "Customer response", withdraw), default);
        public async Task<string> OriginalSnapshot()
        {
            var order = await db.ComboOrders.AsNoTracking().Include(x => x.Items).SingleAsync();
            var contract = await db.BookingContracts.AsNoTracking().SingleOrDefaultAsync();
            return JsonSerializer.Serialize(new { order.Total, order.Subtotal, order.DiscountAmount, Items = order.Items.Select(x => new { x.Title, x.Quantity, x.Price, x.DiscountShare, x.ComponentSnapshotJson, x.SelectionJson }),
                Contract = contract == null ? null : new { contract.ContractValue, contract.DepositAmount, contract.CommissionRateSnapshot, contract.CommissionCategoryIdSnapshot, contract.Version, contract.CancellationPolicySnapshot },
                Quotes = await db.Quotes.AsNoTracking().Select(x => x.ItemsJson).ToListAsync() });
        }
        public static async Task<Fixture> Create(ApplicationDbContext? existing = null, string kind = "combo")
        {
            SqliteConnection? sqlite = null;
            if (existing == null) { sqlite = new("Data Source=:memory:"); await sqlite.OpenAsync(); existing = new(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(sqlite).Options); await existing.Database.EnsureCreatedAsync(); }
            var f = new Fixture(existing, sqlite, kind); var db = existing;
            User Add(UserRole role) { var user = User.Create("Synthetic private agreement account", $"{Guid.NewGuid():N}@private-services.example.invalid", "0901234567", "not-a-real-login", role); db.Users.Add(user); return user; }
            f.Owner = Add(UserRole.VendorOwner); f.Customer = Add(UserRole.Customer); f.OtherCustomer = Add(UserRole.Customer); f.OtherOwner = Add(UserRole.VendorOwner); f.Staff = Add(UserRole.Moderator);
            f.Vendor = Vendor.Create(f.Owner.Id, "Synthetic vendor", "private-" + Guid.NewGuid().ToString("N"), "Owner", "0901234567", "HCM", .08m); db.Vendors.Add(f.Vendor);
            var category = await db.Categories.FirstAsync();
            var listing = Listing.Create(f.Vendor.Id, category.Id, "Original service", "private-" + Guid.NewGuid().ToString("N"), 1_000_000, 1_000_000, "Original scope", "HCM"); listing.Approve(); db.Listings.Add(listing);
            f.Order = ComboOrder.Create(f.Customer.Id, null, DateTime.UtcNow.AddDays(10), 100, "Original order", [(listing.Id, f.Vendor.Id, category.Id, "Original title", 1_000_000)]); db.ComboOrders.Add(f.Order);
            var lead = Lead.Create(f.Customer.Id, f.Vendor.Id, listing.Id, "0901234567", DateTime.UtcNow.AddDays(10), 100, 1_000_000, "Original lead"); lead.Accept(); db.Leads.Add(lead); f.Order.Items.Single().AttachLead(lead.Id);
            lead.AttachSource("Combo", f.Order.Id); f.Lead = lead;
            if (kind == "contract")
            {
                var quote = Quote.Create(lead.Id, f.Vendor.Id, f.Customer.Id, "[{\"Name\":\"Original quote line\",\"Quantity\":1,\"UnitPrice\":1000000}]", 1_000_000, 200_000, "Original terms", 7, 1); quote.Accept(); db.Quotes.Add(quote);
                f.Contract = BookingContract.Create(lead.Id, null, f.Vendor.Id, f.Customer.Id, 1_000_000, 200_000, null, DateTime.UtcNow.AddDays(10)); f.Contract.LinkQuote(quote.Id); f.Contract.ConfirmByCustomer(); f.Contract.SnapshotCommissionRate(.08m); db.BookingContracts.Add(f.Contract);
            }
            await db.SaveChangesAsync(); return f;
        }
        public async ValueTask DisposeAsync() { if (sqlite != null) { await db.DisposeAsync(); await sqlite.DisposeAsync(); } }
    }
}
