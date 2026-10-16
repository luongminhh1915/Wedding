using Microsoft.EntityFrameworkCore;
using Wedding.Application.Common.Interfaces;
using Wedding.Domain.Entities;

namespace Wedding.Infrastructure.Services;

/// <summary>
/// P0: Lưu thông báo tạm ứng hoa hồng vào DB (bảng AdvancePayments) thay cho ConcurrentDictionary + file JSON,
/// đồng thời ghi AuditLog khi Finance/SuperAdmin duyệt hoặc từ chối.
/// </summary>
public class AdvancePaymentService : IAdvancePaymentService
{
    private readonly IApplicationDbContext _context;
    private readonly IAuditLogger _audit;

    public AdvancePaymentService(IApplicationDbContext context, IAuditLogger audit)
    {
        _context = context;
        _audit = audit;
    }

    private static AdvancePaymentNoticeDto Map(AdvancePayment p) => new(
        p.Id, p.ContractId, p.ContractCode, p.VendorId, p.VendorBrandName, p.Amount, p.Reason, p.BankName,
        p.BankAccountNumber, p.BankAccountName, p.Note, p.Status, p.CreatedAt, p.ProcessedAt, p.AdminNote);

    public async Task<AdvancePaymentNoticeDto> CreateRequestAsync(Guid contractId, string contractCode, Guid vendorId,
        string vendorBrandName, decimal amount, string reason, string? bankName = null, string? bankAccountNumber = null,
        string? bankAccountName = null, string? note = null)
    {
        var entity = AdvancePayment.Create(contractId, contractCode, vendorId, vendorBrandName, amount, reason,
            bankName, bankAccountNumber, bankAccountName, note);
        _context.AdvancePayments.Add(entity);
        await _context.SaveChangesAsync();
        return Map(entity);
    }

    public async Task<List<AdvancePaymentNoticeDto>> GetAllAsync()
        => (await _context.AdvancePayments.AsNoTracking().ToListAsync()).OrderByDescending(p => p.CreatedAt).Select(Map).ToList();

    public async Task<List<AdvancePaymentNoticeDto>> GetByContractIdAsync(Guid contractId)
        => (await _context.AdvancePayments.AsNoTracking().Where(p => p.ContractId == contractId).ToListAsync())
            .OrderByDescending(p => p.CreatedAt).Select(Map).ToList();

    public async Task<AdvancePaymentNoticeDto?> GetPendingByContractIdAsync(Guid contractId)
        => (await GetByContractIdAsync(contractId)).FirstOrDefault(p => p.Status == "PendingApproval");

    public async Task<AdvancePaymentNoticeDto?> GetByIdAsync(Guid id)
    {
        var p = await _context.AdvancePayments.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        return p == null ? null : Map(p);
    }

    public Task<AdvancePaymentNoticeDto?> ApproveAsync(Guid id, string? adminNote = null)
        => ProcessAsync(id, true, adminNote ?? "Admin đã xác nhận nhận tiền thành công.");

    public Task<AdvancePaymentNoticeDto?> RejectAsync(Guid id, string? reason = null)
        => ProcessAsync(id, false, reason ?? "Admin đã từ chối/báo chưa nhận được tiền.");

    private async Task<AdvancePaymentNoticeDto?> ProcessAsync(Guid id, bool approved, string note)
    {
        var p = await _context.AdvancePayments.FirstOrDefaultAsync(x => x.Id == id);
        if (p == null) return null;
        p.Process(approved, note);
        await _audit.LogAsync(approved ? "AdvancePayment.Approve" : "AdvancePayment.Reject", nameof(AdvancePayment), id.ToString(),
            new { p.Amount, p.ContractCode, note });
        await _context.SaveChangesAsync();
        return Map(p);
    }
}
