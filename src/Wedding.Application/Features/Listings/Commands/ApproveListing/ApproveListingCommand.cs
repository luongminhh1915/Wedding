using MediatR;
using Microsoft.EntityFrameworkCore;
using Wedding.Application.Common.Interfaces;
using Wedding.Domain.Exceptions;

namespace Wedding.Application.Features.Listings.Commands.ApproveListing;

public record ApproveListingCommand(Guid ListingId) : IRequest;

public class ApproveListingCommandHandler : IRequestHandler<ApproveListingCommand>
{
    private readonly IApplicationDbContext _context;

    public ApproveListingCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task Handle(ApproveListingCommand request, CancellationToken cancellationToken)
    {
        var listing = await _context.Listings
            .FirstOrDefaultAsync(l => l.Id == request.ListingId, cancellationToken)
            ?? throw new NotFoundException("Bài đăng", request.ListingId);

        if (listing.Status != Domain.Enums.ListingStatus.PendingApproval)
            throw new DomainException("Bài đăng không ở trạng thái chờ duyệt.");

        // BR-008: Moderator duyệt bài → chuyển sang Active
        listing.Approve();
        await _context.SaveChangesAsync(cancellationToken);
    }
}
