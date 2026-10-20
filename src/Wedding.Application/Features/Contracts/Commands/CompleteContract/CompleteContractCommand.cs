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

    public Task<ContractDto> Handle(CompleteContractCommand request, CancellationToken cancellationToken) => _context.ExecuteSerializableAsync(ct => CompleteAsync(request, ct), cancellationToken);
    private async Task<ContractDto> CompleteAsync(CompleteContractCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUserService.UserId
            ?? throw new DomainException("Bạn cần đăng nhập để thao tác.");

        var user = await _context.Users.AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == userId && u.IsActive, cancellationToken);
        if (user == null || user.Role.ToString() != _currentUserService.Role)
        {
            throw new DomainException("Tài khoản không hoạt động hoặc quyền đăng nhập đã thay đổi. Vui lòng đăng nhập lại.");
        }

        var contract = await _context.BookingContracts
            .Include(c => c.Vendor)
            .Include(c => c.Customer)
            .Include(c => c.Lead)
            .Include(c => c.Voucher)
            .Include(c => c.Commissions)
            .FirstOrDefaultAsync(c => c.Id == request.ContractId, cancellationToken)
            ?? throw new NotFoundException("Hợp đồng không tồn tại.");

        var isVendor = user.Role == UserRole.VendorOwner && contract.Vendor.UserId == userId && contract.Vendor.IsActive;
        var isCustomer = user.Role == UserRole.Customer && contract.CustomerId == userId;
        var isStaff = user.Role == UserRole.SuperAdmin || user.Role == UserRole.Finance || user.Role == UserRole.Moderator;

        if (!isVendor && !isStaff && !isCustomer)
        {
            throw new DomainException("Chỉ nhà cung cấp hoặc ban quản trị mới có quyền xác nhận hoàn tất dịch vụ cưới.");
        }

        // Completion must not infer historical liability from today's shop/category policy.
        // Older contracts without their original rate require an explicit financial review;
        // do not change status or create a second-period debt while that evidence is missing.
        var commissionRate = contract.CommissionRateSnapshot
            ?? throw new DomainException("Hợp đồng cũ chưa có snapshot hoa hồng ban đầu. Cần Admin/Tài chính kiểm tra hồ sơ trước khi hoàn tất; hệ thống không tự dùng tỷ lệ hiện tại của shop.");
        if (commissionRate is < .03m or > .12m)
            throw new DomainException("Snapshot hoa hồng ban đầu không hợp lệ. Cần Admin/Tài chính kiểm tra, không tự tính lại theo chính sách hiện tại.");

        // Chuyển hợp đồng sang trạng thái Completed
        contract.CompleteContract();

        var existingCommissionsTotal = contract.Commissions.Sum(c => c.CommissionAmount);
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
            commissionK2.LockUntil(contract.WeddingDate!.Value.Date.AddDays(3).AddHours(-7));
            commissionK2.Release(DateTime.UtcNow);

            _context.Commissions.Add(commissionK2);
        }

        // Completing manually never bypasses the three-day complaint window.
        foreach (var c in contract.Commissions.Where(c => c.Period == CommissionPeriod.Period2_Completion)) c.Release(DateTime.UtcNow);

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
            )).ToList(),
            CancellationPolicySnapshot: contract.CancellationPolicySnapshot,
            CommissionRateSnapshot: contract.CommissionRateSnapshot,
            RejectReason: contract.RejectReason,
            RevisionCount: contract.RevisionCount,
            AppointmentDepositCredit: contract.AppointmentDepositCredit
        ).WithCategorySnapshot(contract);
    }
}
