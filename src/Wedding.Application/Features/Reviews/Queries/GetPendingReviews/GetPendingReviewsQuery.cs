using MediatR;
using Microsoft.EntityFrameworkCore;
using Wedding.Application.Common.Interfaces;
using Wedding.Application.Features.Reviews;
using Wedding.Application.Features.Reviews.DTOs;
using Wedding.Domain.Enums;

namespace Wedding.Application.Features.Reviews.Queries.GetPendingReviews;

public record GetPendingReviewsQuery : IRequest<List<ReviewDto>>;

public class GetPendingReviewsQueryHandler : IRequestHandler<GetPendingReviewsQuery, List<ReviewDto>>
{
    private readonly IApplicationDbContext _context;

    public GetPendingReviewsQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<ReviewDto>> Handle(GetPendingReviewsQuery request, CancellationToken cancellationToken)
    {
        var reviews = await _context.Reviews
            .Include(r => r.Customer)
            .Include(r => r.Vendor)
            .Include(r => r.Listing)
            .Include(r => r.ComboTemplate)
            .Include(r => r.BookingContract)
            .Where(r => r.Status == ReviewStatus.Pending)
            .OrderBy(r => r.CreatedAt)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return reviews.Select(review => ReviewRules.Dto(review)).ToList();
    }
}
