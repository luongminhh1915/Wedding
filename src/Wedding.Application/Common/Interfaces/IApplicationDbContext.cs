using Microsoft.EntityFrameworkCore;
using Wedding.Domain.Entities;

namespace Wedding.Application.Common.Interfaces;

public interface IApplicationDbContext
{
    DbSet<User> Users { get; }
    DbSet<Vendor> Vendors { get; }
    DbSet<Category> Categories { get; }
    DbSet<Listing> Listings { get; }
    DbSet<ListingMedia> ListingMedias { get; }
    DbSet<Lead> Leads { get; }
    DbSet<Voucher> Vouchers { get; }
    DbSet<BookingContract> BookingContracts { get; }
    DbSet<Commission> Commissions { get; }
    DbSet<Review> Reviews { get; }
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
    DbSet<WeddingProfile> WeddingProfiles { get; }
    DbSet<AppointmentType> AppointmentTypes { get; }
    DbSet<Appointment> Appointments { get; }
    DbSet<ComboTemplate> ComboTemplates { get; }
    DbSet<ComboTemplateItem> ComboTemplateItems { get; }
    DbSet<ComboOrder> ComboOrders { get; }
    DbSet<ComboOrderItem> ComboOrderItems { get; }
    DbSet<Quote> Quotes { get; }
    DbSet<CommissionSettlement> CommissionSettlements { get; }
    DbSet<VendorVerification> VendorVerifications { get; }
    DbSet<Dispute> Disputes { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
