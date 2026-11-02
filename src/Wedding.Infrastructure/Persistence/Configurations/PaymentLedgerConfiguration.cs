using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wedding.Domain.Entities;

namespace Wedding.Infrastructure.Persistence.Configurations;

public class PaymentTransactionConfiguration : IEntityTypeConfiguration<PaymentTransaction>
{
    public void Configure(EntityTypeBuilder<PaymentTransaction> b) { b.HasIndex(x => x.TransactionId).IsUnique(); b.Property(x => x.TransactionId).HasMaxLength(150); b.Property(x => x.Amount).HasPrecision(18, 2); b.Property(x => x.Content).HasMaxLength(2000); b.Property(x => x.AccountNumber).HasMaxLength(100); b.Property(x => x.Status).HasMaxLength(30); }
}
public class PremiumOrderConfiguration : IEntityTypeConfiguration<PremiumOrder>
{
    public void Configure(EntityTypeBuilder<PremiumOrder> b) { b.HasIndex(x => x.PaymentReference).IsUnique(); b.Property(x => x.PaymentReference).HasMaxLength(80); b.Property(x => x.Amount).HasPrecision(18, 2); b.Property(x => x.Status).HasMaxLength(30); b.HasIndex(x => new { x.CustomerId, x.Status }); }
}
public class CommissionCreditNoteConfiguration : IEntityTypeConfiguration<CommissionCreditNote>
{
    public void Configure(EntityTypeBuilder<CommissionCreditNote> b) { b.HasIndex(x => x.ContractId).IsUnique(); b.Property(x => x.Amount).HasPrecision(18, 2); b.Property(x => x.AppliedAmount).HasPrecision(18, 2); }
}
public class EInvoiceRecordConfiguration : IEntityTypeConfiguration<EInvoiceRecord>
{
    public void Configure(EntityTypeBuilder<EInvoiceRecord> b) { b.HasIndex(x => x.SettlementId).IsUnique(); b.Property(x => x.VatRate).HasPrecision(5, 4); b.Property(x => x.InvoiceNumber).HasMaxLength(150); }
}
public class FinancialContractConfiguration : IEntityTypeConfiguration<BookingContract>
{
    public void Configure(EntityTypeBuilder<BookingContract> b) { b.Property(x => x.Version).IsConcurrencyToken(); b.Property(x => x.CancellationPolicySnapshot).HasMaxLength(4000); b.Property(x => x.AppointmentDepositCredit).HasPrecision(18, 2); }
}
public class FinancialSettlementConfiguration : IEntityTypeConfiguration<CommissionSettlement>
{
    public void Configure(EntityTypeBuilder<CommissionSettlement> b) { b.Property(x => x.Version).IsConcurrencyToken(); b.Property(x => x.CreditAmount).HasPrecision(18, 2); b.HasIndex(x => x.PaymentReference).IsUnique(); }
}
public class FinancialQuoteConfiguration : IEntityTypeConfiguration<Quote>
{
    public void Configure(EntityTypeBuilder<Quote> b) { b.Property(x => x.Subtotal).HasPrecision(18, 2); b.Property(x => x.VatAmount).HasPrecision(18, 2); b.Property(x => x.VatRate).HasPrecision(5, 4); }
}
