using Microsoft.EntityFrameworkCore;
using System.Reflection;
using Wedding.Application.Common.Interfaces;
using Wedding.Domain.Entities;

namespace Wedding.Infrastructure.Persistence;

public class ApplicationDbContext : DbContext, IApplicationDbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<Vendor> Vendors => Set<Vendor>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Listing> Listings => Set<Listing>();
    public DbSet<ListingMedia> ListingMedias => Set<ListingMedia>();
    public DbSet<Lead> Leads => Set<Lead>();
    public DbSet<Voucher> Vouchers => Set<Voucher>();
    public DbSet<BookingContract> BookingContracts => Set<BookingContract>();
    public DbSet<Commission> Commissions => Set<Commission>();
    public DbSet<Review> Reviews => Set<Review>();
    public DbSet<Article> Articles => Set<Article>();
    public DbSet<WeddingInvitation> WeddingInvitations => Set<WeddingInvitation>();
    public DbSet<GuestRSVP> GuestRSVPs => Set<GuestRSVP>();
    public DbSet<BudgetItem> BudgetItems => Set<BudgetItem>();
    public DbSet<ChecklistTask> ChecklistTasks => Set<ChecklistTask>();
    public DbSet<ChatMessage> ChatMessages => Set<ChatMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        // Tự động load toàn bộ IEntityTypeConfiguration từ Infrastructure assembly
        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return await base.SaveChangesAsync(cancellationToken);
    }
}
