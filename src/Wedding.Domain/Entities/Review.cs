using Wedding.Domain.Common;
using Wedding.Domain.Enums;
using Wedding.Domain.Exceptions;

namespace Wedding.Domain.Entities;

public class Review : BaseEntity
{
    public Guid? BookingContractId { get; private set; }
    public Guid ListingId { get; private set; }
    public Guid CustomerId { get; private set; }
    public Guid VendorId { get; private set; }
    public int Rating { get; private set; } // 1 - 5 sao
    public string Content { get; private set; } = string.Empty;
    public string? PhotosJson { get; private set; } // Danh sách link ảnh review
    public bool IsVerifiedBuyer { get; private set; } = false; // BR-009
    public ReviewStatus Status { get; private set; } = ReviewStatus.Pending;
    public string? VendorReply { get; private set; }
    public DateTime? VendorRepliedAt { get; private set; }
    public DateTime? ModeratedAt { get; private set; }

    // Navigation Properties
    public BookingContract? BookingContract { get; private set; }
    public Listing Listing { get; private set; } = null!;
    public User Customer { get; private set; } = null!;
    public Vendor Vendor { get; private set; } = null!;

    private Review() { } // Dành cho EF Core

    public static Review Create(Guid? contractId, Guid listingId, Guid customerId, Guid vendorId, 
        int rating, string content, string? photosJson, bool isVerifiedBuyer = false)
    {
        if (rating < 1 || rating > 5)
            throw new DomainException("Điểm đánh giá phải từ 1 đến 5 sao.");
        if (string.IsNullOrWhiteSpace(content))
            throw new DomainException("Nội dung đánh giá không được để trống.");

        return new Review
        {
            Id = Guid.NewGuid(),
            BookingContractId = contractId,
            ListingId = listingId,
            CustomerId = customerId,
            VendorId = vendorId,
            Rating = rating,
            Content = content.Trim(),
            PhotosJson = photosJson,
            IsVerifiedBuyer = isVerifiedBuyer, // BR-009
            Status = ReviewStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void Approve()
    {
        Status = ReviewStatus.Approved;
        ModeratedAt = DateTime.UtcNow;
        SetUpdated();
    }

    public void Reject()
    {
        Status = ReviewStatus.Rejected;
        ModeratedAt = DateTime.UtcNow;
        SetUpdated();
    }

    public void AddVendorReply(string reply)
    {
        if (string.IsNullOrWhiteSpace(reply))
            throw new DomainException("Nội dung phản hồi không được để trống.");

        VendorReply = reply.Trim();
        VendorRepliedAt = DateTime.UtcNow;
        SetUpdated();
    }
}
