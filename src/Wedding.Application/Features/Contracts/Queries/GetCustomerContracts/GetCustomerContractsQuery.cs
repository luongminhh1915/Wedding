using MediatR;
using Microsoft.EntityFrameworkCore;
using Wedding.Application.Common.Interfaces;
using Wedding.Application.Features.Contracts.DTOs;
using Wedding.Domain.Enums;
using Wedding.Domain.Exceptions;

namespace Wedding.Application.Features.Contracts.Queries.GetCustomerContracts;

public record GetCustomerContractsQuery(
    int PageNumber = 1,
    int PageSize = 20
) : IRequest<List<ContractDto>>;

public class GetCustomerContractsQueryHandler : IRequestHandler<GetCustomerContractsQuery, List<ContractDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public GetCustomerContractsQueryHandler(IApplicationDbContext context, ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<List<ContractDto>> Handle(GetCustomerContractsQuery request, CancellationToken cancellationToken)
    {
        var userId = _currentUserService.UserId
            ?? throw new DomainException("Bạn cần đăng nhập để xem danh sách hợp đồng.");

        var contracts = await _context.BookingContracts
            .Include(c => c.Vendor)
            .Include(c => c.Customer)
            .Include(c => c.Lead)
            .Include(c => c.Voucher)
            .Include(c => c.Commissions)
            .Where(c => c.CustomerId == userId)
            .OrderByDescending(c => c.CreatedAt)
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return contracts.Select(c => new ContractDto(
            Id: c.Id,
            ContractCode: c.ContractCode,
            LeadId: c.LeadId,
            VoucherId: c.VoucherId,
            VoucherCode: c.Voucher?.Code,
            VoucherDiscount: c.Voucher?.DiscountValue,
            VendorId: c.VendorId,
            VendorBrandName: c.Vendor.BrandName,
            CustomerId: c.CustomerId,
            CustomerName: c.Customer.FullName,
            CustomerPhone: c.Lead.RawPhoneNumber,
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
