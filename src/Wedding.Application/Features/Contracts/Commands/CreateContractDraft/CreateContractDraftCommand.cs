using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Wedding.Application.Common.Interfaces;
using Wedding.Application.Features.Contracts.DTOs;
using Wedding.Domain.Entities;
using Wedding.Domain.Enums;
using Wedding.Domain.Exceptions;
using Wedding.Application.Features.CustomerOrders;

namespace Wedding.Application.Features.Contracts.Commands.CreateContractDraft;

public record CreateContractDraftCommand(
    Guid LeadId,
    string? VoucherCode,
    decimal ContractValue,
    decimal DepositAmount,
    string? ContractImageUrl,
    DateTime? WeddingDate,
    string? CancellationPolicy = null
) : IRequest<ContractDto>;

public class CreateContractDraftCommandValidator : AbstractValidator<CreateContractDraftCommand>
{
    public CreateContractDraftCommandValidator()
    {
        RuleFor(x => x.LeadId).NotEmpty().WithMessage("LeadId không được để trống.");
        RuleFor(x => x.ContractValue).GreaterThan(0).WithMessage("Giá trị hợp đồng phải lớn hơn 0 VNĐ.");
        RuleFor(x => x.DepositAmount).GreaterThanOrEqualTo(0).WithMessage("Tiền đặt cọc không được âm.")
            .LessThanOrEqualTo(x => x.ContractValue).WithMessage("Tiền đặt cọc không được vượt quá giá trị hợp đồng.");
        RuleFor(x => x.VoucherCode).MaximumLength(16).When(x => !string.IsNullOrEmpty(x.VoucherCode));
    }
}

public class CreateContractDraftCommandHandler : IRequestHandler<CreateContractDraftCommand, ContractDto>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public CreateContractDraftCommandHandler(IApplicationDbContext context, ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public Task<ContractDto> Handle(CreateContractDraftCommand request, CancellationToken cancellationToken) => _context.ExecuteSerializableAsync(ct => CreateAsync(request, ct), cancellationToken);
    private async Task<ContractDto> CreateAsync(CreateContractDraftCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUserService.UserId
            ?? throw new DomainException("Bạn cần đăng nhập để tạo hợp đồng.");

        var vendor = await _context.Vendors
            .FirstOrDefaultAsync(v => v.UserId == userId, cancellationToken)
            ?? throw new DomainException("Hồ sơ nhà cung cấp không tồn tại.");

        var lead = await _context.Leads
            .Include(l => l.Customer)
            .Include(l => l.Listing)
            .FirstOrDefaultAsync(l => l.Id == request.LeadId, cancellationToken)
            ?? throw new NotFoundException("Yêu cầu tư vấn không tồn tại.");

        if (lead.VendorId != vendor.Id)
        {
            throw new DomainException("Bạn không phải nhà cung cấp được chỉ định cho yêu cầu tư vấn này.");
        }

        CustomerOrderJourney.EnsureActiveLead(lead);

        // Kiểm tra xem Lead này đã có hợp đồng nào đang chờ xác nhận hoặc đã xác nhận chưa
        var existingContract = await _context.BookingContracts
            .AnyAsync(c => c.LeadId == lead.Id, cancellationToken);

        if (existingContract)
        {
            throw new DomainException("Yêu cầu này đã có hợp đồng. Hãy sửa và gửi lại bản nháp hoặc tạo yêu cầu mới để giữ lịch sử.");
        }

        Guid? voucherId = null;
        string? voucherCode = null;
        decimal? voucherDiscount = null;

        // Xử lý mã Voucher BR-004 nếu đối tác nhập
        if (!string.IsNullOrWhiteSpace(request.VoucherCode))
        {
            var cleanCode = request.VoucherCode.Trim().ToUpperInvariant();
            var voucher = await _context.Vouchers
                .FirstOrDefaultAsync(v => v.Code == cleanCode, cancellationToken)
                ?? throw new DomainException($"Mã ưu đãi '{cleanCode}' không tồn tại trên hệ thống.");

            if (voucher.Status != VoucherStatus.Active)
            {
                throw new DomainException($"Mã ưu đãi '{cleanCode}' không khả dụng hoặc đã được sử dụng.");
            }

            if (voucher.ExpiresAt < DateTime.UtcNow)
            {
                throw new DomainException($"Mã ưu đãi '{cleanCode}' đã hết hạn 30 ngày (BR-004).");
            }

            if (voucher.CustomerId != lead.CustomerId || voucher.LeadId != lead.Id)
            {
                throw new DomainException("Mã ưu đãi không thuộc về khách hàng của hợp đồng này.");
            }

            voucherId = voucher.Id;
            voucherCode = voucher.Code;
            voucherDiscount = voucher.DiscountValue;
        }

        // Khởi tạo hợp đồng ở trạng thái PendingVerification (BR-005: Khách duyệt trong 72h)
        var contract = BookingContract.Create(
            leadId: lead.Id,
            voucherId: voucherId,
            vendorId: vendor.Id,
            customerId: lead.CustomerId,
            contractValue: request.ContractValue,
            depositAmount: request.DepositAmount,
            contractImageUrl: request.ContractImageUrl,
            weddingDate: request.WeddingDate ?? lead.WeddingDate
        );

        contract.SetCancellationPolicy(request.CancellationPolicy ?? lead.Listing?.CancellationPolicy);
        var appointmentCredit = (await _context.Appointments.Where(a => a.LeadId == lead.Id && a.DepositPaid && a.RefundDue == 0 && a.RefundedAt == null).ToListAsync(cancellationToken)).Sum(a => a.DepositAmount);
        contract.ApplyAppointmentDepositCredit(appointmentCredit);

        _context.BookingContracts.Add(contract);
        await _context.SaveChangesAsync(cancellationToken);

        return new ContractDto(
            Id: contract.Id,
            ContractCode: contract.ContractCode,
            LeadId: contract.LeadId,
            VoucherId: contract.VoucherId,
            VoucherCode: voucherCode,
            VoucherDiscount: voucherDiscount,
            VendorId: contract.VendorId,
            VendorBrandName: vendor.BrandName,
            CustomerId: contract.CustomerId,
            CustomerName: lead.Customer.FullName,
            CustomerPhone: lead.RawPhoneNumber,
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
            Commissions: new List<ContractCommissionDto>(),
            CancellationPolicySnapshot: contract.CancellationPolicySnapshot,
            AppointmentDepositCredit: contract.AppointmentDepositCredit
        );
    }
}
