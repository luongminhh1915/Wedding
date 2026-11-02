using Microsoft.EntityFrameworkCore;
using Wedding.Application.Common.Interfaces;
using Wedding.Domain.Entities;
using Wedding.Domain.Enums;
using Wedding.Domain.Exceptions;

namespace Wedding.Application.Features.Contracts;

public static class FinancialContractService
{
    /// <summary>Finance verifies the actual retained deposit. Does not initiate a refund or move money.</summary>
    public static async Task CancelAsync(IApplicationDbContext db, BookingContract contract, decimal retainedDeposit, string reason, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(reason) || retainedDeposit < 0 || retainedDeposit > contract.DepositAmount) throw new DomainException("Cần lý do và số cọc NCC thực giữ hợp lệ.");
        if (contract.Status is not (ContractStatus.Confirmed or ContractStatus.Disputed)) throw new DomainException("Chỉ hủy tài chính cho hợp đồng hiệu lực/tranh chấp.");
        if (contract.Status != ContractStatus.Disputed && (!contract.WeddingDate.HasValue || DateTime.UtcNow >= contract.WeddingDate.Value.Date)) throw new DomainException("Sau ngày cưới cần xử lý qua tranh chấp.");
        var commissions = await db.Commissions.Where(c => c.ContractId == contract.Id).ToListAsync(ct);
        if (commissions.Any(c => c.SettlementId.HasValue && c.Status is not (CommissionStatus.Paid or CommissionStatus.Settled or CommissionStatus.Cancelled)))
            throw new DomainException("HĐ có khoản trong bảng kê chưa thu. Finance cần xử lý tranh chấp bảng kê trước; không tự sửa bảng kê đã chốt.");
        var paid = commissions.Sum(c => c.EffectivePaidAmount);
        var liability = Math.Round(retainedDeposit * (contract.CommissionRateSnapshot ?? throw new DomainException("HĐ cũ chưa có snapshot tỷ lệ, cần kiểm tra thủ công.")), 0, MidpointRounding.AwayFromZero);
        foreach (var c in commissions) c.Cancel();
        var first = commissions.FirstOrDefault(c => c.Period == CommissionPeriod.Period1_Deposit);
        if (first != null) first.SetCancellationLiability(liability);
        if (paid > liability && !await db.CommissionCreditNotes.AnyAsync(c => c.ContractId == contract.Id, ct)) db.CommissionCreditNotes.Add(CommissionCreditNote.Create(contract.Id, contract.VendorId, paid - liability, reason));
        contract.Cancel(reason);
        var budget = await db.BudgetItems.FirstOrDefaultAsync(b => b.ContractId == contract.Id, ct);
        if (budget != null) budget.SynchronizeContract(retainedDeposit, retainedDeposit);
    }
}
