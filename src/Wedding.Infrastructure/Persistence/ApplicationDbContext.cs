using Microsoft.EntityFrameworkCore;
using System.Reflection;
using System.Data;
using Wedding.Domain.Exceptions;
using Wedding.Application.Common.Interfaces;
using Wedding.Domain.Entities;

namespace Wedding.Infrastructure.Persistence;

public class ApplicationDbContext : DbContext, IApplicationDbContext
{
    private readonly ICurrentUserService? currentUser;
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options, ICurrentUserService? currentUser = null)
        : base(options)
    {
        this.currentUser = currentUser;
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
    // Weddy journey (P0–P6)
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<AdvancePayment> AdvancePayments => Set<AdvancePayment>();
    public DbSet<OtpCode> OtpCodes => Set<OtpCode>();
    public DbSet<WeddingProfile> WeddingProfiles => Set<WeddingProfile>();
    public DbSet<AppointmentType> AppointmentTypes => Set<AppointmentType>();
    public DbSet<Appointment> Appointments => Set<Appointment>();
    public DbSet<ComboTemplate> ComboTemplates => Set<ComboTemplate>();
    public DbSet<ComboTemplateItem> ComboTemplateItems => Set<ComboTemplateItem>();
    public DbSet<ComboOrder> ComboOrders => Set<ComboOrder>();
    public DbSet<ComboOrderItem> ComboOrderItems => Set<ComboOrderItem>();
    public DbSet<Quote> Quotes => Set<Quote>();
    public DbSet<CommissionSettlement> CommissionSettlements => Set<CommissionSettlement>();
    public DbSet<VendorVerification> VendorVerifications => Set<VendorVerification>();
    public DbSet<Dispute> Disputes => Set<Dispute>();
    public DbSet<WeddingMember> WeddingMembers => Set<WeddingMember>();
    public DbSet<WeddingJoinInvitation> WeddingJoinInvitations => Set<WeddingJoinInvitation>();
    public DbSet<WeddingGuest> WeddingGuests => Set<WeddingGuest>();
    public DbSet<SeatTable> SeatTables => Set<SeatTable>();
    public DbSet<AppointmentAvailabilityException> AppointmentAvailabilityExceptions => Set<AppointmentAvailabilityException>();
    public DbSet<AppointmentEvent> AppointmentEvents => Set<AppointmentEvent>();
    public DbSet<ServiceItem> ServiceItems => Set<ServiceItem>();
    public DbSet<ItemVariant> ItemVariants => Set<ItemVariant>();
    public DbSet<ItemAddOn> ItemAddOns => Set<ItemAddOn>();
    public DbSet<BundleDiscountRule> BundleDiscountRules => Set<BundleDiscountRule>();
    public DbSet<PaymentTransaction> PaymentTransactions => Set<PaymentTransaction>();
    public DbSet<PremiumOrder> PremiumOrders => Set<PremiumOrder>();
    public DbSet<CommissionCreditNote> CommissionCreditNotes => Set<CommissionCreditNote>();
    public DbSet<EInvoiceRecord> EInvoiceRecords => Set<EInvoiceRecord>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<VendorTaxProfile> VendorTaxProfiles => Set<VendorTaxProfile>();
    public DbSet<ArticleListingPin> ArticleListingPins => Set<ArticleListingPin>();
    public DbSet<EmergencyMatch> EmergencyMatches => Set<EmergencyMatch>();
    public DbSet<InvitationTemplate> InvitationTemplates => Set<InvitationTemplate>();
    public DbSet<CustomerOrderState> CustomerOrderStates => Set<CustomerOrderState>();
    public DbSet<OrderChangeRequest> OrderChangeRequests => Set<OrderChangeRequest>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        // Tự động load toàn bộ IEntityTypeConfiguration từ Infrastructure assembly
        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
        modelBuilder.Entity<BookingContract>().Property(x => x.Version).IsConcurrencyToken();
        modelBuilder.Entity<CommissionSettlement>().Property(x => x.Version).IsConcurrencyToken();
        modelBuilder.Entity<BudgetItem>().HasIndex(x => x.ContractId).IsUnique().HasFilter("[ContractId] IS NOT NULL");
        modelBuilder.Entity<ChecklistTask>().HasIndex(x => x.ContractId).IsUnique().HasFilter("[ContractId] IS NOT NULL");
        modelBuilder.Entity<Vendor>().Property(x => x.MaxComboDiscount).HasPrecision(8, 5);
        modelBuilder.Entity<ComboTemplate>().Property(x => x.SingleVendorDiscount).HasPrecision(8, 5);
        modelBuilder.Entity<WeddingProfile>().Property(x => x.EventsJson).HasDefaultValue("[]");
        modelBuilder.Entity<WeddingProfile>().Property(x => x.PrioritiesJson).HasDefaultValue("[]");
        modelBuilder.Entity<WeddingProfile>().Property(x => x.PlanningRole).HasDefaultValue("Couple");
        modelBuilder.Entity<WeddingInvitation>().Property(x => x.ContentJson).HasDefaultValue("{}");
        modelBuilder.Entity<WeddingInvitation>().Property(x => x.Lang).HasDefaultValue("vi");
        modelBuilder.Entity<BudgetItem>().Property(x => x.Owner).HasDefaultValue("Shared");
        modelBuilder.Entity<ChecklistTask>().Property(x => x.Owner).HasDefaultValue("Shared");
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        if (ChangeTracker.Entries<AuditLog>().Any(e => e.State is EntityState.Modified or EntityState.Deleted))
            throw new InvalidOperationException("Audit logs are append-only and must be retained for at least 24 months.");
        var auditedTypes = new[] { typeof(BookingContract), typeof(Commission), typeof(CommissionSettlement), typeof(PaymentTransaction), typeof(AdvancePayment), typeof(VendorVerification), typeof(VendorTaxProfile), typeof(Dispute), typeof(Listing), typeof(Review) };
        var changes = ChangeTracker.Entries().Where(e => auditedTypes.Contains(e.Entity.GetType()) && e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted).ToArray();
        foreach (var change in changes)
            AuditLogs.Add(AuditLog.Create(currentUser?.UserId, currentUser?.Role ?? "System", $"Persist.{change.State}", change.Entity.GetType().Name,
                change.Property("Id").CurrentValue?.ToString(), System.Text.Json.JsonSerializer.Serialize(new { fields = change.Properties.Where(p => p.IsModified || change.State == EntityState.Added).Select(p => p.Metadata.Name).ToArray() })));
        return await base.SaveChangesAsync(cancellationToken);
    }

    public async Task<T> ExecuteSerializableAsync<T>(Func<CancellationToken, Task<T>> action, CancellationToken cancellationToken = default)
    {
        if (Database.CurrentTransaction != null) return await action(cancellationToken);
        // One short database-owned lock covers all inventory/ledger writers, including multiple API instances.
        // Never retry a delegate after it has changed tracked entities; the caller can safely retry a failed request.
        await using var transaction = await Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        try
        {
            if (Database.IsSqlServer())
                await Database.ExecuteSqlRawAsync("DECLARE @result int; EXEC @result = sys.sp_getapplock @Resource=N'Wedding.AtomicWrite', @LockMode='Exclusive', @LockOwner='Transaction', @LockTimeout=10000; IF @result < 0 THROW 51000, 'Busy, please retry.', 1;", cancellationToken);
            var result = await action(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return result;
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            ChangeTracker.Clear();
            throw;
        }
    }
}
