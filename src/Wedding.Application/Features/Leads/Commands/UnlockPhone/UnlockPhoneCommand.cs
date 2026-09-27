using MediatR;
using Microsoft.EntityFrameworkCore;
using Wedding.Application.Common.Interfaces;
using Wedding.Domain.Exceptions;

namespace Wedding.Application.Features.Leads.Commands.UnlockPhone;

public record UnlockPhoneCommand(Guid LeadId) : IRequest<bool>;

public class UnlockPhoneCommandHandler : IRequestHandler<UnlockPhoneCommand, bool>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public UnlockPhoneCommandHandler(IApplicationDbContext context, ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<bool> Handle(UnlockPhoneCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUserService.UserId
            ?? throw new DomainException("Bạn cần đăng nhập để thực hiện thao tác này.");

        var lead = await _context.Leads
            .FirstOrDefaultAsync(l => l.Id == request.LeadId, cancellationToken)
            ?? throw new NotFoundException("Yêu cầu tư vấn không tồn tại.");

        // Chỉ khách hàng sở hữu Lead mới có quyền mở khóa SĐT cho Vendor (BR-002)
        if (lead.CustomerId != userId)
            throw new DomainException("Bạn không có quyền mở khóa số điện thoại cho yêu cầu tư vấn này.");

        lead.UnlockPhone();
        await _context.SaveChangesAsync(cancellationToken);

        return true;
    }
}
