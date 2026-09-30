using MediatR;
using Microsoft.EntityFrameworkCore;
using Wedding.Application.Common.Interfaces;
using Wedding.Application.Features.Contracts.DTOs;
using Wedding.Domain.Entities;
using Wedding.Domain.Enums;
using Wedding.Domain.Exceptions;

namespace Wedding.Application.Features.Contracts.Commands.ConfirmContract;

public record ConfirmContractCommand(Guid ContractId) : IRequest<ContractDto>;

public class ConfirmContractCommandHandler : IRequestHandler<ConfirmContractCommand, ContractDto>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public ConfirmContractCommandHandler(IApplicationDbContext context, ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<ContractDto> Handle(ConfirmContractCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUserService.UserId
            ?? throw new DomainException("Bạn cần đăng nhập để xác nhận hợp đồng.");

        var contract = await _context.BookingContracts
            .Include(c => c.Vendor)
            .Include(c => c.Customer)
            .Include(c => c.Voucher)
            .Include(c => c.Lead)
            .Include(c => c.Commissions)
            .FirstOrDefaultAsync(c => c.Id == request.ContractId, cancellationToken)
            ?? throw new NotFoundException("Hợp đồng không tồn tại.");

        // BR-005: Khách hàng là người trực tiếp xác thực giao dịch
        if (contract.CustomerId != userId)
        {
            throw new DomainException("Chỉ khách hàng của hợp đồng mới có quyền xác nhận giao dịch (BR-005).");
        }

        // Kiểm tra điều kiện và chuyển sang trạng thái Confirmed (nếu quá 72h sẽ throw exception)
        contract.ConfirmByCustomer();

        // Nếu có mã Voucher ưu đãi: Đánh dấu đã sử dụng (Redeemed)
        if (contract.Voucher != null && contract.Voucher.Status == VoucherStatus.Active)
        {
            contract.Voucher.Redeem();
        }

        var commissionRate = contract.Vendor.CommissionRate > 0 ? contract.Vendor.CommissionRate : 0.08m;
        var totalCommission = Math.Round(contract.ContractValue * commissionRate, 0);

        // BR-007: Hạn thanh toán hoa hồng là ngày 25 của chu kỳ đối soát
        var now = DateTime.UtcNow;
        var dueYear = now.Year;
        var dueMonth = now.Month;
        var dueDate = new DateTime(dueYear, dueMonth, 25, 23, 59, 59, DateTimeKind.Utc);
        if (now.Day > 25)
        {
            var next = now.AddMonths(1);
            dueDate = new DateTime(next.Year, next.Month, 25, 23, 59, 59, DateTimeKind.Utc);
        }

        var commission = Commission.Create(
            contractId: contract.Id,
            vendorId: contract.VendorId,
            period: CommissionPeriod.Period1_Deposit,
            commissionRate: commissionRate,
            commissionAmount: totalCommission,
            dueDate: dueDate
        );

        _context.Commissions.Add(commission);
        await _context.SaveChangesAsync(cancellationToken);

        var commissionDtos = new List<ContractCommissionDto>
        {
            new(
                Id: commission.Id,
                Period: "Kỳ 1 (50% lúc cọc)",
                CommissionRate: commission.CommissionRate,
                CommissionAmount: commission.CommissionAmount,
                DueDate: commission.DueDate,
                Status: commission.Status.ToString()
            )
        };

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
            Commissions: commissionDtos
        );
    }
}
