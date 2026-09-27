using MediatR;
using Microsoft.EntityFrameworkCore;
using Wedding.Application.Common.Interfaces;
using Wedding.Domain.Enums;
using Wedding.Domain.Exceptions;

namespace Wedding.Application.Features.Leads.Commands.AcceptLead;

public record AcceptLeadCommand(Guid LeadId) : IRequest<bool>;

public class AcceptLeadCommandHandler : IRequestHandler<AcceptLeadCommand, bool>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public AcceptLeadCommandHandler(IApplicationDbContext context, ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<bool> Handle(AcceptLeadCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUserService.UserId
            ?? throw new DomainException("Bạn cần đăng nhập để thực hiện thao tác này.");

        var vendor = await _context.Vendors
            .FirstOrDefaultAsync(v => v.UserId == userId, cancellationToken)
            ?? throw new DomainException("Hồ sơ nhà cung cấp không tồn tại.");

        var lead = await _context.Leads
            .FirstOrDefaultAsync(l => l.Id == request.LeadId, cancellationToken)
            ?? throw new NotFoundException("Yêu cầu tư vấn không tồn tại.");

        if (lead.VendorId != vendor.Id)
            throw new DomainException("Bạn không phải nhà cung cấp được chỉ định cho yêu cầu tư vấn này.");

        // BR-003: Tiếp nhận lead và ghi nhận thời gian tiếp nhận
        lead.Accept();
        await _context.SaveChangesAsync(cancellationToken);

        return true;
    }
}
