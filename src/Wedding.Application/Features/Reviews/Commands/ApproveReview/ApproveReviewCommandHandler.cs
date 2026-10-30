using MediatR;
using Microsoft.EntityFrameworkCore;
using Wedding.Application.Common.Interfaces;
using Wedding.Application.Features.Reviews.DTOs;
using Wedding.Domain.Enums;
using Wedding.Domain.Exceptions;

namespace Wedding.Application.Features.Reviews.Commands.ApproveReview;

public class ApproveReviewCommandHandler : IRequestHandler<ApproveReviewCommand, ReviewDto>
{
    private readonly IApplicationDbContext _db;
    public ApproveReviewCommandHandler(IApplicationDbContext db) => _db = db;
    public Task<ReviewDto> Handle(ApproveReviewCommand r, CancellationToken ct) => _db.ExecuteSerializableAsync(token => ApproveAsync(r, token), ct);
    private async Task<ReviewDto> ApproveAsync(ApproveReviewCommand r, CancellationToken ct)
    {
        var review = await _db.Reviews.Include(x => x.Customer).Include(x => x.Vendor).Include(x => x.Listing)
            .Include(x => x.ComboTemplate).Include(x => x.BookingContract)
            .FirstOrDefaultAsync(x => x.Id == r.ReviewId, ct) ?? throw new NotFoundException("Đánh giá không tồn tại.");
        if (await ReviewRules.HasDisputedEvidenceAsync(_db, review, ct))
            throw new DomainException("Tạm dừng duyệt đánh giá trong thời gian tranh chấp.");
        review.Approve(); await _db.SaveChangesAsync(ct);
        if (review.TargetType == ReviewTargetType.Shop) await ReviewRules.RefreshVendorAsync(_db, review.VendorId, ct);
        await _db.SaveChangesAsync(ct); return ReviewRules.Dto(review);
    }
}
