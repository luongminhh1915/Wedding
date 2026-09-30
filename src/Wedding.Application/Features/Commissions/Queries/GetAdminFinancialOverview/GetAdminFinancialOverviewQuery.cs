using MediatR;
using Microsoft.EntityFrameworkCore;
using Wedding.Application.Common.Interfaces;
using Wedding.Domain.Enums;

namespace Wedding.Application.Features.Commissions.Queries.GetAdminFinancialOverview;

// DTO cho từng khoản thanh toán hoa hồng
public record AdminCommissionItemDto(
    Guid Id,
    decimal CommissionAmount,
    DateTime DueDate,
    string Status,
    DateTime? PaidAt,
    string? PaymentReferenceCode
);

// DTO cho từng đơn hợp đồng trong bảng kiểm kê
public record ContractFinancialRowDto(
    Guid ContractId,
    string ContractCode,
    Guid VendorId,
    string VendorBrandName,
    string OwnerName,
    string OwnerEmail,
    string CustomerName,
    string CustomerEmail,
    string CustomerPhone,
    decimal ContractValue,
    decimal DepositAmount,
    decimal TotalCommissionDue,
    decimal TotalCommissionPaid,
    decimal TotalCommissionPending,
    decimal TotalCommissionOverdue,
    decimal CommissionRate,
    string PaymentStatus, // Paid | PartiallyPaid | Pending | Overdue
    DateTime CreatedAt,
    DateTime? WeddingDate,
    DateTime DueDate,
    List<AdminCommissionItemDto> Items
);

// Tổng hợp toàn hệ thống cho Admin
public record AdminFinancialOverviewDto(
    int TotalVendors,
    int TotalContracts,
    decimal TotalGmv,
    decimal TotalDepositReceived,
    decimal TotalCommissionDue,
    decimal TotalCommissionPaid,
    decimal TotalCommissionPending,
    decimal TotalCommissionOverdue,
    List<ContractFinancialRowDto> Orders,
    List<ContractFinancialRowDto> Vendors // Giữ alias vendors để tương thích ngược
);

public record GetAdminFinancialOverviewQuery(
    int? Month = null,
    int? Year = null
) : IRequest<AdminFinancialOverviewDto>;

public class GetAdminFinancialOverviewQueryHandler
    : IRequestHandler<GetAdminFinancialOverviewQuery, AdminFinancialOverviewDto>
{
    private readonly IApplicationDbContext _context;

    public GetAdminFinancialOverviewQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<AdminFinancialOverviewDto> Handle(
        GetAdminFinancialOverviewQuery request,
        CancellationToken cancellationToken)
    {
        // Lấy toàn bộ commissions kèm contract + vendor + customer
        var commissionsQuery = _context.Commissions
            .Include(c => c.Contract)
                .ThenInclude(ct => ct.Customer)
            .Include(c => c.Contract)
                .ThenInclude(ct => ct.Vendor)
                    .ThenInclude(v => v.User)
            .AsNoTracking();

        // Lọc theo tháng/năm nếu có
        if (request.Month.HasValue && request.Year.HasValue)
        {
            var start = new DateTime(request.Year.Value, request.Month.Value, 1, 0, 0, 0, DateTimeKind.Utc);
            var end = start.AddMonths(1);
            commissionsQuery = commissionsQuery.Where(c => c.CreatedAt >= start && c.CreatedAt < end);
        }
        else if (request.Year.HasValue)
        {
            var start = new DateTime(request.Year.Value, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            var end = start.AddYears(1);
            commissionsQuery = commissionsQuery.Where(c => c.CreatedAt >= start && c.CreatedAt < end);
        }

        var commissions = await commissionsQuery.ToListAsync(cancellationToken);

        // Nhóm theo từng Đơn Hợp Đồng (Contract) để hiển thị chi tiết từng đơn
        var grouped = commissions
            .GroupBy(c => c.ContractId)
            .Select(g =>
            {
                var first = g.First();
                var contract = first.Contract;
                var vendor = contract.Vendor;
                var customer = contract.Customer;

                var totalDue = g.Sum(c => c.CommissionAmount);
                var totalPaid = g.Where(c => c.Status == CommissionStatus.Paid).Sum(c => c.CommissionAmount);
                var totalPending = g.Where(c => c.Status == CommissionStatus.Pending).Sum(c => c.CommissionAmount);
                var totalOverdue = g.Where(c => c.Status == CommissionStatus.Overdue).Sum(c => c.CommissionAmount);

                var commissionRate = vendor.CommissionRate > 0
                    ? vendor.CommissionRate
                    : (g.Select(c => c.CommissionRate).FirstOrDefault(r => r > 0));

                string payStatus;
                if (totalPending + totalOverdue == 0 && totalPaid > 0) payStatus = "Paid";
                else if (totalOverdue > 0) payStatus = "Overdue";
                else if (totalPaid > 0) payStatus = "PartiallyPaid";
                else payStatus = "Pending";

                var dueDate = g.OrderBy(c => c.DueDate).Select(c => c.DueDate).FirstOrDefault();
                if (dueDate == default) dueDate = DateTime.UtcNow.AddDays(25);

                var items = g
                    .OrderBy(c => c.PaidAt.HasValue ? 0 : 1)
                    .ThenBy(c => c.PaidAt ?? c.CreatedAt)
                    .Select(c => new AdminCommissionItemDto(
                        c.Id,
                        c.CommissionAmount,
                        c.DueDate,
                        c.Status.ToString(),
                        c.PaidAt,
                        c.PaymentReferenceCode
                    ))
                    .ToList();

                return new ContractFinancialRowDto(
                    ContractId: contract.Id,
                    ContractCode: contract.ContractCode,
                    VendorId: vendor.Id,
                    VendorBrandName: vendor.BrandName,
                    OwnerName: vendor.User?.FullName ?? "",
                    OwnerEmail: vendor.User?.Email ?? "",
                    CustomerName: customer?.FullName ?? "",
                    CustomerEmail: customer?.Email ?? "",
                    CustomerPhone: customer?.PhoneNumber ?? "",
                    ContractValue: contract.ContractValue,
                    DepositAmount: contract.DepositAmount,
                    TotalCommissionDue: totalDue,
                    TotalCommissionPaid: totalPaid,
                    TotalCommissionPending: totalPending,
                    TotalCommissionOverdue: totalOverdue,
                    CommissionRate: commissionRate,
                    PaymentStatus: payStatus,
                    CreatedAt: contract.CreatedAt,
                    WeddingDate: contract.WeddingDate,
                    DueDate: dueDate,
                    Items: items
                );
            })
            .OrderByDescending(o => o.TotalCommissionOverdue)
            .ThenByDescending(o => o.TotalCommissionPending)
            .ThenByDescending(o => o.CreatedAt)
            .ToList();

        return new AdminFinancialOverviewDto(
            TotalVendors: grouped.Select(o => o.VendorId).Distinct().Count(),
            TotalContracts: grouped.Count,
            TotalGmv: grouped.Sum(o => o.ContractValue),
            TotalDepositReceived: grouped.Sum(o => o.DepositAmount),
            TotalCommissionDue: grouped.Sum(o => o.TotalCommissionDue),
            TotalCommissionPaid: grouped.Sum(o => o.TotalCommissionPaid),
            TotalCommissionPending: grouped.Sum(o => o.TotalCommissionPending),
            TotalCommissionOverdue: grouped.Sum(o => o.TotalCommissionOverdue),
            Orders: grouped,
            Vendors: grouped
        );
    }
}
