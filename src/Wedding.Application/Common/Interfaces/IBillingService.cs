namespace Wedding.Application.Common.Interfaces;

public record PremiumOrderDto(Guid Id, bool IsPremium, decimal Price, string PaymentRef, string QrUrl, string Status, DateTime? ActivatedAt, DateTime ExpiresAt);
public record BankReceipt(string TransactionId, string Gateway, string AccountNumber, decimal Amount, string Content);
public record PaymentReceiptDto(Guid Id, string TransactionId, decimal Amount, string Content, string Status, string? UnmatchedReason, Guid? SettlementId, Guid? PremiumOrderId, DateTime CreatedAt);
public interface IBillingService
{
    Task<PremiumOrderDto> CreatePremiumOrderAsync(Guid customerId, CancellationToken ct);
    Task<PremiumOrderDto?> GetPremiumAsync(Guid customerId, CancellationToken ct);
    Task<PaymentReceiptDto> ReceiveAsync(BankReceipt receipt, Guid? manualSettlementId, Guid? actorId, string? note, CancellationToken ct);
    Task<PaymentReceiptDto> MatchAsync(Guid receiptId, Guid? settlementId, Guid? premiumOrderId, Guid actor, string note, CancellationToken ct);
}
