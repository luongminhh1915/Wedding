using Wedding.Domain.Common;
using Wedding.Domain.Enums;
using Wedding.Domain.Exceptions;
using System.Text.Json;

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
    public int? QualityRating { get; private set; }
    public int? ServiceRating { get; private set; }
    public int? PunctualityRating { get; private set; }
    public int? ValueRating { get; private set; }
    public string? ModerationFlag { get; private set; }
    public string? RejectionReason { get; private set; }
    public decimal AverageRating => QualityRating.HasValue ? (QualityRating.Value + ServiceRating!.Value + PunctualityRating!.Value + ValueRating!.Value) / 4m : Rating;

    // Navigation Properties
    public BookingContract? BookingContract { get; private set; }
    public Listing Listing { get; private set; } = null!;
    public User Customer { get; private set; } = null!;
    public Vendor Vendor { get; private set; } = null!;

    private Review() { } // Dành cho EF Core

    public static Review Create(Guid? contractId, Guid listingId, Guid customerId, Guid vendorId, 
        int rating, string content, string? photosJson, bool isVerifiedBuyer = false,
        int? qualityRating = null, int? serviceRating = null, int? punctualityRating = null, int? valueRating = null)
    {
        if (rating < 1 || rating > 5)
            throw new DomainException("Điểm đánh giá phải từ 1 đến 5 sao.");
        if (string.IsNullOrWhiteSpace(content) || content.Trim().Length is < 30 or > 3000)
            throw new DomainException("Nội dung đánh giá cần từ 30 đến 3000 ký tự.");
        var criteria = new[] { qualityRating, serviceRating, punctualityRating, valueRating };
        if (criteria.Any(x => x.HasValue) && criteria.Any(x => !x.HasValue || x < 1 || x > 5)) throw new DomainException("Cần đủ 4 tiêu chí, mỗi tiêu chí từ 1 đến 5 sao.");
        if (!string.IsNullOrWhiteSpace(photosJson))
        {
            try
            {
                var photos = JsonSerializer.Deserialize<List<string>>(photosJson);
                if (photos == null || photos.Count > 5 || photos.Any(p => string.IsNullOrWhiteSpace(p) || p.Length > 700 || (!p.StartsWith("/uploads/", StringComparison.Ordinal) && (!Uri.TryCreate(p, UriKind.Absolute, out var uri) || uri.Scheme != "https")))) throw new DomainException("Tối đa 5 ảnh HTTPS hoặc ảnh tải lên hợp lệ.");
            }
            catch (JsonException) { throw new DomainException("Danh sách ảnh không đúng JSON."); }
        }

        return new Review
        {
            Id = Guid.NewGuid(),
            BookingContractId = contractId,
            ListingId = listingId,
            CustomerId = customerId,
            VendorId = vendorId,
            Rating = rating,
            QualityRating = qualityRating, ServiceRating = serviceRating, PunctualityRating = punctualityRating, ValueRating = valueRating,
            ModerationFlag = new[] { "địt", "đụ má", "fuck" }.Any(word => content.Contains(word, StringComparison.OrdinalIgnoreCase)) ? "SensitiveLanguage: moderator must review context" : null,
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

    public void Reject(string? reason = null)
    {
        Status = ReviewStatus.Rejected;
        RejectionReason = reason;
        ModeratedAt = DateTime.UtcNow;
        SetUpdated();
    }

    public void AddVendorReply(string reply)
    {
        if (string.IsNullOrWhiteSpace(reply) || reply.Trim().Length > 2000 || Status != ReviewStatus.Approved)
            throw new DomainException("Nội dung phản hồi không được để trống.");

        VendorReply = reply.Trim();
        VendorRepliedAt = DateTime.UtcNow;
        SetUpdated();
    }
}
