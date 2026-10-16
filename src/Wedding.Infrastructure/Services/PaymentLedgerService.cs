using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Wedding.Application.Common.Interfaces;
using Wedding.Domain.Entities;
using Wedding.Domain.Enums;
using Wedding.Domain.Exceptions;
using Wedding.Infrastructure.Persistence;

namespace Wedding.Infrastructure.Services;

/// <summary>One durable receipt per bank transaction. Allocation and business updates commit atomically.</summary>
public class PaymentLedgerService : IBillingService
{
    private readonly ApplicationDbContext _db;
    private readonly IConfiguration _config;
    private readonly IAuditLogger _audit;
    public PaymentLedgerService(ApplicationDbContext db, IConfiguration config, IAuditLogger audit) { _db = db; _config = config; _audit = audit; }
    private static PaymentReceiptDto Map(PaymentTransaction p) => new(p.Id, p.TransactionId, p.Amount, p.Content, p.Status, p.UnmatchedReason, p.SettlementId, p.PremiumOrderId, p.CreatedAt);
    private string Qr(decimal amount, string reference)
    {
        var bank = _config["Payments:BankCode"]; var account = _config["Payments:AccountNumber"];
        if (string.IsNullOrWhiteSpace(bank) || string.IsNullOrWhiteSpace(account)) return "";
        return $"https://img.vietqr.io/image/{Uri.EscapeDataString(bank)}-{Uri.EscapeDataString(account)}-compact2.png?amount={amount:0}&addInfo={Uri.EscapeDataString(reference)}&accountName={Uri.EscapeDataString(_config["Payments:AccountName"] ?? "")}";
    }
    private PremiumOrderDto PremiumDto(PremiumOrder p, WeddingInvitation inv) => new(p.Id, inv.IsPremium, p.Amount, p.PaymentReference, Qr(p.Amount, p.PaymentReference), p.Status, inv.PremiumActivatedAt, p.ExpiresAt);
    public Task<PremiumOrderDto> CreatePremiumOrderAsync(Guid customerId, CancellationToken ct) => _db.ExecuteSerializableAsync(token => CreatePremiumCoreAsync(customerId, token), ct);
    private async Task<PremiumOrderDto> CreatePremiumCoreAsync(Guid customerId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_config["Payments:AccountNumber"]) || string.IsNullOrWhiteSpace(_config["Payments:BankCode"]))
            throw new DomainException("Thanh toán chưa được cấu hình tài khoản nhận tiền. Không tạo QR thử nghiệm.");
        var inv = await _db.WeddingInvitations.FirstOrDefaultAsync(x => x.CustomerId == customerId, ct) ?? throw new DomainException("Hãy tạo thiệp trước khi nâng cấp.");
        var now = DateTime.UtcNow;
        var order = await _db.PremiumOrders.OrderByDescending(x => x.CreatedAt).FirstOrDefaultAsync(x => x.CustomerId == customerId && (x.Status == "Paid" || x.Status == "Pending" && x.ExpiresAt > now), ct);
        if (order == null) { order = PremiumOrder.Create(customerId, inv.Id); _db.PremiumOrders.Add(order); await _db.SaveChangesAsync(ct); }
        return PremiumDto(order, inv);
    }
    public async Task<PremiumOrderDto?> GetPremiumAsync(Guid customerId, CancellationToken ct)
    {
        var inv = await _db.WeddingInvitations.AsNoTracking().FirstOrDefaultAsync(x => x.CustomerId == customerId, ct);
        var order = await _db.PremiumOrders.AsNoTracking().OrderByDescending(x => x.CreatedAt).FirstOrDefaultAsync(x => x.CustomerId == customerId, ct);
        return inv == null || order == null ? null : PremiumDto(order, inv);
    }
    public Task<PaymentReceiptDto> ReceiveAsync(BankReceipt r, Guid? manualSettlementId, Guid? actorId, string? note, CancellationToken ct) => _db.ExecuteSerializableAsync(token => ReceiveCoreAsync(r, manualSettlementId, actorId, note, token), ct);
    private async Task<PaymentReceiptDto> ReceiveCoreAsync(BankReceipt r, Guid? manualSettlementId, Guid? actorId, string? note, CancellationToken ct)
    {
        var existing = await _db.PaymentTransactions.FirstOrDefaultAsync(x => x.TransactionId == r.TransactionId.Trim(), ct);
        if (existing != null)
        {
            if (existing.Amount != r.Amount || existing.AccountNumber != r.AccountNumber || existing.Gateway != r.Gateway || existing.Content != r.Content)
                throw new DomainException("Mã giao dịch đã tồn tại nhưng dữ liệu không khớp; cần Finance kiểm tra.");
            return Map(existing);
        }
        var receipt = PaymentTransaction.Create(r.TransactionId, r.Gateway, r.AccountNumber, r.Amount, r.Content);
        _db.PaymentTransactions.Add(receipt);
        var normalized = string.Concat(r.Content.Where(c => !char.IsWhiteSpace(c))).ToUpperInvariant();
        CommissionSettlement? settlement = null;
        PremiumOrder? premium = null;
        if (manualSettlementId.HasValue) settlement = await _db.CommissionSettlements.FirstOrDefaultAsync(s => s.Id == manualSettlementId, ct);
        else
        {
            // Exact reference only. Ambiguous/substrings never automatically settle money.
            settlement = await _db.CommissionSettlements.FirstOrDefaultAsync(s => s.PaymentReference == normalized, ct);
            premium = await _db.PremiumOrders.FirstOrDefaultAsync(p => p.PaymentReference == normalized, ct);
        }
        await AllocateAsync(receipt, settlement, premium, actorId, note, ct);
        await _audit.LogAsync("Payment.Received", nameof(PaymentTransaction), receipt.Id.ToString(), new { receipt.TransactionId, receipt.Amount, receipt.Status, receipt.SettlementId, receipt.PremiumOrderId });
        await _db.SaveChangesAsync(ct);
        return Map(receipt);
    }
    public Task<PaymentReceiptDto> MatchAsync(Guid receiptId, Guid? settlementId, Guid? premiumOrderId, Guid actor, string note, CancellationToken ct) => _db.ExecuteSerializableAsync(token => MatchCoreAsync(receiptId, settlementId, premiumOrderId, actor, note, token), ct);
    private async Task<PaymentReceiptDto> MatchCoreAsync(Guid receiptId, Guid? settlementId, Guid? premiumOrderId, Guid actor, string note, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(note) || settlementId.HasValue == premiumOrderId.HasValue) throw new DomainException("Chọn đúng một đích đối soát và ghi lý do.");
        var receipt = await _db.PaymentTransactions.FirstOrDefaultAsync(x => x.Id == receiptId, ct) ?? throw new NotFoundException("Không tìm thấy giao dịch.");
        if (receipt.Status == "Matched") throw new DomainException("Giao dịch đã được xử lý.");
        var settlement = settlementId.HasValue ? await _db.CommissionSettlements.FirstOrDefaultAsync(s => s.Id == settlementId, ct) : null;
        var premium = premiumOrderId.HasValue ? await _db.PremiumOrders.FirstOrDefaultAsync(p => p.Id == premiumOrderId, ct) : null;
        await AllocateAsync(receipt, settlement, premium, actor, note, ct);
        if (receipt.Status != "Matched") throw new DomainException(receipt.UnmatchedReason ?? "Không thể khớp.");
        await _audit.LogAsync("Payment.ManuallyMatched", nameof(PaymentTransaction), receipt.Id.ToString(), new { settlementId, premiumOrderId, note });
        await _db.SaveChangesAsync(ct); return Map(receipt);
    }
    private async Task AllocateAsync(PaymentTransaction receipt, CommissionSettlement? settlement, PremiumOrder? premium, Guid? actor, string? note, CancellationToken ct)
    {
        if (settlement != null && premium != null) { receipt.Unmatched("Tham chiếu mơ hồ."); return; }
        if (premium != null)
        {
            if (premium.Status != "Pending" || receipt.Amount != premium.Amount || premium.ExpiresAt < receipt.CreatedAt)
            { receipt.Unmatched("Đơn Premium đã trả/hết hạn hoặc số tiền không đúng."); return; }
            var inv = await _db.WeddingInvitations.FirstOrDefaultAsync(i => i.Id == premium.InvitationId, ct);
            if (inv == null) { receipt.Unmatched("Thiệp không tồn tại."); return; }
            premium.MarkPaid(); inv.ActivatePremium(premium.PaymentReference); receipt.Match(null, premium.Id, actor, note); return;
        }
        if (settlement == null) { receipt.Unmatched("Không tìm thấy tham chiếu bảng kê hoặc đơn Premium chính xác."); return; }
        if (settlement.Status is not (SettlementStatus.Closed or SettlementStatus.Overdue) || receipt.Amount > settlement.TotalAmount - settlement.PaidAmount)
        { receipt.Unmatched("Bảng kê chưa chốt/đang tranh chấp/đã trả hoặc giao dịch vượt dư nợ."); return; }
        var commissions = await _db.Commissions.Include(c => c.Contract).Where(c => c.SettlementId == settlement.Id).OrderBy(c => c.CreatedAt).ToListAsync(ct);
        if (commissions.Any(c => c.Status == CommissionStatus.Frozen || c.Contract.Status == ContractStatus.Disputed))
        { receipt.Unmatched("Hợp đồng đang tranh chấp; đóng băng thu hoa hồng."); return; }
        var remaining = receipt.Amount;
        foreach (var c in commissions.Where(c => c.Status is CommissionStatus.InSettlement or CommissionStatus.Overdue))
        {
            var applied = Math.Min(remaining, c.OutstandingAmount);
            if (applied > 0) { c.ApplyPayment(applied, receipt.TransactionId); remaining -= applied; }
            if (remaining == 0) break;
        }
        if (remaining > 0) throw new DomainException("Dư nợ chi tiết không khớp bảng kê; cần Finance xử lý trước.");
        settlement.MarkPaid(receipt.Amount); receipt.Match(settlement.Id, null, actor, note);
    }
}
