using MediatR;
using Microsoft.EntityFrameworkCore;
using Wedding.Application.Common.Interfaces;
using Wedding.Application.Features.Contracts.DTOs;
using Wedding.Domain.Enums;
using Wedding.Domain.Exceptions;

namespace Wedding.Application.Features.Contracts.Queries.GetContractDetail;

public record GetContractDetailQuery(Guid Id) : IRequest<ContractDto>;

public class GetContractDetailQueryHandler : IRequestHandler<GetContractDetailQuery, ContractDto>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public GetContractDetailQueryHandler(IApplicationDbContext context, ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<ContractDto> Handle(GetContractDetailQuery request, CancellationToken cancellationToken)
    {
        var userId = _currentUserService.UserId
            ?? throw new DomainException("Bạn cần đăng nhập để xem thông tin hợp đồng.");

        var contract = await _context.BookingContracts
            .Include(c => c.Vendor)
            .Include(c => c.Customer)
            .Include(c => c.Lead)
            .Include(c => c.Voucher)
            .Include(c => c.Commissions)
            .FirstOrDefaultAsync(c => c.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException("Hợp đồng không tồn tại.");

        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken)
            ?? throw new NotFoundException("Người dùng không tồn tại.");

        var isCustomer = contract.CustomerId == userId;
        var isVendor = contract.Vendor.UserId == userId;
        var isStaff = user.Role == UserRole.SuperAdmin || user.Role == UserRole.Finance || user.Role == UserRole.Moderator;

        if (!isCustomer && !isVendor && !isStaff)
        {
            throw new DomainException("Bạn không có quyền truy cập thông tin hợp đồng này.");
        }

        return new ContractDto(
            Id: contract.Id,
            ContractCode: contract.ContractCode,
            LeadId: contract.LeadId,
            VoucherId: contract.VoucherId,
            VoucherCode: contract.Voucher?.Code,
            VoucherDiscount: contract.Voucher?.DiscountValue,
            VendorId: contract.VendorId,
            VendorBrandName: contract.Vendor.BrandName,
            CustomerId: contract.CustomerId,
            CustomerName: contract.Customer.FullName,
            CustomerPhone: contract.Lead.RawPhoneNumber,
            ContractValue: contract.ContractValue,
            DepositAmount: contract.DepositAmount,
            ContractImageUrl: contract.ContractImageUrl,
            WeddingDate: contract.WeddingDate,
            Status: contract.Status.ToString(),
            VerificationDeadline: contract.VerificationDeadline,
            ConfirmedAt: contract.ConfirmedAt,
            CompletedAt: contract.CompletedAt,
            CancellationReason: contract.CancellationReason,
            CreatedAt: contract.CreatedAt,
            Commissions: contract.Commissions.Select(cm => new ContractCommissionDto(
                Id: cm.Id,
                Period: cm.Period == CommissionPeriod.Period1_Deposit ? "Kỳ 1 (50% lúc cọc)" : "Kỳ 2 (50% sau cưới)",
                CommissionRate: cm.CommissionRate,
                CommissionAmount: cm.CommissionAmount,
                DueDate: cm.DueDate,
                Status: cm.Status.ToString()
            )).ToList()
        );
    }
}
