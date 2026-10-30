namespace Wedding.Application.Features.Reviews.DTOs;

// Public reviews deliberately have no customer account ID or contract identifiers.
// The richer ReviewDto below remains for the submitting customer and moderation endpoints.
public record PublicReviewDto(
    Guid Id,
    Guid? ListingId,
    string? ListingTitle,
    string CustomerName,
    Guid VendorId,
    string VendorBrandName,
    int Rating,
    string Content,
    string? PhotosJson,
    bool IsVerifiedBuyer,
    string Status,
    string? VendorReply,
    DateTime? VendorRepliedAt,
    DateTime CreatedAt,
    int? QualityRating = null,
    int? ServiceRating = null,
    int? PunctualityRating = null,
    int? ValueRating = null,
    decimal AverageRating = 0,
    string TargetType = "Service",
    Guid? ComboTemplateId = null
);

public record ReviewDto(
    Guid Id,
    Guid? BookingContractId,
    string? ContractCode,
    Guid? ListingId,
    string ListingTitle,
    Guid CustomerId,
    string CustomerName,
    Guid VendorId,
    string VendorBrandName,
    int Rating,
    string Content,
    string? PhotosJson,
    bool IsVerifiedBuyer, // BR-009
    string Status,        // Pending, Approved, Rejected
    string? VendorReply,
    DateTime? VendorRepliedAt,
    DateTime CreatedAt,
    int? QualityRating = null,
    int? ServiceRating = null,
    int? PunctualityRating = null,
    int? ValueRating = null,
    decimal AverageRating = 0,
    string? ModerationFlag = null,
    DateTime? ModerationDeadline = null,
    string? RejectionReason = null,
    string TargetType = "Service",
    Guid? ComboOrderId = null,
    Guid? ComboTemplateId = null,
    string? ComboTemplateName = null,
    string? EditStatus = null,
    int EditAttemptCount = 0,
    bool CanEdit = false
);

public record SubmitReviewCommand(
    Guid? BookingContractId,
    Guid ListingId,
    int Rating,
    string Content,
    string? PhotosJson = null,
    int? QualityRating = null,
    int? ServiceRating = null,
    int? PunctualityRating = null,
    int? ValueRating = null
) : MediatR.IRequest<ReviewDto>;

public record VendorReplyReviewCommand(
    Guid ReviewId,
    string Reply
) : MediatR.IRequest<ReviewDto>;

public record ApproveReviewCommand(
    Guid ReviewId
) : MediatR.IRequest<ReviewDto>;

public record RejectReviewCommand(
    Guid ReviewId,
    string? Reason = null
) : MediatR.IRequest<ReviewDto>;

public record EligibleContractDto(
    Guid ContractId,
    string ContractCode,
    Guid VendorId,
    string VendorBrandName,
    Guid? ListingId,
    string? ListingTitle,
    DateTime? WeddingDate,
    Guid? ComboOrderId = null
);

public record SubmitShopReviewCommand(
    Guid? BookingContractId,
    Guid? ComboOrderId,
    int Rating,
    string Content,
    string? PhotosJson = null
) : MediatR.IRequest<ReviewDto>;

public record SubmitComboReviewCommand(
    Guid ComboOrderId,
    int Rating,
    string Content,
    string? PhotosJson = null
) : MediatR.IRequest<ReviewDto>;

// Optional null/omitted fields preserve their current values. PhotosJson = "[]"
// explicitly clears photo references; criteria still obey the existing domain rules.
public record SubmitReviewEditRequestCommand(
    Guid ReviewId,
    int Rating,
    string Content,
    string? PhotosJson = null,
    int? QualityRating = null,
    int? ServiceRating = null,
    int? PunctualityRating = null,
    int? ValueRating = null
) : MediatR.IRequest<ReviewDto>;

public record ModerateReviewEditRequestCommand(Guid EditRequestId, bool Approve, string? Reason = null)
    : MediatR.IRequest<ReviewDto>;

public record EligibleReviewTargetDto(
    string TargetType,
    Guid TransactionId,
    string TransactionCode,
    Guid TargetId,
    string TargetName,
    Guid? BookingContractId,
    Guid? ComboOrderId,
    Guid? ListingId
);

public record ReviewRatingSummaryDto(decimal AverageRating, int TotalReviews, Dictionary<int, int> RatingDistribution);
public record ReviewPageDto(
    ReviewRatingSummaryDto Summary,
    List<PublicReviewDto> Reviews,
    int Page,
    int PageSize,
    int FilteredCount,
    int TotalPages
);

public record GetIndependentReviewsQuery(string TargetType, Guid TargetId, int Page = 1, int PageSize = 20,
    int? Rating = null, bool? VerifiedOnly = null, string Sort = "newest") : MediatR.IRequest<ReviewPageDto>;

public record PendingReviewEditDto(Guid Id, Guid ReviewId, int AttemptNumber, string BeforeContent,
    int BeforeRating, string AfterContent, int AfterRating, DateTime RequestedAt, ReviewDto Review,
    string? BeforePhotosJson = null, string? AfterPhotosJson = null,
    int? BeforeQualityRating = null, int? AfterQualityRating = null,
    int? BeforeServiceRating = null, int? AfterServiceRating = null,
    int? BeforePunctualityRating = null, int? AfterPunctualityRating = null,
    int? BeforeValueRating = null, int? AfterValueRating = null);
