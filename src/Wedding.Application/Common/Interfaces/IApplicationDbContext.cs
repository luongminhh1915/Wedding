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

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
