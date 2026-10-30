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
    public DbSet<BusinessGroup> BusinessGroups => Set<BusinessGroup>();
    public DbSet<VendorBusinessGroup> VendorBusinessGroups => Set<VendorBusinessGroup>();
    public DbSet<Listing> Listings => Set<Listing>();
    public DbSet<ListingRevision> ListingRevisions => Set<ListingRevision>();
    public DbSet<ListingMedia> ListingMedias => Set<ListingMedia>();
    public DbSet<Lead> Leads => Set<Lead>();
    public DbSet<Voucher> Vouchers => Set<Voucher>();
    public DbSet<BookingContract> BookingContracts => Set<BookingContract>();
    public DbSet<Commission> Commissions => Set<Commission>();
    public DbSet<CommissionPolicyRule> CommissionPolicyRules => Set<CommissionPolicyRule>();
    public DbSet<CommissionComponentBasisApproval> CommissionComponentBasisApprovals => Set<CommissionComponentBasisApproval>();
    public DbSet<CommissionAdjustmentRequest> CommissionAdjustmentRequests => Set<CommissionAdjustmentRequest>();
    public DbSet<Review> Reviews => Set<Review>();
    public DbSet<ReviewEditRequest> ReviewEditRequests => Set<ReviewEditRequest>();
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
    public DbSet<PasswordResetToken> PasswordResetTokens => Set<PasswordResetToken>();
    public DbSet<WeddingProfile> WeddingProfiles => Set<WeddingProfile>();
    public DbSet<AppointmentType> AppointmentTypes => Set<AppointmentType>();
    public DbSet<Appointment> Appointments => Set<Appointment>();
    public DbSet<ComboTemplate> ComboTemplates => Set<ComboTemplate>();
    public DbSet<ComboTemplateRevision> ComboTemplateRevisions => Set<ComboTemplateRevision>();
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
    public DbSet<PrivateServiceAgreement> PrivateServiceAgreements => Set<PrivateServiceAgreement>();

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
        foreach (var entry in ChangeTracker.Entries<BookingContract>().Where(entry => entry.State == EntityState.Modified))
        {
            var alreadyConfirmed = entry.OriginalValues.GetValue<DateTime?>(nameof(BookingContract.ConfirmedAt)).HasValue ||
                entry.OriginalValues.GetValue<Wedding.Domain.Enums.ContractStatus>(nameof(BookingContract.Status)) is
                    Wedding.Domain.Enums.ContractStatus.Confirmed or Wedding.Domain.Enums.ContractStatus.Completed or Wedding.Domain.Enums.ContractStatus.Disputed;
            var snapshotFields = new[] { nameof(BookingContract.CommissionRateSnapshot), nameof(BookingContract.CommissionCategoryIdSnapshot),
                nameof(BookingContract.CommissionCategoryNameSnapshot), nameof(BookingContract.CommissionBusinessGroupIdSnapshot),
                nameof(BookingContract.CommissionBusinessGroupNameSnapshot), nameof(BookingContract.CommissionRateSourceSnapshot),
                nameof(BookingContract.CommissionPolicyVersionSnapshot) };
            if (alreadyConfirmed && snapshotFields.Any(field => entry.Property(field).IsModified))
                throw new InvalidOperationException("Confirmed commission/category snapshots are immutable, including missing historical metadata; use a separately approved adjustment record instead of backfilling or overwriting.");
        }
        if (ChangeTracker.Entries<ListingRevision>().Any(entry => entry.State == EntityState.Deleted ||
            entry.State == EntityState.Modified && (entry.Property(revision => revision.ContentJson).IsModified ||
                entry.Property(revision => revision.ListingId).IsModified || entry.Property(revision => revision.Sequence).IsModified ||
                entry.Property(revision => revision.CreatedById).IsModified || entry.Property(revision => revision.CreatedAt).IsModified)))
            throw new InvalidOperationException("Listing content snapshots and their authors are immutable; append a new revision.");
        if (ChangeTracker.Entries<ComboTemplateRevision>().Any(entry => entry.State == EntityState.Deleted ||
            entry.State == EntityState.Modified && (entry.Property(revision => revision.SnapshotJson).IsModified ||
                entry.Property(revision => revision.ComboTemplateId).IsModified || entry.Property(revision => revision.Sequence).IsModified ||
                entry.Property(revision => revision.CreatedById).IsModified || entry.Property(revision => revision.CreatedByRole).IsModified ||
                entry.Property(revision => revision.CreatedAt).IsModified)))
            throw new InvalidOperationException("Combo proposal snapshots and their authors are immutable; create a new revision instead.");
        if (ChangeTracker.Entries<ComboOrder>().Any(entry => entry.State == EntityState.Modified &&
            new[] { nameof(ComboOrder.ComboTemplateId), nameof(ComboOrder.ComboTemplateRevisionId), nameof(ComboOrder.ComboTemplateSnapshotJson) }
                .Any(field => entry.Property(field).IsModified)))
            throw new InvalidOperationException("Combo order template version snapshots are immutable.");
        foreach (var entry in ChangeTracker.Entries<ReviewEditRequest>())
        {
            if (entry.State == EntityState.Deleted)
                throw new InvalidOperationException("Review edit history is append-only and must be retained.");
            if (entry.State != EntityState.Modified) continue;
            var allowed = new[] { nameof(ReviewEditRequest.Status), nameof(ReviewEditRequest.ReviewedById),
                nameof(ReviewEditRequest.ReviewedAt), nameof(ReviewEditRequest.RejectionReason), nameof(ReviewEditRequest.UpdatedAt) };
            if (entry.Properties.Any(property => property.IsModified && !allowed.Contains(property.Metadata.Name)) ||
                entry.OriginalValues.GetValue<Wedding.Domain.Enums.ReviewEditStatus>(nameof(ReviewEditRequest.Status)) != Wedding.Domain.Enums.ReviewEditStatus.Pending ||
                entry.Entity.Status is not (Wedding.Domain.Enums.ReviewEditStatus.Approved or Wedding.Domain.Enums.ReviewEditStatus.Rejected) ||
                !entry.Entity.ReviewedAt.HasValue || !entry.Entity.ReviewedById.HasValue)
                throw new InvalidOperationException("Review edit content and authorship are immutable; only pending moderation status may transition once.");
        }
        foreach (var entry in ChangeTracker.Entries<PrivateServiceAgreement>())
        {
            if (entry.State == EntityState.Deleted)
                throw new InvalidOperationException("Private service agreement history cannot be deleted.");
            if (entry.State != EntityState.Modified) continue;
            var responseFields = new[] { nameof(PrivateServiceAgreement.Status), nameof(PrivateServiceAgreement.Response),
                nameof(PrivateServiceAgreement.RespondedAt), nameof(PrivateServiceAgreement.RespondedById), nameof(PrivateServiceAgreement.UpdatedAt) };
            var previousStatus = entry.OriginalValues.GetValue<string>(nameof(PrivateServiceAgreement.Status));
            var row = entry.Entity;
            if (entry.Properties.Any(p => p.IsModified && !responseFields.Contains(p.Metadata.Name)) ||
                previousStatus != "PendingCustomer" || row.Status is not ("Accepted" or "Rejected" or "Withdrawn") ||
                row.RespondedAt == null || row.RespondedById != (row.Status == "Withdrawn" ? row.CreatedById : row.CustomerId))
                throw new InvalidOperationException("Private proposal content, original snapshot and final responses are immutable; append a new proposal.");
        }
        foreach (var entry in ChangeTracker.Entries<CommissionPolicyRule>())
        {
            if (entry.State == EntityState.Deleted)
                throw new InvalidOperationException("Commission policy history is append-only and cannot be deleted.");
            if (entry.State != EntityState.Modified) continue;
            var allowed = new[] { nameof(CommissionPolicyRule.Status), nameof(CommissionPolicyRule.SubmittedAt),
                nameof(CommissionPolicyRule.ApprovedById), nameof(CommissionPolicyRule.ApprovedAt),
                nameof(CommissionPolicyRule.RejectionReason), nameof(CommissionPolicyRule.Version), nameof(CommissionPolicyRule.UpdatedAt) };
            var previousStatus = entry.OriginalValues.GetValue<Wedding.Domain.Enums.CommissionPolicyStatus>(nameof(CommissionPolicyRule.Status));
            var rule = entry.Entity;
            var validTransition = previousStatus == Wedding.Domain.Enums.CommissionPolicyStatus.Draft &&
                    rule.Status == Wedding.Domain.Enums.CommissionPolicyStatus.Submitted && rule.SubmittedAt.HasValue ||
                previousStatus == Wedding.Domain.Enums.CommissionPolicyStatus.Submitted &&
                    rule.Status is Wedding.Domain.Enums.CommissionPolicyStatus.Approved or Wedding.Domain.Enums.CommissionPolicyStatus.Rejected &&
                    rule.ApprovedAt.HasValue && rule.ApprovedById.HasValue;
            if (entry.Properties.Any(property => property.IsModified && !allowed.Contains(property.Metadata.Name)) || !validTransition)
                throw new InvalidOperationException("Submitted commission policies are immutable; create a new policy version instead.");
        }
        foreach (var entry in ChangeTracker.Entries<CommissionComponentBasisApproval>())
        {
            if (entry.State == EntityState.Deleted)
                throw new InvalidOperationException("Commission component basis history is append-only and cannot be deleted.");
            if (entry.State != EntityState.Modified) continue;
            var allowed = new[] { nameof(CommissionComponentBasisApproval.Status), nameof(CommissionComponentBasisApproval.SubmittedAt),
                nameof(CommissionComponentBasisApproval.ApprovedById), nameof(CommissionComponentBasisApproval.ApprovedAt),
                nameof(CommissionComponentBasisApproval.RejectionReason), nameof(CommissionComponentBasisApproval.Version),
                nameof(CommissionComponentBasisApproval.UpdatedAt) };
            var previousStatus = entry.OriginalValues.GetValue<Wedding.Domain.Enums.CommissionPolicyStatus>(nameof(CommissionComponentBasisApproval.Status));
            var basis = entry.Entity;
            var validTransition = previousStatus == Wedding.Domain.Enums.CommissionPolicyStatus.Draft &&
                    basis.Status == Wedding.Domain.Enums.CommissionPolicyStatus.Submitted && basis.SubmittedAt.HasValue ||
                previousStatus == Wedding.Domain.Enums.CommissionPolicyStatus.Submitted &&
                    basis.Status is Wedding.Domain.Enums.CommissionPolicyStatus.Approved or Wedding.Domain.Enums.CommissionPolicyStatus.Rejected &&
                    basis.ApprovedAt.HasValue && basis.ApprovedById.HasValue;
            if (entry.Properties.Any(property => property.IsModified && !allowed.Contains(property.Metadata.Name)) || !validTransition)
                throw new InvalidOperationException("Submitted commission component basis is immutable; append a new approval sequence instead.");
        }
        foreach (var entry in ChangeTracker.Entries<CommissionAdjustmentRequest>())
        {
            if (entry.State == EntityState.Deleted)
                throw new InvalidOperationException("Commission adjustment decisions are append-only and cannot be deleted.");
            if (entry.State != EntityState.Modified) continue;
            var allowed = new[] { nameof(CommissionAdjustmentRequest.Status), nameof(CommissionAdjustmentRequest.DecidedById),
                nameof(CommissionAdjustmentRequest.DecidedAt), nameof(CommissionAdjustmentRequest.DecisionReason),
                nameof(CommissionAdjustmentRequest.Version), nameof(CommissionAdjustmentRequest.UpdatedAt) };
            var previousStatus = entry.OriginalValues.GetValue<Wedding.Domain.Enums.CommissionAdjustmentStatus>(nameof(CommissionAdjustmentRequest.Status));
            if (entry.Properties.Any(property => property.IsModified && !allowed.Contains(property.Metadata.Name)) ||
                previousStatus != Wedding.Domain.Enums.CommissionAdjustmentStatus.Submitted ||
                entry.Entity.Status is not (Wedding.Domain.Enums.CommissionAdjustmentStatus.Approved or Wedding.Domain.Enums.CommissionAdjustmentStatus.Rejected) ||
                !entry.Entity.DecidedAt.HasValue || !entry.Entity.DecidedById.HasValue)
                throw new InvalidOperationException("Commission adjustment request snapshots are immutable and can receive one final decision only.");
        }
        var auditedTypes = new[] { typeof(BookingContract), typeof(Commission), typeof(CommissionSettlement), typeof(CommissionPolicyRule), typeof(CommissionComponentBasisApproval), typeof(CommissionAdjustmentRequest), typeof(PaymentTransaction), typeof(AdvancePayment), typeof(VendorVerification), typeof(VendorTaxProfile), typeof(Dispute), typeof(Listing), typeof(Review), typeof(ReviewEditRequest) };
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
