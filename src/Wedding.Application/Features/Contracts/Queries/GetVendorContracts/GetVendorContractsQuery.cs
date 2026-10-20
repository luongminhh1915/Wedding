using MediatR;
using Microsoft.EntityFrameworkCore;
using Wedding.Application.Common.Interfaces;
using Wedding.Application.Features.Contracts.DTOs;
using Wedding.Domain.Enums;
using Wedding.Domain.Exceptions;

namespace Wedding.Application.Features.Contracts.Queries.GetVendorContracts;

public record GetVendorContractsQuery(
    string? Status = null,
    int PageNumber = 1,
    int PageSize = 20,
    Guid? FocusId = null
) : IRequest<List<ContractDto>>;

public class GetVendorContractsQueryHandler : IRequestHandler<GetVendorContractsQuery, List<ContractDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public GetVendorContractsQueryHandler(IApplicationDbContext context, ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<List<ContractDto>> Handle(GetVendorContractsQuery request, CancellationToken cancellationToken)
    {
        var userId = _currentUserService.UserId
            ?? throw new DomainException("Bạn cần đăng nhập để xem danh sách hợp đồng.");
        if (request.PageNumber < 1 || request.PageSize is < 1 or > 50) throw new DomainException("Trang hoặc số lượng hợp đồng không hợp lệ.");

        var vendor = await _context.Vendors
            .FirstOrDefaultAsync(v => v.UserId == userId, cancellationToken)
            ?? throw new DomainException("Hồ sơ nhà cung cấp không tồn tại.");

        var query = _context.BookingContracts
            .Include(c => c.Customer)
            .Include(c => c.Lead)
            .Include(c => c.Voucher)
            .Include(c => c.Commissions)
            .Where(c => c.VendorId == vendor.Id)
            .AsNoTracking();

        if (request.FocusId.HasValue) query = query.Where(c => c.Id == request.FocusId.Value);
        else if (!string.IsNullOrWhiteSpace(request.Status) && Enum.TryParse<ContractStatus>(request.Status, true, out var contractStatus))
        {
            query = query.Where(c => c.Status == contractStatus);
        }

        var contracts = await query
            .OrderByDescending(c => c.CreatedAt)
            .ThenByDescending(c => c.Id)
            .Skip(request.FocusId.HasValue ? 0 : (int)Math.Min((long)(request.PageNumber - 1) * request.PageSize, int.MaxValue))
            .Take(request.PageSize)
            .ToListAsync(cancellationToken);

        return contracts.Select(c => new ContractDto(
            Id: c.Id,
            ContractCode: c.ContractCode,
            LeadId: c.LeadId,
            VoucherId: c.VoucherId,
            VoucherCode: c.Voucher?.Code,
            VoucherDiscount: c.Voucher?.DiscountValue,
            VendorId: c.VendorId,
            VendorBrandName: vendor.BrandName,
            CustomerId: c.CustomerId,
            CustomerName: c.Customer.FullName,
            CustomerPhone: c.Lead.RawPhoneNumber, // Khi đã ký HĐ thực tế thì Vendor thấy SĐT liên hệ
            ContractValue: c.ContractValue,
            DepositAmount: c.DepositAmount,
            ContractImageUrl: c.ContractImageUrl,
            WeddingDate: c.WeddingDate,
            Status: c.Status.ToString(),
            VerificationDeadline: c.VerificationDeadline,
            ConfirmedAt: c.ConfirmedAt,
            CompletedAt: c.CompletedAt,
            CancellationReason: c.CancellationReason,
            CreatedAt: c.CreatedAt,
            Commissions: c.Commissions.Select(cm => new ContractCommissionDto(
                Id: cm.Id,
                Period: cm.Period == CommissionPeriod.Period1_Deposit ? "Kỳ 1 (50% lúc cọc)" : "Kỳ 2 (50% sau cưới)",
                CommissionRate: cm.CommissionRate,
                CommissionAmount: cm.CommissionAmount,
                DueDate: cm.DueDate,
                Status: cm.Status.ToString()
            )).ToList(),
            CancellationPolicySnapshot: c.CancellationPolicySnapshot,
            CommissionRateSnapshot: c.CommissionRateSnapshot,
            RejectReason: c.RejectReason,
            RevisionCount: c.RevisionCount,
            AppointmentDepositCredit: c.AppointmentDepositCredit
        ).WithCategorySnapshot(c)).ToList();
    }
}
