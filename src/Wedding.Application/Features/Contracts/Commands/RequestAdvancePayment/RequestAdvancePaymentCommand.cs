using MediatR;
using Microsoft.EntityFrameworkCore;
using Wedding.Application.Common.Interfaces;
using Wedding.Domain.Exceptions;

namespace Wedding.Application.Features.Contracts.Commands.RequestAdvancePayment;

public record RequestAdvancePaymentCommand(
    Guid ContractId,
    decimal Amount,
    string Reason,
    string? BankName = null,
    string? BankAccountNumber = null,
    string? BankAccountName = null,
    string? Note = null
) : IRequest<AdvancePaymentResultDto>;

public record AdvancePaymentResultDto(
    Guid ContractId,
    string ContractCode,
    decimal Amount,
    string Reason,
    string? BankName,
    string? BankAccountNumber,
    string? BankAccountName,
    string? Note,
    string Status,
    DateTime RequestedAt,
    string Message
);

public class RequestAdvancePaymentCommandHandler : IRequestHandler<RequestAdvancePaymentCommand, AdvancePaymentResultDto>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IAdvancePaymentService _advancePaymentService;

    public RequestAdvancePaymentCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService,
        IAdvancePaymentService advancePaymentService)
    {
        _context = context;
        _currentUserService = currentUserService;
        _advancePaymentService = advancePaymentService;
    }

    public async Task<AdvancePaymentResultDto> Handle(RequestAdvancePaymentCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUserService.UserId
            ?? throw new DomainException("Bạn cần đăng nhập để thực hiện thao tác.");

        var vendor = await _context.Vendors
            .FirstOrDefaultAsync(v => v.UserId == userId, cancellationToken)
            ?? throw new DomainException("Hồ sơ nhà cung cấp không tồn tại.");

        var contract = await _context.BookingContracts
            .FirstOrDefaultAsync(c => c.Id == request.ContractId, cancellationToken)
            ?? throw new NotFoundException("Hợp đồng không tồn tại.");

        if (contract.VendorId != vendor.Id)
        {
            throw new DomainException("Bạn không có quyền gửi yêu cầu thanh toán trước cho hợp đồng này.");
        }

        if (request.Amount <= 0)
        {
            throw new DomainException("Số tiền xin thanh toán trước phải lớn hơn 0 VNĐ.");
        }

        if (request.Amount > contract.ContractValue)
        {
            throw new DomainException($"Số tiền xin thanh toán trước không thể vượt quá giá trị hợp đồng ({contract.ContractValue:N0} đ).");
        }

        var notice = await _advancePaymentService.CreateRequestAsync(
            contract.Id,
            contract.ContractCode,
            vendor.Id,
            vendor.BrandName,
            request.Amount,
            request.Reason,
            request.BankName,
            request.BankAccountNumber,
            request.BankAccountName,
            request.Note
        );

        return new AdvancePaymentResultDto(
            contract.Id,
            contract.ContractCode,
            notice.Amount,
            notice.Reason,
            notice.BankName,
            notice.BankAccountNumber,
            notice.BankAccountName,
            notice.Note,
            notice.Status,
            notice.RequestedAt,
            $"Đã ghi nhận yêu cầu xin thanh toán trước {request.Amount:N0} đ cho hợp đồng {contract.ContractCode}. Hệ thống đã gửi thông báo đến Admin để xác nhận!"
        );
    }
}
