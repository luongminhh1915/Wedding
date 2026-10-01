using MediatR;
using Microsoft.EntityFrameworkCore;
using Wedding.Application.Common.Interfaces;
using Wedding.Application.Features.Commissions.DTOs;
using Wedding.Domain.Enums;
using Wedding.Domain.Exceptions;

namespace Wedding.Application.Features.Commissions.Queries.GetVendorSettlementStatement;

public record GetVendorSettlementStatementQuery(
    int? Month = null,
    int? Year = null
) : IRequest<MonthlySettlementStatementDto>;

public class GetVendorSettlementStatementQueryHandler : IRequestHandler<GetVendorSettlementStatementQuery, MonthlySettlementStatementDto>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IVietQrService _vietQrService;
    private readonly IAdvancePaymentService _advancePaymentService;

    public GetVendorSettlementStatementQueryHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService,
        IVietQrService vietQrService,
        IAdvancePaymentService advancePaymentService)
    {
        _context = context;
        _currentUserService = currentUserService;
        _vietQrService = vietQrService;
        _advancePaymentService = advancePaymentService;
    }

    public async Task<MonthlySettlementStatementDto> Handle(GetVendorSettlementStatementQuery request, CancellationToken cancellationToken)
    {
        var userId = _currentUserService.UserId
            ?? throw new DomainException("Bạn cần đăng nhập để xem bảng kê đối soát hoa hồng.");

        var vendor = await _context.Vendors
            .FirstOrDefaultAsync(v => v.UserId == userId, cancellationToken)
            ?? throw new DomainException("Hồ sơ nhà cung cấp không tồn tại.");

        var now = DateTime.UtcNow;
        var targetMonth = request.Month ?? now.Month;
        var targetYear = request.Year ?? now.Year;

        // Chu kỳ đối soát: chốt sổ ngày 25 hàng tháng (BR-007)
        var settlementDate = new DateTime(targetYear, targetMonth, 25, 0, 0, 0, DateTimeKind.Utc);
        var lastDay = DateTime.DaysInMonth(targetYear, targetMonth);
        var dueDate = new DateTime(targetYear, targetMonth, lastDay, 23, 59, 59, DateTimeKind.Utc);

        // Lấy toàn bộ các khoản hoa hồng của Vendor
        var commissions = await _context.Commissions
            .Include(c => c.Contract)
                .ThenInclude(ct => ct.Customer)
            .Where(c => c.VendorId == vendor.Id)
            .OrderByDescending(c => c.CreatedAt)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        // Lọc các khoản hoa hồng trong kỳ đối soát tháng này (hoặc các khoản Pending còn tồn)
        var items = commissions.Select(c =>
        {
            var isPeriod1 = c.Period == CommissionPeriod.Period1_Deposit;
            var periodName = isPeriod1 ? "Kỳ 1 (50% lúc cọc)" : "Kỳ 2 (50% sau cưới)";
            var qr = _vietQrService.GenerateQrImageUrl("MB", "0338889999", "WEDDING PLATFORM VN", c.CommissionAmount, $"HH {c.Id.ToString("N")[..6].ToUpper()}");

            return new CommissionItemDto(
                Id: c.Id,
                ContractId: c.ContractId,
                ContractCode: c.Contract.ContractCode,
                CustomerName: c.Contract.Customer.FullName,
                ContractValue: c.Contract.ContractValue,
                Period: periodName,
                CommissionRate: c.CommissionRate,
                CommissionAmount: c.CommissionAmount,
                DueDate: c.DueDate,
                Status: c.Status.ToString(),
                PaidAt: c.PaidAt,
                PaymentReferenceCode: c.PaymentReferenceCode,
                QrImageUrl: qr
            );
        }).ToList();

        var totalContractVal = commissions
            .Select(c => c.ContractId)
            .Distinct()
            .Select(cid => commissions.First(x => x.ContractId == cid).Contract.ContractValue)
            .Sum();
        var totalCommissionVal = commissions.Sum(c => c.CommissionAmount);
        var totalPending = commissions.Where(c => c.Status == CommissionStatus.Pending).Sum(c => c.CommissionAmount);
        var totalOverdue = commissions.Where(c => c.Status == CommissionStatus.Overdue).Sum(c => c.CommissionAmount);
        var totalPaid = commissions.Where(c => c.Status == CommissionStatus.Paid).Sum(c => c.CommissionAmount);

        var totalNeedPay = totalPending + totalOverdue;

        string overallStatus;
        if (totalNeedPay == 0 && totalPaid > 0) overallStatus = "Paid";
        else if (totalOverdue > 0) overallStatus = "Overdue";
        else if (totalPaid > 0) overallStatus = "PartiallyPaid";
        else overallStatus = "Pending";

        // BR-007: Nội dung chuyển khoản định danh: HH <VendorId> T<Thang>
        var vendorShortCode = vendor.Id.ToString("N").Substring(0, 6).ToUpperInvariant();
        var transferContent = $"HH {vendorShortCode} T{targetMonth:D2}";

        VietQrInfo? vietQr = null;
        if (totalNeedPay > 0)
        {
            vietQr = _vietQrService.GenerateVietQr(totalNeedPay, transferContent);
        }

        var allAdvances = await _advancePaymentService.GetAllAsync();
        var vendorAdvances = allAdvances
            .Where(a => a.VendorId == vendor.Id || commissions.Any(c => c.ContractId == a.ContractId))
            .OrderByDescending(a => a.RequestedAt)
            .ToList();

        return new MonthlySettlementStatementDto(
            VendorId: vendor.Id,
            VendorBrandName: vendor.BrandName,
            Month: targetMonth,
            Year: targetYear,
            SettlementDate: settlementDate,
            DueDate: dueDate,
            TotalContractValue: totalContractVal,
            TotalCommissionAmount: totalCommissionVal,
            TotalPendingAmount: totalNeedPay,
            TotalPaidAmount: totalPaid,
            TotalOverdueAmount: totalOverdue,
            PaymentStatus: overallStatus,
            TransferContent: transferContent,
            VietQr: vietQr,
            Items: items,
            AdvanceRequests: vendorAdvances
        );
    }
}
