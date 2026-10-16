using MediatR;
using Microsoft.EntityFrameworkCore;
using Wedding.Application.Common.Interfaces;
using Wedding.Domain.Entities;
using Wedding.Domain.Enums;
using Wedding.Domain.Exceptions;

namespace Wedding.Application.Features.Settlements;

public record SettlementDto(Guid Id, Guid VendorId, string VendorName, string Period, int CommissionCount, decimal TotalAmount,
    decimal PaidAmount, string Status, DateTime DueDate, string PaymentReference, DateTime? PaidAt, string? InvoiceNumber, string QrUrl);

internal static class SettlementMap
{
    public static SettlementDto ToDto(CommissionSettlement s, IMockPaymentGateway pay) => new(s.Id, s.VendorId, s.Vendor?.BrandName ?? "", s.Period,
        s.CommissionCount, s.TotalAmount, s.PaidAmount, s.Status.ToString(), s.DueDate, s.PaymentReference, s.PaidAt, s.InvoiceNumber,
        pay.BuildVietQrUrl(Math.Max(0, s.TotalAmount - s.PaidAmount), s.PaymentReference));
}

/// <summary>P4 – Chốt bảng kê kỳ yyyy-MM (BR-007: hạn ngày 25). Gom các khoản hoa hồng Pending/Overdue có hạn trong kỳ.</summary>
public record RunSettlementCommand(string Period) : IRequest<List<SettlementDto>>;

public class RunSettlementCommandHandler : IRequestHandler<RunSettlementCommand, List<SettlementDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly IMockPaymentGateway _pay;
    private readonly IAuditLogger _audit;
    public RunSettlementCommandHandler(IApplicationDbContext context, IMockPaymentGateway pay, IAuditLogger audit) { _context = context; _pay = pay; _audit = audit; }

    public async Task<List<SettlementDto>> Handle(RunSettlementCommand r, CancellationToken ct)
    {
        if (!DateTime.TryParseExact(r.Period + "-01", "yyyy-MM-dd", null, System.Globalization.DateTimeStyles.None, out var start))
            throw new DomainException("Kỳ đối soát phải có dạng yyyy-MM.");
        var end = start.AddMonths(1);
        var commissions = await _context.Commissions
            .Where(c => c.DueDate >= start && c.DueDate < end && (c.Status == CommissionStatus.Pending || c.Status == CommissionStatus.Overdue))
            .ToListAsync(ct);
        var existing = await _context.CommissionSettlements.Where(s => s.Period == r.Period).ToListAsync(ct);
        foreach (var g in commissions.GroupBy(c => c.VendorId))
        {
            var s = existing.FirstOrDefault(x => x.VendorId == g.Key);
            if (s == null)
            {
                s = CommissionSettlement.Create(g.Key, r.Period, g.Count(), g.Sum(c => c.CommissionAmount), new DateTime(start.Year, start.Month, 25, 0, 0, 0, DateTimeKind.Utc));
                _context.CommissionSettlements.Add(s);
            }
            else s.Refresh(g.Count(), g.Sum(c => c.CommissionAmount));
            s.Close();
        }
        await _audit.LogAsync("Settlement.Run", nameof(CommissionSettlement), r.Period, new { vendors = commissions.Select(c => c.VendorId).Distinct().Count() });
        await _context.SaveChangesAsync(ct);
        return (await _context.CommissionSettlements.AsNoTracking().Include(s => s.Vendor).Where(s => s.Period == r.Period).ToListAsync(ct))
            .Select(s => SettlementMap.ToDto(s, _pay)).ToList();
    }
}

public record GetSettlementsQuery(string? Period) : IRequest<List<SettlementDto>>;

public class GetSettlementsQueryHandler : IRequestHandler<GetSettlementsQuery, List<SettlementDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _user;
    private readonly IMockPaymentGateway _pay;
    public GetSettlementsQueryHandler(IApplicationDbContext context, ICurrentUserService user, IMockPaymentGateway pay) { _context = context; _user = user; _pay = pay; }

    public async Task<List<SettlementDto>> Handle(GetSettlementsQuery r, CancellationToken ct)
    {
        var q = _context.CommissionSettlements.AsNoTracking().Include(s => s.Vendor).AsQueryable();
        if (_user.Role == nameof(UserRole.VendorOwner)) q = q.Where(s => s.Vendor.UserId == _user.UserId);
        if (!string.IsNullOrWhiteSpace(r.Period)) q = q.Where(s => s.Period == r.Period);
        foreach (var s in await _context.CommissionSettlements.Where(s => s.Status == SettlementStatus.Closed).ToListAsync(ct)) s.MarkOverdue();
        await _context.SaveChangesAsync(ct);
        return (await q.ToListAsync(ct)).OrderByDescending(s => s.Period).ThenBy(s => s.Vendor.BrandName).Select(s => SettlementMap.ToDto(s, _pay)).ToList();
    }
}

/// <summary>P4 – Ghi nhận thanh toán bảng kê (mock webhook ngân hàng/SePay + mock xuất hóa đơn điện tử).</summary>
public record PaySettlementCommand(Guid Id, decimal? Amount) : IRequest<SettlementDto>;

public class PaySettlementCommandHandler : IRequestHandler<PaySettlementCommand, SettlementDto>
{
    private readonly IApplicationDbContext _context;
    private readonly IMockPaymentGateway _pay;
    private readonly IAuditLogger _audit;
    public PaySettlementCommandHandler(IApplicationDbContext context, IMockPaymentGateway pay, IAuditLogger audit) { _context = context; _pay = pay; _audit = audit; }

    public async Task<SettlementDto> Handle(PaySettlementCommand r, CancellationToken ct)
    {
        var s = await _context.CommissionSettlements.Include(x => x.Vendor).FirstOrDefaultAsync(x => x.Id == r.Id, ct) ?? throw new NotFoundException("Bảng kê không tồn tại.");
        if (s.Status == SettlementStatus.Paid || s.TotalAmount - s.PaidAmount <= 0) throw new DomainException("Bảng kê đã được thanh toán.");
        if (s.Status == SettlementStatus.Open) throw new DomainException("Bảng kê chưa được chốt.");
        var amount = r.Amount ?? (s.TotalAmount - s.PaidAmount);
        if (amount <= 0) throw new DomainException("Số tiền không hợp lệ.");
        s.MarkPaid(amount);
        if (s.Status == SettlementStatus.Paid)
        {
            var start = DateTime.ParseExact(s.Period + "-01", "yyyy-MM-dd", null);
            var items = await _context.Commissions.Where(c => c.VendorId == s.VendorId && c.DueDate >= start && c.DueDate < start.AddMonths(1)
                && (c.Status == CommissionStatus.Pending || c.Status == CommissionStatus.Overdue)).ToListAsync(ct);
            foreach (var c in items) c.MarkPaid(s.PaymentReference);
        }
        await _audit.LogAsync("Settlement.Payment", nameof(CommissionSettlement), s.Id.ToString(), new { amount, s.PaymentReference, mock = true });
        await _context.SaveChangesAsync(ct);
        return SettlementMap.ToDto(s, _pay);
    }
}
