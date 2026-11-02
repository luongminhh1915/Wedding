using System.Globalization;
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
        s.Status is SettlementStatus.Closed or SettlementStatus.Overdue ? pay.BuildVietQrUrl(Math.Max(0, s.TotalAmount - s.PaidAmount), s.PaymentReference) : "");
}

public record RunSettlementCommand(string Period) : IRequest<List<SettlementDto>>;
public class RunSettlementCommandHandler : IRequestHandler<RunSettlementCommand, List<SettlementDto>>
{
    private readonly IApplicationDbContext _db;
    private readonly IMockPaymentGateway _pay;
    private readonly IAuditLogger _audit;
    public RunSettlementCommandHandler(IApplicationDbContext db, IMockPaymentGateway pay, IAuditLogger audit) { _db = db; _pay = pay; _audit = audit; }
    public Task<List<SettlementDto>> Handle(RunSettlementCommand r, CancellationToken ct) => _db.ExecuteSerializableAsync(token => RunAsync(r, token), ct);
    private async Task<List<SettlementDto>> RunAsync(RunSettlementCommand r, CancellationToken ct)
    {
        if (!DateTime.TryParseExact(r.Period + "-01", "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var start)) throw new DomainException("Kỳ đối soát phải có dạng yyyy-MM.");
        var cutoff = new DateTime(start.Year, start.Month, 25, 0, 0, 0, DateTimeKind.Utc).AddHours(-7);
        if (DateTime.UtcNow < cutoff) throw new DomainException("Chỉ chốt bảng kê từ ngày 25 theo múi giờ Việt Nam.");
        var end = DateTime.SpecifyKind(start.AddMonths(1), DateTimeKind.Utc).AddHours(-7);
        var commissions = await _db.Commissions.Include(c => c.Contract).Where(c => c.SettlementId == null && c.CreatedAt < cutoff && c.DueDate < end &&
            (c.Status == CommissionStatus.Pending || c.Status == CommissionStatus.Unsettled || c.Status == CommissionStatus.Overdue) && c.Contract.Status != ContractStatus.Disputed).ToListAsync(ct);
        var existing = await _db.CommissionSettlements.Where(s => s.Period == r.Period).ToListAsync(ct);
        foreach (var group in commissions.GroupBy(c => c.VendorId))
        {
            // Closed statements are immutable. Late items carry into the next monthly run.
            if (existing.Any(s => s.VendorId == group.Key)) continue;
            var items = group.ToList();
            var due = new DateTime(start.Year, start.Month, Math.Min(30, DateTime.DaysInMonth(start.Year, start.Month)), 23, 59, 59, DateTimeKind.Utc).AddHours(-7);
            var statement = CommissionSettlement.Create(group.Key, r.Period, items.Count, items.Sum(c => c.OutstandingAmount), due);
            _db.CommissionSettlements.Add(statement);
            foreach (var c in items) c.AssignSettlement(statement.Id);
            var credits = await _db.CommissionCreditNotes.Where(x => x.VendorId == group.Key && x.AppliedAmount < x.Amount).OrderBy(x => x.CreatedAt).ToListAsync(ct);
            var creditAmount = Math.Min(statement.TotalAmount, credits.Sum(x => x.Amount - x.AppliedAmount));
            var remainingCredit = creditAmount;
            foreach (var credit in credits) { var take = Math.Min(remainingCredit, credit.Amount - credit.AppliedAmount); if (take > 0) { credit.Apply(take); remainingCredit -= take; } }
            remainingCredit = creditAmount;
            foreach (var c in items) { var take = Math.Min(remainingCredit, c.OutstandingAmount); if (take > 0) { c.ApplyPayment(take, $"CREDIT-{statement.Id:N}"); remainingCredit -= take; } }
            statement.ApplyCredit(creditAmount); statement.Close();
        }
        await _audit.LogAsync("Settlement.Closed", nameof(CommissionSettlement), r.Period, new { count = commissions.Count });
        await _db.SaveChangesAsync(ct);
        return (await _db.CommissionSettlements.AsNoTracking().Include(s => s.Vendor).Where(s => s.Period == r.Period).ToListAsync(ct)).Select(s => SettlementMap.ToDto(s, _pay)).ToList();
    }
}

public record GetSettlementsQuery(string? Period) : IRequest<List<SettlementDto>>;
public class GetSettlementsQueryHandler : IRequestHandler<GetSettlementsQuery, List<SettlementDto>>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _user;
    private readonly IMockPaymentGateway _pay;
    public GetSettlementsQueryHandler(IApplicationDbContext db, ICurrentUserService user, IMockPaymentGateway pay) { _db = db; _user = user; _pay = pay; }
    public async Task<List<SettlementDto>> Handle(GetSettlementsQuery r, CancellationToken ct)
    {
        var query = _db.CommissionSettlements.AsNoTracking().Include(s => s.Vendor).AsQueryable();
        if (_user.Role == nameof(UserRole.VendorOwner)) query = query.Where(s => s.Vendor.UserId == _user.UserId);
        else if (_user.Role is not (nameof(UserRole.Finance) or nameof(UserRole.SuperAdmin))) throw new DomainException("Bạn không có quyền xem đối soát.");
        if (!string.IsNullOrWhiteSpace(r.Period)) query = query.Where(s => s.Period == r.Period);
        return (await query.OrderByDescending(s => s.Period).Take(200).ToListAsync(ct)).Select(s => SettlementMap.ToDto(s, _pay)).ToList();
    }
}

public record PaySettlementCommand(Guid Id, decimal? Amount) : IRequest<SettlementDto>;
public class PaySettlementCommandHandler : IRequestHandler<PaySettlementCommand, SettlementDto>
{
    public Task<SettlementDto> Handle(PaySettlementCommand request, CancellationToken ct) => throw new DomainException("Đã tắt xác nhận giả lập. Ghi nhận giao dịch ngân hàng thật với transactionId hoặc dùng webhook ký HMAC.");
}
