using Wedding.Domain.Common;
using Wedding.Domain.Enums;
using Wedding.Domain.Exceptions;

namespace Wedding.Domain.Entities;

public class ReviewEditRequest : BaseEntity
{
    public Guid ReviewId { get; private set; }
    public int AttemptNumber { get; private set; }
    public Guid RequestedById { get; private set; }
    public int BeforeRating { get; private set; }
    public string BeforeContent { get; private set; } = string.Empty;
    public string? BeforePhotosJson { get; private set; }
    public int? BeforeQualityRating { get; private set; }
    public int? BeforeServiceRating { get; private set; }
    public int? BeforePunctualityRating { get; private set; }
    public int? BeforeValueRating { get; private set; }
    public int AfterRating { get; private set; }
    public string AfterContent { get; private set; } = string.Empty;
    public string? AfterPhotosJson { get; private set; }
    public int? AfterQualityRating { get; private set; }
    public int? AfterServiceRating { get; private set; }
    public int? AfterPunctualityRating { get; private set; }
    public int? AfterValueRating { get; private set; }
    public ReviewEditStatus Status { get; private set; } = ReviewEditStatus.Pending;
    public DateTime RequestedAt { get; private set; }
    public Guid? ReviewedById { get; private set; }
    public DateTime? ReviewedAt { get; private set; }
    public string? RejectionReason { get; private set; }
    public Review Review { get; private set; } = null!;

    private ReviewEditRequest() { }

    public static ReviewEditRequest Create(Review review, int attemptNumber, Guid requestedById,
        int rating, string content, string? photosJson, int? qualityRating = null,
        int? serviceRating = null, int? punctualityRating = null, int? valueRating = null)
    {
        if (review.Status != ReviewStatus.Approved || requestedById != review.CustomerId ||
            attemptNumber is < 1 or > 3)
            throw new DomainException("Chỉ chủ sở hữu được gửi yêu cầu chỉnh sửa đánh giá đã duyệt.");
        Review.ValidateContent(rating, content, photosJson, qualityRating, serviceRating, punctualityRating, valueRating);
        return new ReviewEditRequest
        {
            ReviewId = review.Id,
            AttemptNumber = attemptNumber,
            RequestedById = requestedById,
            BeforeRating = review.Rating,
            BeforeContent = review.Content,
            BeforePhotosJson = review.PhotosJson,
            BeforeQualityRating = review.QualityRating,
            BeforeServiceRating = review.ServiceRating,
            BeforePunctualityRating = review.PunctualityRating,
            BeforeValueRating = review.ValueRating,
            AfterRating = rating,
            AfterContent = content.Trim(),
            AfterPhotosJson = photosJson,
            AfterQualityRating = qualityRating,
            AfterServiceRating = serviceRating,
            AfterPunctualityRating = punctualityRating,
            AfterValueRating = valueRating,
            RequestedAt = DateTime.UtcNow
        };
    }

    public void Approve(Guid moderatorId)
    {
        if (Status != ReviewEditStatus.Pending || moderatorId == Guid.Empty)
            throw new DomainException("Chỉ phiên bản đang chờ mới được duyệt.");
        Status = ReviewEditStatus.Approved;
        ReviewedById = moderatorId;
        ReviewedAt = DateTime.UtcNow;
        SetUpdated();
    }

    public void Reject(Guid moderatorId, string reason)
    {
        var cleanReason = reason.Trim();
        if (Status != ReviewEditStatus.Pending || moderatorId == Guid.Empty ||
            string.IsNullOrWhiteSpace(cleanReason) || cleanReason.Length > 1000)
            throw new DomainException("Lý do từ chối phiên bản đánh giá không hợp lệ.");
        Status = ReviewEditStatus.Rejected;
        ReviewedById = moderatorId;
        ReviewedAt = DateTime.UtcNow;
        RejectionReason = cleanReason;
        SetUpdated();
    }
}
