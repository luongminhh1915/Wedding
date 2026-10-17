using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Wedding.Application.Common.Interfaces;
using Wedding.Domain.Enums;
using Wedding.Domain.Exceptions;
using Wedding.Application.Features.CustomerOrders;

namespace Wedding.Application.Features.Contracts.Commands.RejectContract;

public record RejectContractCommand(Guid ContractId, string Reason) : IRequest<bool>;

public class RejectContractCommandValidator : AbstractValidator<RejectContractCommand>
{
    public RejectContractCommandValidator()
    {
        RuleFor(x => x.ContractId).NotEmpty().WithMessage("Mã hợp đồng không được để trống.");
        RuleFor(x => x.Reason).NotEmpty().WithMessage("Lý do báo sai lệch không được để trống.").MaximumLength(500);
    }
}

public class RejectContractCommandHandler : IRequestHandler<RejectContractCommand, bool>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public RejectContractCommandHandler(IApplicationDbContext context, ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public Task<bool> Handle(RejectContractCommand request, CancellationToken cancellationToken) => _context.ExecuteSerializableAsync(ct => RejectAsync(request, ct), cancellationToken);
    private async Task<bool> RejectAsync(RejectContractCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUserService.UserId
            ?? throw new DomainException("Bạn cần đăng nhập để thao tác.");

        var contract = await _context.BookingContracts
            .FirstOrDefaultAsync(c => c.Id == request.ContractId, cancellationToken)
            ?? throw new NotFoundException("Hợp đồng không tồn tại.");

        if (contract.CustomerId != userId)
        {
            throw new DomainException("Chỉ khách hàng của hợp đồng mới có quyền báo sai lệch.");
        }

        if (contract.Status != ContractStatus.PendingVerification)
        {
            throw new DomainException("Chỉ có thể báo sai lệch với hợp đồng đang chờ xác nhận.");
        }

        var lead = await _context.Leads.SingleAsync(l => l.Id == contract.LeadId, cancellationToken);
        CustomerOrderJourney.EnsureActiveLead(lead);
        if (await CustomerOrderJourney.HasMoneyAsync(_context, new[] { lead.Id }, cancellationToken))
            throw new DomainException("Đã có cọc/chuyển khoản hoặc nghĩa vụ tài chính. Hãy gửi đề nghị thay đổi để NCC/Tài chính phối hợp, không trả về nháp trực tiếp.");

        // Chuyển hợp đồng về trạng thái Draft để NCC chỉnh sửa lại
        contract.RejectByCustomer(request.Reason);
        await _context.SaveChangesAsync(cancellationToken);

        return true;
    }
}
