using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Wedding.Application.Common.Interfaces;
using Wedding.Application.Features.Appointments;
using Wedding.Application.Features.Combos;
using Wedding.Application.Features.Contracts.Commands.ConfirmContract;
using Wedding.Application.Features.Contracts.Commands.CreateContractDraft;
using Wedding.Application.Features.CustomerOrders;
using Wedding.Application.Features.Quotes;
using Wedding.Domain.Entities;
using Wedding.Domain.Enums;
using Wedding.Domain.Exceptions;
using Wedding.Infrastructure.Persistence;
using Wedding.Infrastructure.Services;

namespace Wedding.Tests;

public class CustomerOrderLifecycleTests
{
    private const string Reason = "Muốn thay đổi kế hoạch và lựa chọn dịch vụ.";

    [Fact]
    public async Task ArchiveIsCustomerOnlyPresentationAndRestoreRetainsRecordsAndMoney()
    {
        await using var f = await Fixture.Create(); var order = await f.Submit();
        var lead = await f.Db.Leads.SingleAsync(); var contract = f.Contract(lead); contract.ConfirmByCustomer(); await f.Db.SaveChangesAsync();
        var archived = await f.Action("contract", contract.Id, "archive");
        Assert.True(archived.IsArchived); Assert.True(archived.Capabilities.CanRestore);
        Assert.Equal(ContractStatus.Confirmed, (await f.Db.BookingContracts.SingleAsync()).Status);
        Assert.Equal(LeadStatus.New, (await f.Db.Leads.SingleAsync()).Status); Assert.Single(await f.Db.BudgetItems.ToListAsync());
        Assert.False((await f.Action("contract", contract.Id, "restore")).IsArchived);
        var intruder = new Current(f.Other.Id, "Customer");
        await Assert.ThrowsAsync<NotFoundException>(() => new CustomerOrderActionCommandHandler(f.Db, intruder).Handle(new("combo", order.Id, "archive"), default));
        Assert.Empty(await CustomerOrderJourney.LoadAsync(f.Db, f.Other.Id, default));
    }

    [Fact]
    public async Task RevisionDraftRetainsSourceUntilValidSubmitAndInheritsProvenanceOnResend()
    {
        await using var f = await Fixture.Create(); await RevisionScenario(f);
    }

    private static async Task RevisionScenario(Fixture f)
    {
        var original = await f.Submit(); var oldLead = await f.Db.Leads.SingleAsync();
        var quote = f.Quote(oldLead); await f.Db.SaveChangesAsync();
        var originalBudget = (await f.Db.BudgetItems.SingleAsync()).PlannedCost;
        var draft = await f.Submit(f.Input with { SaveAsDraft = true, RevisionOfId = original.Id, RevisionReason = Reason });
        Assert.Equal("Draft", draft.Status); Assert.Equal(original.Id, draft.RevisionOfId);
        Assert.Equal(LeadStatus.New, (await f.Db.Leads.SingleAsync()).Status);
        Assert.Equal(QuoteStatus.Sent, (await f.Db.Quotes.SingleAsync()).Status);
        Assert.Equal(ComboOrderStatus.Submitted, (await f.Db.ComboOrders.SingleAsync(o => o.Id == original.Id)).Status);
        Assert.Equal(originalBudget, (await f.Db.BudgetItems.SingleAsync()).PlannedCost);
        Assert.Null((await f.Journey("combo", original.Id)).SupersededById);
        // A stale price cannot destroy the source or draft. The transaction rolls back all effects.
        await Assert.ThrowsAsync<DomainException>(() => f.Submit(f.Input with { DraftId = draft.Id, ExpectedTotal = -1 }));
        Assert.Equal(LeadStatus.New, (await f.Db.Leads.SingleAsync()).Status);
        Assert.Equal(ComboOrderStatus.Draft, (await f.Db.ComboOrders.SingleAsync(o => o.Id == draft.Id)).Status);
        // Frontend need not resend revision ids: the saved draft is authoritative.
        var replacement = await f.Submit(f.Input with { DraftId = draft.Id });
        Assert.Equal(original.Id, replacement.RevisionOfId); Assert.Equal(Reason, replacement.RevisionReason);
        Assert.Equal(ComboOrderStatus.Cancelled, (await f.Db.ComboOrders.SingleAsync(o => o.Id == original.Id)).Status);
        Assert.Equal(LeadStatus.Cancelled, (await f.Db.Leads.SingleAsync(l => l.Id == oldLead.Id)).Status);
        Assert.Equal(QuoteStatus.Superseded, (await f.Db.Quotes.SingleAsync()).Status);
        Assert.Equal(replacement.Id, (await f.Journey("combo", original.Id)).SupersededById);
        Assert.Equal(replacement.Id, (await f.Journey("combo", draft.Id)).SupersededById);
        Assert.Equal(2, await f.Db.Leads.CountAsync()); Assert.Single(await f.Db.BudgetItems.ToListAsync());
        await Assert.ThrowsAsync<DomainException>(() => new RespondQuoteCommandHandler(f.Db, f.CustomerUser).Handle(new(quote.Id, true, null), default));
        Assert.False((await f.Journey("quote", quote.Id)).Capabilities.CanRespondQuote);
        Assert.Equal(0, await f.Db.BookingContracts.CountAsync()); Assert.Equal(0, await f.Db.PaymentTransactions.CountAsync());
        await Assert.ThrowsAsync<DomainException>(() => f.Submit(f.Input with { RevisionOfId = original.Id, RevisionReason = Reason }));
    }

