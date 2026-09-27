using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Wedding.Application.Common.Interfaces;
using Wedding.Domain.Exceptions;

namespace Wedding.Application.Features.Listings.Commands.RejectListing;

public record RejectListingCommand(Guid ListingId, string Reason) : IRequest;

public class RejectListingCommandValidator : AbstractValidator<RejectListingCommand>
{
    public RejectListingCommandValidator()
    {
        RuleFor(x => x.ListingId).NotEmpty();
        RuleFor(x => x.Reason)
            .NotEmpty().WithMessage("Phải cung cấp lý do từ chối bài đăng (BR-008).")
            .MaximumLength(1000).WithMessage("Lý do từ chối không được vượt quá 1000 ký tự.");
    }
}

public class RejectListingCommandHandler : IRequestHandler<RejectListingCommand>
{
    private readonly IApplicationDbContext _context;

    public RejectListingCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task Handle(RejectListingCommand request, CancellationToken cancellationToken)
    {
        var listing = await _context.Listings
            .FirstOrDefaultAsync(l => l.Id == request.ListingId, cancellationToken)
            ?? throw new NotFoundException("Bài đăng", request.ListingId);

        if (listing.Status != Domain.Enums.ListingStatus.PendingApproval)
            throw new DomainException("Bài đăng không ở trạng thái chờ duyệt.");

        // BR-008: Moderator từ chối phải kèm lý do, SLA 24h
        listing.Reject(request.Reason);
        await _context.SaveChangesAsync(cancellationToken);
    }
}
