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
    int PageSize = 20
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

        if (!string.IsNullOrWhiteSpace(request.Status) && Enum.TryParse<ContractStatus>(request.Status, true, out var contractStatus))
        {
            query = query.Where(c => c.Status == contractStatus);
        }

        var contracts = await query
            .OrderByDescending(c => c.CreatedAt)
            .Skip((request.PageNumber - 1) * request.PageSize)
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
            )).ToList()
        )).ToList();
    }
}
