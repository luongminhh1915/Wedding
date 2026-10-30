using MediatR;
using Microsoft.EntityFrameworkCore;
using Wedding.Application.Common.Interfaces;
using Wedding.Application.Features.Reviews.DTOs;
using Wedding.Domain.Exceptions;
using Wedding.Domain.Enums;

namespace Wedding.Application.Features.Reviews.Commands.RejectReview;

public class RejectReviewCommandHandler : IRequestHandler<RejectReviewCommand, ReviewDto>
{
    private readonly IApplicationDbContext _db;
    public RejectReviewCommandHandler(IApplicationDbContext db) => _db = db;
    public Task<ReviewDto> Handle(RejectReviewCommand r, CancellationToken ct) => _db.ExecuteSerializableAsync(token => RejectAsync(r, token), ct);
    private async Task<ReviewDto> RejectAsync(RejectReviewCommand r, CancellationToken ct)
    {
        var review = await _db.Reviews.Include(x => x.Customer).Include(x => x.Vendor).Include(x => x.Listing)
            .Include(x => x.ComboTemplate).Include(x => x.BookingContract)
            .FirstOrDefaultAsync(x => x.Id == r.ReviewId, ct) ?? throw new NotFoundException("Đánh giá không tồn tại.");
        if (string.IsNullOrWhiteSpace(r.Reason)) throw new DomainException("Cần lý do từ chối rõ ràng.");
        review.Reject(r.Reason.Trim()); await _db.SaveChangesAsync(ct);
        if (review.TargetType == ReviewTargetType.Shop) await ReviewRules.RefreshVendorAsync(_db, review.VendorId, ct);
        await _db.SaveChangesAsync(ct); return ReviewRules.Dto(review);
    }
}
