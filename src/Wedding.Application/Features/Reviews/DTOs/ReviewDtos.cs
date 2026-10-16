namespace Wedding.Application.Features.Reviews.DTOs;

public record ReviewDto(
    Guid Id,
    Guid? BookingContractId,
    string? ContractCode,
    Guid ListingId,
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
    string? RejectionReason = null
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
    DateTime? WeddingDate
);
