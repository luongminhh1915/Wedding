using Microsoft.EntityFrameworkCore;
using Wedding.Application.Common.Interfaces;
using Wedding.Domain.Entities;
using Wedding.Domain.Enums;
using Wedding.Domain.Exceptions;

namespace Wedding.Infrastructure.Services;

/// <summary>
/// P0: Lưu thông báo tạm ứng hoa hồng vào DB (bảng AdvancePayments) thay cho ConcurrentDictionary + file JSON,
/// đồng thời ghi AuditLog khi Finance/SuperAdmin duyệt hoặc từ chối.
/// </summary>
public class AdvancePaymentService : IAdvancePaymentService
{
    private readonly IApplicationDbContext _context;
    private readonly IAuditLogger _audit;
    private readonly ICurrentUserService _currentUser;

    public AdvancePaymentService(IApplicationDbContext context, IAuditLogger audit, ICurrentUserService currentUser)
    {
        _context = context;
        _audit = audit;
        _currentUser = currentUser;
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
    {
        // Enforce access here as well as at the HTTP boundary: settlement queries also
        // use this service, and must never load another shop's banking data first.
        var userId = _currentUser.UserId;
        if (!userId.HasValue || userId == Guid.Empty)
            throw new DomainException("Bạn cần đăng nhập để xem thông báo chuyển tiền.");

        var actor = await _context.Users.AsNoTracking()
            .Where(user => user.Id == userId.Value && user.IsActive)
            .Select(user => new { user.Id, user.Role })
            .SingleOrDefaultAsync();
        if (actor == null || actor.Role.ToString() != _currentUser.Role ||
            actor.Role is not (UserRole.VendorOwner or UserRole.Finance or UserRole.SuperAdmin))
            throw new DomainException("Bạn không có quyền xem thông báo chuyển tiền.");

        var notices = _context.AdvancePayments.AsNoTracking();
        if (actor.Role == UserRole.VendorOwner)
        {
            var vendorId = await _context.Vendors.AsNoTracking()
                .Where(vendor => vendor.UserId == actor.Id && vendor.IsActive)
                .Select(vendor => (Guid?)vendor.Id)
                .SingleOrDefaultAsync();
            if (!vendorId.HasValue)
                throw new DomainException("Hồ sơ nhà cung cấp đang hoạt động không tồn tại.");
            notices = notices.Where(notice => notice.VendorId == vendorId.Value);
        }

        return (await notices.OrderByDescending(notice => notice.CreatedAt).ThenByDescending(notice => notice.Id)
            .ToListAsync()).Select(Map).ToList();
    }

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
