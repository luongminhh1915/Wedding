using MediatR;
using Microsoft.EntityFrameworkCore;
using Wedding.Application.Common.Interfaces;
using Wedding.Domain.Exceptions;

namespace Wedding.Application.Features.Listings.Commands.SubmitForApproval;

public record SubmitForApprovalCommand(Guid ListingId) : IRequest;

public class SubmitForApprovalCommandHandler : IRequestHandler<SubmitForApprovalCommand>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public SubmitForApprovalCommandHandler(IApplicationDbContext context, ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task Handle(SubmitForApprovalCommand request, CancellationToken cancellationToken)
    {
        var userId = _currentUserService.UserId
            ?? throw new DomainException("Không xác định được người dùng.");

        var vendor = await _context.Vendors
            .FirstOrDefaultAsync(v => v.UserId == userId, cancellationToken)
            ?? throw new DomainException("Hồ sơ nhà cung cấp không tồn tại.");

        var listing = await _context.Listings
            .FirstOrDefaultAsync(l => l.Id == request.ListingId && l.VendorId == vendor.Id, cancellationToken)
            ?? throw new NotFoundException("Bài đăng", request.ListingId);

        // Chỉ cho phép nộp khi đang ở Draft hoặc Rejected
        if (listing.Status != Domain.Enums.ListingStatus.Draft && listing.Status != Domain.Enums.ListingStatus.Rejected)
            throw new DomainException("Bài đăng đã được nộp duyệt hoặc đang hoạt động, không thể nộp lại.");

        listing.SubmitForApproval();
        await _context.SaveChangesAsync(cancellationToken);
    }
}
