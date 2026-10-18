using MediatR;
using Microsoft.EntityFrameworkCore;
using Wedding.Application.Common.Interfaces;
using Wedding.Application.Features.Contracts.DTOs;
using Wedding.Domain.Entities;
using Wedding.Domain.Enums;
using Wedding.Domain.Exceptions;
using Wedding.Application.Features.Planner;
using Wedding.Application.Features.CustomerOrders;

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

    public Task<ContractDto> Handle(ConfirmContractCommand request, CancellationToken cancellationToken) => _context.ExecuteSerializableAsync(ct => ConfirmAsync(request, ct), cancellationToken);
    private async Task<ContractDto> ConfirmAsync(ConfirmContractCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUserService.UserId
            ?? throw new DomainException("Bạn cần đăng nhập để xác nhận hợp đồng.");

        var contract = await _context.BookingContracts
            .Include(c => c.Vendor)
            .Include(c => c.Customer)
            .Include(c => c.Voucher)
            .Include(c => c.Lead)
                .ThenInclude(l => l.Listing).ThenInclude(l => l!.Category)
            .Include(c => c.Commissions)
            .FirstOrDefaultAsync(c => c.Id == request.ContractId, cancellationToken)
            ?? throw new NotFoundException("Hợp đồng không tồn tại.");

        // BR-005: Khách hàng là người trực tiếp xác thực giao dịch
        if (contract.CustomerId != userId)
        {
            throw new DomainException("Chỉ khách hàng của hợp đồng mới có quyền xác nhận giao dịch (BR-005).");
        }
        var weddingScope = await WeddingAccess.ResolveAsync(_context, _currentUserService, cancellationToken, write: true);
        CustomerOrderJourney.EnsureActiveLead(contract.Lead);

        // Kiểm tra điều kiện và chuyển sang trạng thái Confirmed (nếu quá 72h sẽ throw exception)
        contract.ConfirmByCustomer();

        // Nếu có mã Voucher ưu đãi: Đánh dấu đã sử dụng (Redeemed)
        if (contract.Voucher != null && contract.Voucher.Status == VoucherStatus.Active)
        {
            contract.Voucher.Redeem();
        }

        // P4: chốt (snapshot) tỷ lệ hoa hồng tại thời điểm khách xác nhận – thay đổi tỷ lệ sau này không ảnh hưởng HĐ cũ
        if (!contract.WeddingDate.HasValue) throw new DomainException("Cần ngày cưới để xác định thời điểm giải phóng K2.");
        contract.SnapshotCommissionRate(CommissionRules.ResolveRate(contract.Lead.Listing?.Category.DefaultCommissionRate ?? 0.08m, contract.Vendor.CommissionRate));
        var commissionRate = contract.CommissionRateSnapshot!.Value;
        var split = CommissionRules.Split(contract.ContractValue, commissionRate);

        // BR-007: Hạn thanh toán hoa hồng là ngày 25 của chu kỳ đối soát
        var now = DateTime.UtcNow;
        var dueDate = SettlementCalendar.DueDate(now);

        var commission = Commission.Create(
            contractId: contract.Id,
            vendorId: contract.VendorId,
            period: CommissionPeriod.Period1_Deposit,
            commissionRate: commissionRate,
            commissionAmount: split.First,
            dueDate: dueDate
        );

        _context.Commissions.Add(commission);
        var second = Commission.Create(contract.Id, contract.VendorId, CommissionPeriod.Period2_Completion, commissionRate, split.Second, SettlementCalendar.DueDate(contract.WeddingDate.Value.AddDays(3)));
        second.LockUntil(contract.WeddingDate.Value.Date.AddDays(3).AddHours(-7));
        _context.Commissions.Add(second);
        contract.Lead.MarkContracted();
        contract.Lead.UnlockPhone();
        var budget = await _context.BudgetItems.FirstOrDefaultAsync(b => b.ContractId == contract.Id, cancellationToken);
        budget ??= await Wedding.Application.Features.Combos.ComboBudgetSynchronization.FindContractEstimateAsync(_context, contract.LeadId, weddingScope, cancellationToken);
        if (budget == null)
        {
            var priorComboEstimate = await _context.ComboOrderItems.AnyAsync(i => i.LeadId == contract.LeadId && i.BudgetItemId.HasValue, cancellationToken);
            var note = priorComboEstimate ? "Dự toán combo cũ đã được chỉnh sửa/đã chi hoặc không có đủ dấu vết tự động, nên hệ thống giữ nguyên và tạo khoản hợp đồng riêng. Hãy kiểm tra/gộp lại trong Planner để tránh trùng dự toán." : "Tự đồng bộ từ hợp đồng đã xác nhận.";
            budget = BudgetItem.Create(weddingScope.OwnerId, contract.Lead.Listing?.CategoryId, $"{contract.Vendor.BrandName} · {contract.ContractCode}", contract.ContractValue, contract.DepositAmount, note);
            if (priorComboEstimate) _context.Notifications.Add(Wedding.Domain.Entities.Notification.Create(userId, "InApp", "", $"Hợp đồng {contract.ContractCode}: {note}", contextKind: "contract", contextId: contract.Id));
            budget.LinkContract(contract.Id);
            if (weddingScope.ProfileId.HasValue) budget.AttachToWedding(weddingScope.ProfileId.Value);
            _context.BudgetItems.Add(budget);
        }
        else { budget.LinkContract(contract.Id); budget.SynchronizeContract(contract.ContractValue, contract.DepositAmount); }
        if (!await _context.ChecklistTasks.AnyAsync(t => t.ContractId == contract.Id, cancellationToken))
        {
            var task = ChecklistTask.Create(weddingScope.OwnerId, $"Thanh toán đợt 2 · {contract.Vendor.BrandName}", "Trước 1 tuần", contract.WeddingDate.Value.AddDays(-7), $"Còn {contract.ContractValue - contract.DepositAmount:N0} đ theo {contract.ContractCode}.");
            task.LinkContract(contract.Id);
            if (weddingScope.ProfileId.HasValue) task.AttachToWedding(weddingScope.ProfileId.Value);
            _context.ChecklistTasks.Add(task);
        }
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
        commissionDtos.Add(new ContractCommissionDto(second.Id, "Kỳ 2 (khóa đến sau cưới 3 ngày)", second.CommissionRate, second.CommissionAmount, second.DueDate, second.Status.ToString()));

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
