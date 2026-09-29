using MediatR;
using Microsoft.EntityFrameworkCore;
using Wedding.Application.Common.Interfaces;
using Wedding.Domain.Enums;
using Wedding.Domain.Exceptions;

namespace Wedding.Application.Features.Listings.Commands.ToggleListingStatus;

public record ToggleListingStatusCommand(Guid ListingId, ListingStatus NewStatus) : IRequest;

public class ToggleListingStatusCommandHandler : IRequestHandler<ToggleListingStatusCommand>
{
    private readonly IApplicationDbContext _context;

    public ToggleListingStatusCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task Handle(ToggleListingStatusCommand request, CancellationToken cancellationToken)
    {
        var listing = await _context.Listings.FirstOrDefaultAsync(l => l.Id == request.ListingId, cancellationToken);
        if (listing == null)
            throw new NotFoundException("Bài đăng không tồn tại.");

        listing.SetStatus(request.NewStatus);
        await _context.SaveChangesAsync(cancellationToken);
    }
}
