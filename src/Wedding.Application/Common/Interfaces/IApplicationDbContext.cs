using Microsoft.EntityFrameworkCore;
using Wedding.Domain.Entities;

namespace Wedding.Application.Common.Interfaces;

public interface IApplicationDbContext
{
    DbSet<User> Users { get; }
    DbSet<Vendor> Vendors { get; }
    DbSet<Category> Categories { get; }
    DbSet<BusinessGroup> BusinessGroups { get; }
    DbSet<VendorBusinessGroup> VendorBusinessGroups { get; }
    DbSet<Listing> Listings { get; }
    DbSet<ListingRevision> ListingRevisions { get; }
    DbSet<ListingMedia> ListingMedias { get; }
    DbSet<Lead> Leads { get; }
    DbSet<Voucher> Vouchers { get; }
    DbSet<BookingContract> BookingContracts { get; }
    DbSet<Commission> Commissions { get; }
    DbSet<CommissionPolicyRule> CommissionPolicyRules { get; }
    DbSet<CommissionComponentBasisApproval> CommissionComponentBasisApprovals { get; }
    DbSet<CommissionAdjustmentRequest> CommissionAdjustmentRequests { get; }
    DbSet<Review> Reviews { get; }
    DbSet<ReviewEditRequest> ReviewEditRequests { get; }
    DbSet<Article> Articles { get; }
    DbSet<WeddingInvitation> WeddingInvitations { get; }
    DbSet<GuestRSVP> GuestRSVPs { get; }
    DbSet<BudgetItem> BudgetItems { get; }
    DbSet<ChecklistTask> ChecklistTasks { get; }
    DbSet<ChatMessage> ChatMessages { get; }
    // Weddy journey (P0–P6)
    DbSet<AuditLog> AuditLogs { get; }
    DbSet<AdvancePayment> AdvancePayments { get; }
    DbSet<OtpCode> OtpCodes { get; }
    DbSet<PasswordResetToken> PasswordResetTokens { get; }
    DbSet<WeddingProfile> WeddingProfiles { get; }
    DbSet<AppointmentType> AppointmentTypes { get; }
    DbSet<Appointment> Appointments { get; }
    DbSet<ComboTemplate> ComboTemplates { get; }
    DbSet<ComboTemplateRevision> ComboTemplateRevisions { get; }
    DbSet<ComboTemplateItem> ComboTemplateItems { get; }
    DbSet<ComboOrder> ComboOrders { get; }
    DbSet<ComboOrderItem> ComboOrderItems { get; }
    DbSet<Quote> Quotes { get; }
    DbSet<CommissionSettlement> CommissionSettlements { get; }
    DbSet<VendorVerification> VendorVerifications { get; }
    DbSet<Dispute> Disputes { get; }
    DbSet<WeddingMember> WeddingMembers { get; }
    DbSet<WeddingJoinInvitation> WeddingJoinInvitations { get; }
    DbSet<WeddingGuest> WeddingGuests { get; }
    DbSet<SeatTable> SeatTables { get; }
    DbSet<AppointmentAvailabilityException> AppointmentAvailabilityExceptions { get; }
    DbSet<AppointmentEvent> AppointmentEvents { get; }
    DbSet<ServiceItem> ServiceItems { get; }
    DbSet<ItemVariant> ItemVariants { get; }
    DbSet<ItemAddOn> ItemAddOns { get; }
    DbSet<BundleDiscountRule> BundleDiscountRules { get; }
    DbSet<PaymentTransaction> PaymentTransactions { get; }
    DbSet<PremiumOrder> PremiumOrders { get; }
    DbSet<CommissionCreditNote> CommissionCreditNotes { get; }
    DbSet<EInvoiceRecord> EInvoiceRecords { get; }
    DbSet<Notification> Notifications { get; }
    DbSet<VendorTaxProfile> VendorTaxProfiles { get; }
    DbSet<ArticleListingPin> ArticleListingPins { get; }
    DbSet<EmergencyMatch> EmergencyMatches { get; }
    DbSet<InvitationTemplate> InvitationTemplates { get; }
    DbSet<CustomerOrderState> CustomerOrderStates { get; }
    DbSet<OrderChangeRequest> OrderChangeRequests { get; }
    DbSet<PrivateServiceAgreement> PrivateServiceAgreements { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    Task<T> ExecuteSerializableAsync<T>(Func<CancellationToken, Task<T>> action, CancellationToken cancellationToken = default);
}
