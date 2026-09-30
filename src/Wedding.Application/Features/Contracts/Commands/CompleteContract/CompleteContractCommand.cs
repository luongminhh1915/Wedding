using MediatR;
using Microsoft.EntityFrameworkCore;
using Wedding.Application.Common.Interfaces;
using Wedding.Application.Features.Contracts.DTOs;
using Wedding.Domain.Entities;
using Wedding.Domain.Enums;
using Wedding.Domain.Exceptions;

namespace Wedding.Application.Features.Contracts.Commands.CompleteContract;

public record CompleteContractCommand(Guid ContractId) : IRequest<ContractDto>;

public class CompleteContractCommandHandler : IRequestHandler<CompleteContractCommand, ContractDto>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public CompleteContractCommandHandler(IApplicationDbContext context, ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<ContractDto> Handle(CompleteContractCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUserService.UserId
            ?? throw new DomainException("Bạn cần đăng nhập để thao tác.");

        var contract = await _context.BookingContracts
            .Include(c => c.Vendor)
            .Include(c => c.Customer)
            .Include(c => c.Lead)
            .Include(c => c.Voucher)
            .Include(c => c.Commissions)
            .FirstOrDefaultAsync(c => c.Id == request.ContractId, cancellationToken)
            ?? throw new NotFoundException("Hợp đồng không tồn tại.");

        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken)
            ?? throw new NotFoundException("Người dùng không tồn tại.");

        var isVendor = contract.Vendor.UserId == userId;
        var isStaff = user.Role == UserRole.SuperAdmin || user.Role == UserRole.Finance || user.Role == UserRole.Moderator;

        if (!isVendor && !isStaff)
        {
            throw new DomainException("Chỉ nhà cung cấp hoặc ban quản trị mới có quyền xác nhận hoàn tất dịch vụ cưới.");
        }

        // Chuyển hợp đồng sang trạng thái Completed
        contract.CompleteContract();

        var existingCommissionsTotal = contract.Commissions.Sum(c => c.CommissionAmount);
        var commissionRate = contract.Vendor.CommissionRate > 0 ? contract.Vendor.CommissionRate : 0.08m;
        var totalCommission = Math.Round(contract.ContractValue * commissionRate, 0);
        var remainderCommission = totalCommission - existingCommissionsTotal;

        if (remainderCommission > 0)
        {
            var now = DateTime.UtcNow;
            var dueDate = new DateTime(now.Year, now.Month, 25, 23, 59, 59, DateTimeKind.Utc);
            if (now.Day > 25)
            {
                var next = now.AddMonths(1);
                dueDate = new DateTime(next.Year, next.Month, 25, 23, 59, 59, DateTimeKind.Utc);
            }

            var commissionK2 = Commission.Create(
                contractId: contract.Id,
                vendorId: contract.VendorId,
                period: CommissionPeriod.Period2_Completion,
                commissionRate: commissionRate,
                commissionAmount: remainderCommission,
                dueDate: dueDate
            );

            _context.Commissions.Add(commissionK2);
        }

        await _context.SaveChangesAsync(cancellationToken);

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