    [Fact]
    public async Task RevisionPreviewReleasesOnlyUntouchedAutomaticBudgetWithoutChangingDatabase()
    {
        await using var f = await Fixture.Create(); var original = await f.Submit();
        var row = await f.Db.BudgetItems.SingleAsync(); var oldCost = row.PlannedCost;
        var normal = await ComboPricing.CalculateAsync(f.Db, f.PriceInput, f.Customer.Id, default);
        var revision = await ComboPricing.CalculateAsync(f.Db, f.PriceInput with { RevisionOfId = original.Id }, f.Customer.Id, default);
        Assert.Equal(normal.PlannerReservedBeforeCart - oldCost, revision.PlannerReservedBeforeCart);
        Assert.Equal(oldCost, row.PlannedCost); Assert.Single(await f.Db.BudgetItems.ToListAsync());
        row.UpdateCosts(1500000, 200000, "Customer manually edited", false); await f.Db.SaveChangesAsync();
        var protectedPreview = await ComboPricing.CalculateAsync(f.Db, f.PriceInput with { RevisionOfId = original.Id }, f.Customer.Id, default);
        Assert.Equal(1500000, protectedPreview.PlannerReservedBeforeCart); Assert.Contains(protectedPreview.Warnings, w => w.Contains("được giữ nguyên"));
        await f.Submit(f.Input with { RevisionOfId = original.Id, RevisionReason = Reason });
        Assert.Equal(2, await f.Db.BudgetItems.CountAsync()); Assert.Equal(200000, (await f.Db.BudgetItems.SingleAsync(b => b.Id == row.Id)).ActualCost);
    }

    [Fact]
    public async Task DraftCannotChangeItsRevisionSourceAndCrossVendorRevisionFails()
    {
        await using var f = await Fixture.Create(); var original = await f.Submit();
        var second = await f.Submit(); var draft = await f.Submit(f.Input with { RevisionOfId = original.Id, RevisionReason = Reason, SaveAsDraft = true });
        await Assert.ThrowsAsync<DomainException>(() => f.Submit(f.Input with { DraftId = draft.Id, RevisionOfId = second.Id, RevisionReason = Reason }));
        await Assert.ThrowsAsync<NotFoundException>(() => new SubmitComboOrderCommandHandler(f.Db, new Current(f.Other.Id, "Customer"), f.Notify).Handle(f.Input with { RevisionOfId = original.Id, RevisionReason = Reason }, default));
        await Assert.ThrowsAsync<DomainException>(() => ComboPricing.CalculateAsync(f.Db, f.PriceInput with { Selections = new() { new(f.ForeignItem.Id, null, null) }, RevisionOfId = original.Id }, f.Customer.Id, default));
        Assert.Equal(ComboOrderStatus.Submitted, (await f.Db.ComboOrders.SingleAsync(o => o.Id == original.Id)).Status);
    }

    [Fact]
    public async Task WithdrawalInvalidatesQuotesVouchersAndUnpaidMeetingsAndNotifiesVendor()
    {
        await using var f = await Fixture.Create(); var order = await f.Submit(); var lead = await f.Db.Leads.SingleAsync(); var quote = f.Quote(lead);
        var meeting = f.Meeting(lead); await f.Db.SaveChangesAsync();
        await f.Action("combo", order.Id, "withdraw");
        Assert.Equal(LeadStatus.Cancelled, (await f.Db.Leads.SingleAsync()).Status); Assert.Equal(QuoteStatus.Superseded, (await f.Db.Quotes.SingleAsync()).Status);
        Assert.Equal(VoucherStatus.Expired, (await f.Db.Vouchers.SingleAsync()).Status); Assert.Equal(AppointmentStatus.Cancelled, (await f.Db.Appointments.SingleAsync()).Status);
        Assert.Empty(await f.Db.BudgetItems.ToListAsync()); Assert.Contains(await f.Db.Notifications.ToListAsync(), n => n.UserId == f.Owner.Id);
        await Assert.ThrowsAsync<DomainException>(() => new RespondQuoteCommandHandler(f.Db, f.CustomerUser).Handle(new(quote.Id, true, null), default));
        await Assert.ThrowsAsync<DomainException>(() => new CreateContractDraftCommandHandler(f.Db, f.VendorUser).Handle(new(lead.Id, null, 1000000, 100000, null, f.Date), default));
        await Assert.ThrowsAsync<DomainException>(() => new BookAppointmentCommandHandler(f.Db, f.CustomerUser, f.Notify).Handle(new(meeting.AppointmentTypeId, f.Date.AddDays(-5), 2, null, lead.Id), default));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ActualPendingOrDraftContractBlocksComboReplacementAndWithdrawal(bool draft)
    {
        await using var f = await Fixture.Create(); var order = await f.Submit(); var lead = await f.Db.Leads.SingleAsync();
        var contract = f.Contract(lead); if (draft) contract.RejectByCustomer(Reason); await f.Db.SaveChangesAsync();
        var meta = await f.Journey("combo", order.Id); Assert.False(meta.Capabilities.CanEdit); Assert.False(meta.Capabilities.CanWithdraw);
        await Assert.ThrowsAsync<DomainException>(() => f.Action("combo", order.Id, "withdraw"));
        await Assert.ThrowsAsync<DomainException>(() => f.Submit(f.Input with { RevisionOfId = order.Id, RevisionReason = Reason }));
        Assert.True((await f.Journey("contract", contract.Id)).Capabilities.CanStopPending);
        await f.Action("contract", contract.Id, "stop-pending");
        Assert.Equal(ContractStatus.Cancelled, (await f.Db.BookingContracts.SingleAsync()).Status);
        Assert.False((await f.Journey("contract", contract.Id)).Capabilities.CanConfirm);
        await Assert.ThrowsAsync<DomainException>(() => new ConfirmContractCommandHandler(f.Db, f.CustomerUser).Handle(new(contract.Id), default));
        Assert.Contains(await f.Db.Notifications.ToListAsync(), n => n.UserId == f.Owner.Id);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PaidOrReportedAppointmentBlocksWithdrawalReplacementAndPendingContractStop(bool paid)
    {
        await using var f = await Fixture.Create(); var order = await f.Submit(); var lead = await f.Db.Leads.SingleAsync();
        var appointment = f.Meeting(lead, 100000); if (paid) appointment.MarkDepositPaid(true); else appointment.ReportDeposit();
        var contract = f.Contract(lead); await f.Db.SaveChangesAsync();
        Assert.False((await f.Journey("combo", order.Id)).Capabilities.CanWithdraw);
        Assert.False((await f.Journey("contract", contract.Id)).Capabilities.CanStopPending);
        await Assert.ThrowsAsync<DomainException>(() => f.Action("combo", order.Id, "withdraw"));
        await Assert.ThrowsAsync<DomainException>(() => f.Action("contract", contract.Id, "stop-pending"));
        await Assert.ThrowsAsync<DomainException>(() => f.Submit(f.Input with { RevisionOfId = order.Id, RevisionReason = Reason, SaveAsDraft = true }));
        Assert.Equal(ContractStatus.PendingVerification, (await f.Db.BookingContracts.SingleAsync()).Status);
    }

    [Fact]
    public async Task StandaloneLeadRevisionReplacesOnlyAfterValidationAndKeepsOldConversationHistory()
    {
        await using var f = await Fixture.Create(); var lead = f.Standalone(); var quote = f.Quote(lead); await f.Db.SaveChangesAsync();
        await Assert.ThrowsAsync<DomainException>(() => new ReviseCustomerLeadCommandHandler(f.Db, f.CustomerUser, f.Notify).Handle(new(lead.Id, Reason, f.ForeignItem.ListingId), default));
        Assert.Equal(LeadStatus.New, (await f.Db.Leads.SingleAsync()).Status); Assert.Equal(QuoteStatus.Sent, (await f.Db.Quotes.SingleAsync()).Status);
        var replacement = await new ReviseCustomerLeadCommandHandler(f.Db, f.CustomerUser, f.Notify).Handle(new(lead.Id, Reason, EstimatedGuests: 201, Notes: "Updated notes"), default);
        Assert.Equal(lead.Id, replacement.RevisionOfId); Assert.Equal(replacement.Id, (await f.Journey("lead", lead.Id)).SupersededById);
        Assert.Equal(LeadStatus.Cancelled, (await f.Db.Leads.SingleAsync(l => l.Id == lead.Id)).Status); Assert.Equal(QuoteStatus.Superseded, (await f.Db.Quotes.SingleAsync()).Status);
        Assert.Equal(201, (await f.Db.Leads.SingleAsync(l => l.Id == replacement.Id)).EstimatedGuests); Assert.Contains(f.Notify.Messages, n => n.Recipient == f.Owner.Id.ToString());
    }

    [Fact]
    public async Task ComboChildCannotBeRevisedOrWithdrawnIndependently()
    {
        await using var f = await Fixture.Create(); await f.Submit(); var lead = await f.Db.Leads.SingleAsync();
        Assert.False((await f.Journey("lead", lead.Id)).Capabilities.CanEdit);
        await Assert.ThrowsAsync<DomainException>(() => f.Action("lead", lead.Id, "withdraw"));
        await Assert.ThrowsAsync<DomainException>(() => new ReviseCustomerLeadCommandHandler(f.Db, f.CustomerUser, f.Notify).Handle(new(lead.Id, Reason), default));
    }

    [Fact]
    public async Task ConfirmedCancellationRequiresVendorThenFinanceAndNeverChangesContractOrMoney()
    {
        await using var f = await Fixture.Create(); await AcknowledgementScenario(f);
    }

    private static async Task AcknowledgementScenario(Fixture f)
    {
        var lead = f.Standalone(); var contract = f.Contract(lead); contract.ConfirmByCustomer(); await f.Db.SaveChangesAsync();
        var command = new CreateOrderChangeRequestCommand("contract", contract.Id, "Cancellation", Reason, "Please discuss revised terms");
        var request = await new CreateOrderChangeRequestCommandHandler(f.Db, f.CustomerUser, f.Notify).Handle(command, default);
        Assert.True(request.RequiresFinance); Assert.Equal("PendingVendor", request.Status);
        await Assert.ThrowsAsync<DomainException>(() => f.Action("contract", contract.Id, "stop-pending"));
        await Assert.ThrowsAsync<DomainException>(() => new CreateOrderChangeRequestCommandHandler(f.Db, f.CustomerUser, f.Notify).Handle(command, default));
        await Assert.ThrowsAsync<DomainException>(() => new ReviewOrderChangeRequestCommandHandler(f.Db, f.FinanceUser, f.Notify).Handle(new(request.Id, true, true, Reason), default));
        await Assert.ThrowsAsync<DomainException>(() => new ReviewOrderChangeRequestCommandHandler(f.Db, new Current(f.OtherOwner.Id, "VendorOwner"), f.Notify).Handle(new(request.Id, false, true, Reason), default));
        var accepted = await new ReviewOrderChangeRequestCommandHandler(f.Db, f.VendorUser, f.Notify).Handle(new(request.Id, false, true, Reason), default);
        Assert.Equal("PendingFinance", accepted.Status);
        await Assert.ThrowsAsync<DomainException>(() => new ReviewOrderChangeRequestCommandHandler(f.Db, f.CustomerUser, f.Notify).Handle(new(request.Id, true, true, Reason), default));
        var final = await new ReviewOrderChangeRequestCommandHandler(f.Db, f.FinanceUser, f.Notify).Handle(new(request.Id, true, true, Reason), default);
        Assert.Equal("Acknowledged", final.Status); Assert.NotNull(final.FinanceReviewedAt); Assert.NotNull(final.VendorReviewedAt);
        Assert.Equal(ContractStatus.Confirmed, (await f.Db.BookingContracts.SingleAsync(c => c.Id == contract.Id)).Status);
        Assert.Empty(await f.Db.PaymentTransactions.ToListAsync()); Assert.Empty(await f.Db.CommissionCreditNotes.ToListAsync());
        Assert.Empty(await new GetOrderChangeRequestsQueryHandler(f.Db, new Current(f.OtherOwner.Id, "VendorOwner")).Handle(new(false), default));
        Assert.Single(await new GetOrderChangeRequestsQueryHandler(f.Db, f.FinanceUser).Handle(new(true), default));
        Assert.Contains(f.Notify.Messages, n => n.Recipient == f.Finance.Id.ToString());
    }

    [Fact]
    public async Task RejectedRequestIsTerminalAndMinReasonIsEnforcedWithoutMutation()
    {
        await using var f = await Fixture.Create(); var lead = f.Standalone(); var contract = f.Contract(lead); await f.Db.SaveChangesAsync();
        foreach (var reason in new[] { "", "too short", new string('x', 2001) })
        {
            await Assert.ThrowsAsync<DomainException>(() => f.Action("contract", contract.Id, "stop-pending", reason));
            await Assert.ThrowsAsync<DomainException>(() => new CreateOrderChangeRequestCommandHandler(f.Db, f.CustomerUser, f.Notify).Handle(new("contract", contract.Id, "Change", reason), default));
        }
        var request = await new CreateOrderChangeRequestCommandHandler(f.Db, f.CustomerUser, f.Notify).Handle(new("contract", contract.Id, "Change", Reason), default);
        var rejected = await new ReviewOrderChangeRequestCommandHandler(f.Db, f.VendorUser, f.Notify).Handle(new(request.Id, false, false, Reason), default);
        Assert.Equal("Rejected", rejected.Status);
        await Assert.ThrowsAsync<DomainException>(() => new ReviewOrderChangeRequestCommandHandler(f.Db, f.VendorUser, f.Notify).Handle(new(request.Id, false, true, Reason), default));
        Assert.Equal(ContractStatus.PendingVerification, (await f.Db.BookingContracts.SingleAsync()).Status);
        Assert.True((await f.Journey("contract", contract.Id)).Capabilities.CanConfirm);
    }

    [Fact]
    public async Task JourneyMediaUsesListingImagesNotDocumentsOrPrivateVendorFields()
    {
        await using var f = await Fixture.Create(); var order = await f.Submit();
        f.Db.ListingMedias.AddRange(ListingMedia.Create(f.Item.ListingId, "https://images.test/real.jpg", "https://images.test/thumb.jpg", isFeatured: true),
            ListingMedia.Create(f.Item.ListingId, "https://private.test/document.pdf", null, mediaType: "document")); await f.Db.SaveChangesAsync();
        var meta = await f.Journey("combo", order.Id); Assert.Equal("https://images.test/thumb.jpg", meta.Media.ImageUrl);
        Assert.Equal(new[] { "https://images.test/real.jpg" }, meta.Media.GalleryUrls); Assert.Single(meta.Media.Items);
        Assert.DoesNotContain("private.test", System.Text.Json.JsonSerializer.Serialize(meta));
    }

    [Fact]
    public async Task VendorAcceptanceEscalatesWhenMoneyArrivesAfterRequestCreation()
    {
        await using var f = await Fixture.Create(); var lead = f.Standalone(); var contract = f.Contract(lead); await f.Db.SaveChangesAsync();
        var request = await new CreateOrderChangeRequestCommandHandler(f.Db, f.CustomerUser, f.Notify).Handle(new("contract", contract.Id, "Change", Reason), default);
        Assert.False(request.RequiresFinance);
        var meeting = f.Meeting(lead, 100000); meeting.ReportDeposit(); await f.Db.SaveChangesAsync();
        var reviewed = await new ReviewOrderChangeRequestCommandHandler(f.Db, f.VendorUser, f.Notify).Handle(new(request.Id, false, true, Reason), default);
        Assert.True(reviewed.RequiresFinance); Assert.Equal("PendingFinance", reviewed.Status);
        Assert.Equal(ContractStatus.PendingVerification, (await f.Db.BookingContracts.SingleAsync()).Status);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ExpiryWorkersKeepMoneyBearingPendingContractsAndTheirBudget(bool paid)
    {
        await using var f = await Fixture.Create(); var order = await f.Submit(); var lead = await f.Db.Leads.SingleAsync(); var contract = f.Contract(lead);
        var meeting = f.Meeting(lead, 100000); if (paid) meeting.MarkDepositPaid(true); else meeting.ReportDeposit();
        f.Db.Entry(contract).Property(c => c.VerificationDeadline).CurrentValue = DateTime.UtcNow.AddDays(-1);
        var combo = await f.Db.ComboOrders.SingleAsync(); f.Db.Entry(combo).Property(c => c.PriceLockUntil).CurrentValue = DateTime.UtcNow.AddDays(-1); await f.Db.SaveChangesAsync();
        Assert.Equal(0, await ContractVerificationBackgroundService.ExpirePendingAsync(f.Db, DateTime.UtcNow, default));
        Assert.Equal(0, await ContractVerificationBackgroundService.ExpirePendingAsync(f.Db, DateTime.UtcNow, default));
        Assert.Equal(1, await AppointmentHousekeepingBackgroundService.ExpireComboLocksAsync(f.Db, DateTime.UtcNow, default));
        Assert.Equal(ContractStatus.PendingVerification, (await f.Db.BookingContracts.SingleAsync()).Status);
        Assert.Equal(1000000, (await f.Db.BudgetItems.SingleAsync()).PlannedCost); Assert.Null((await f.Db.Appointments.SingleAsync()).RefundedAt);
        Assert.Single(await f.Db.AuditLogs.Where(a => a.Action == "Contract.ExpiryNeedsFinance").ToListAsync());
        Assert.Equal(3, await f.Db.Notifications.CountAsync()); // Customer, owning vendor and Finance, once only.
    }

    [Fact]
    public async Task ExpiryWithoutContractOrDepositCanReleaseOnlyAutomaticBudget()
    {
        await using var f = await Fixture.Create(); await f.Submit(); var lead = await f.Db.Leads.SingleAsync(); var contract = f.Contract(lead);
        f.Db.Entry(contract).Property(c => c.VerificationDeadline).CurrentValue = DateTime.UtcNow.AddDays(-1); var combo = await f.Db.ComboOrders.SingleAsync();
        f.Db.Entry(combo).Property(c => c.PriceLockUntil).CurrentValue = DateTime.UtcNow.AddDays(-1); await f.Db.SaveChangesAsync();
        Assert.Equal(1, await ContractVerificationBackgroundService.ExpirePendingAsync(f.Db, DateTime.UtcNow, default));
        Assert.Equal(ContractStatus.Cancelled, (await f.Db.BookingContracts.SingleAsync()).Status);
        Assert.Equal(1, await AppointmentHousekeepingBackgroundService.ExpireComboLocksAsync(f.Db, DateTime.UtcNow, default)); Assert.Empty(await f.Db.BudgetItems.ToListAsync());
    }

    [Fact]
    public async Task MaximumValidRevisionReasonIsPreservedWithoutOverflowingAppointmentNotes()
    {
        await using var f = await Fixture.Create(); var lead = f.Standalone(); var meeting = f.Meeting(lead); await f.Db.SaveChangesAsync();
        var reason = new string('x', 2000);
        var revised = await new ReviseCustomerLeadCommandHandler(f.Db, f.CustomerUser, f.Notify).Handle(new(lead.Id, reason), default);
        Assert.Equal(reason, revised.RevisionReason); Assert.Equal(2000, revised.RevisionReason!.Length);
        Assert.True((await f.Db.AppointmentEvents.SingleAsync()).Note!.Length <= 2000); Assert.True((await f.Db.Appointments.SingleAsync()).VendorNote!.Length <= 2000);
    }

    [SqlServerFact]
    public async Task SqlServerLifecycleMigrationTransactionsAndAcknowledgementsMatchSqlite()
    {
        await using var f = await Fixture.Create(sql: true); Assert.False(f.Db.Database.HasPendingModelChanges());
        await RevisionScenario(f); await AcknowledgementScenario(f);
        var latest = (await f.Db.ComboOrders.Where(o => o.Status == ComboOrderStatus.Submitted).ToListAsync()).Single();
        Assert.True((await f.Action("combo", latest.Id, "archive")).IsArchived);
        Assert.False((await f.Action("combo", latest.Id, "restore")).IsArchived);
    }

    private sealed record Current(Guid Id, string CurrentRole) : ICurrentUserService { public Guid? UserId => Id; public string? Email => null; public string? Role => CurrentRole; }
    private sealed class Notify : INotificationSender
    {
        public List<(string Recipient, string Message)> Messages { get; } = new();
        public Task SendAsync(string channel, string recipient, string message, CancellationToken cancellationToken = default) { Messages.Add((recipient, message)); return Task.CompletedTask; }
    }
    private sealed class Fixture : IAsyncDisposable
    {
        public ApplicationDbContext Db = null!; public SqliteConnection? Connection; private string? databaseName;
        public User Customer = null!, Owner = null!, Other = null!, OtherOwner = null!, Finance = null!; public Vendor Vendor = null!; public ServiceItem Item = null!, ForeignItem = null!;
        public Notify Notify = new(); public DateTime Date => DateTime.UtcNow.AddDays(30).Date;
        public Current CustomerUser => new(Customer.Id, "Customer"); public Current VendorUser => new(Owner.Id, "VendorOwner"); public Current FinanceUser => new(Finance.Id, "Finance");
        public ComboPriceCommand PriceInput => new(null, "HCM", Date, 100, new() { new(Item.Id, null, null) });
        public SubmitComboOrderCommand Input => new(null, new(), Date, 100, Customer.PhoneNumber, "Original plan", "HCM", PriceInput.Selections);
        public Task<ComboOrderDto> Submit(SubmitComboOrderCommand? input = null) => new SubmitComboOrderCommandHandler(Db, CustomerUser, Notify).Handle(input ?? Input, default);
        public Task<OrderJourneyItemDto> Action(string kind, Guid id, string action, string reason = Reason) => new CustomerOrderActionCommandHandler(Db, CustomerUser).Handle(new(kind, id, action, reason), default);
        public async Task<OrderJourneyItemDto> Journey(string kind, Guid id) => (await CustomerOrderJourney.LoadAsync(Db, Customer.Id, default)).Single(x => x.Kind == kind && x.Id == id);
        public Lead Standalone() { var l = Lead.Create(Customer.Id, Vendor.Id, Item.ListingId, Customer.PhoneNumber, Date, 100, 1000000, "Original consultation"); Db.Leads.Add(l); return l; }
        public Quote Quote(Lead lead) { var q = Wedding.Domain.Entities.Quote.Create(lead.Id, Vendor.Id, Customer.Id, "[]", 1000000, 100000, "Terms", 7, 1); Db.Quotes.Add(q); return q; }
        public BookingContract Contract(Lead lead) { var c = BookingContract.Create(lead.Id, null, Vendor.Id, Customer.Id, 1000000, 100000, null, Date); Db.BookingContracts.Add(c); return c; }
        public Appointment Meeting(Lead lead, decimal deposit = 0)
        {
            var type = AppointmentType.Create(Vendor.Id, "Consultation", null, 60, 10, "0,1,2,3,4,5,6", TimeSpan.Zero, new(23, 59, 0), deposit, true); Db.AppointmentTypes.Add(type);
            var a = Appointment.Create(type, Customer.Id, Date.AddDays(-5), 2, null, lead.Id); Db.Appointments.Add(a); return a;
        }
        public static async Task<Fixture> Create(bool sql = false)
        {
            var f = new Fixture(); var options = new DbContextOptionsBuilder<ApplicationDbContext>();
            if (sql) { f.databaseName = $"WeddingPlanTest_Lifecycle_{Guid.NewGuid():N}"; options.UseSqlServer($"Server=localhost;Database={f.databaseName};Integrated Security=true;TrustServerCertificate=true;Connection Timeout=5"); }
            else { f.Connection = new SqliteConnection("Data Source=:memory:"); await f.Connection.OpenAsync(); options.UseSqlite(f.Connection); }
            f.Db = new ApplicationDbContext(options.Options);
            if (sql) await f.Db.Database.MigrateAsync(); else await f.Db.Database.EnsureCreatedAsync();
            f.Customer = User.Create("Customer", "customer@lifecycle.test", "0900000000", "hash", UserRole.Customer); f.Owner = User.Create("Owner", "owner@lifecycle.test", "0900000001", "hash", UserRole.VendorOwner);
            f.Other = User.Create("Other customer", "other@lifecycle.test", "0900000002", "hash", UserRole.Customer); f.OtherOwner = User.Create("Other owner", "otherowner@lifecycle.test", "0900000003", "hash", UserRole.VendorOwner);
            f.Finance = User.Create("Finance", "finance@lifecycle.test", "0900000004", "hash", UserRole.Finance); f.Db.Users.AddRange(f.Customer, f.Owner, f.Other, f.OtherOwner, f.Finance);
            f.Vendor = Vendor.Create(f.Owner.Id, "One Vendor", "lifecycle-vendor", "Owner", f.Owner.PhoneNumber, "HCM", .08m); var foreignVendor = Vendor.Create(f.OtherOwner.Id, "Foreign Vendor", "foreign-vendor", "Foreign", f.OtherOwner.PhoneNumber, "HCM", .08m);
            foreach (var v in new[] { f.Vendor, foreignVendor }) { v.SetVerified(true); v.ConfigureComboParticipation(true, .08m); f.Db.Vendors.Add(v); }
            var category = await f.Db.Categories.SingleAsync(c => c.Slug == "tiem-cuoi-venues");
            ServiceItem Add(Vendor v, string title)
            {
                var listing = Listing.Create(v.Id, category.Id, title, title.ToLowerInvariant().Replace(' ', '-'), 1000000, 1000000, "Real service description", "HCM"); listing.Approve(); f.Db.Listings.Add(listing);
                var item = ServiceItem.Create(v.Id, listing.Id, category.Id, title, 1000000, "Package", 1, 1000, "HCM", "Policy"); item.Submit(); item.Review(true, null); f.Db.ServiceItems.Add(item); return item;
            }
            f.Item = Add(f.Vendor, "Venue"); f.ForeignItem = Add(foreignVendor, "Foreign Venue");
            var profile = WeddingProfile.Create(f.Customer.Id); profile.Update("Groom", "Bride", f.Date, "HCM", 10000000, 100, "Classic", null); f.Db.WeddingProfiles.Add(profile);
            await f.Db.SaveChangesAsync(); return f;
        }
        public async ValueTask DisposeAsync()
        {
            if (databaseName != null) { Assert.StartsWith("WeddingPlanTest_Lifecycle_", databaseName); await Db.Database.EnsureDeletedAsync(); }
            await Db.DisposeAsync(); if (Connection != null) await Connection.DisposeAsync();
        }
    }
}
