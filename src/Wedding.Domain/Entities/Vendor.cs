using Wedding.Domain.Common;
using Wedding.Domain.Exceptions;

namespace Wedding.Domain.Entities;

public class Vendor : BaseEntity
{
    public Guid UserId { get; private set; } // BR-001: 1 Vendor = 1 Tài khoản VendorOwner
    public string BrandName { get; private set; } = string.Empty;
    public string Slug { get; private set; } = string.Empty;
    public string ContactPerson { get; private set; } = string.Empty;
    public string Hotline { get; private set; } = string.Empty;
    public string? Address { get; private set; }
    public string City { get; private set; } = string.Empty;
    // Null on legacy rows: use City for display, without rewriting address or service coverage.
    public string? OperatingRegions { get; private set; }
    public string? Bio { get; private set; }
    public string? LogoUrl { get; private set; }
    public string? CoverImageUrl { get; private set; }
    // Positive legacy shop-wide rate is retained until an explicitly approved transition.
    // Zero is used only by the new category-inheriting registration path.
    public decimal CommissionRate { get; private set; }
    public DateTime? PartnershipPolicyAcceptedAt { get; private set; }
    public string? PartnershipPolicyVersion { get; private set; }
    public decimal RatingAvg { get; private set; }
    public int TotalReviews { get; private set; } = 0;
    public bool IsActive { get; private set; } = true;
    public bool IsVerified { get; private set; } = false;
    public bool IsTopRated { get; private set; }
    public bool AllowInCombos { get; private set; }
    public decimal MaxComboDiscount { get; private set; }

    // Navigation Properties
    public User User { get; private set; } = null!;
    public ICollection<Listing> Listings { get; private set; } = new List<Listing>();
    public ICollection<Lead> Leads { get; private set; } = new List<Lead>();
    public ICollection<BookingContract> Contracts { get; private set; } = new List<BookingContract>();
    public ICollection<Commission> Commissions { get; private set; } = new List<Commission>();
    public ICollection<Review> Reviews { get; private set; } = new List<Review>();
    public ICollection<VendorBusinessGroup> BusinessGroups { get; private set; } = new List<VendorBusinessGroup>();

    private Vendor() { } // Dành cho EF Core

    public static Vendor Create(Guid userId, string brandName, string slug, string contactPerson, 
        string hotline, string city, decimal commissionRate)
    {
        if (commissionRate < 0.03m || commissionRate > 0.12m)
            throw new DomainException("Tỷ lệ hoa hồng đối tác phải nằm trong khoảng 3% đến 12%.");
        return CreateCore(userId, brandName, slug, contactPerson, hotline, city, commissionRate);
    }

    public static Vendor CreateWithCategoryDefaults(Guid userId, string brandName, string slug,
        string contactPerson, string hotline, string city, string acceptedPolicyVersion)
    {
        if (string.IsNullOrWhiteSpace(acceptedPolicyVersion) || acceptedPolicyVersion.Length > 50)
            throw new DomainException("Phiên bản chính sách hợp tác không hợp lệ.");
        var vendor = CreateCore(userId, brandName, slug, contactPerson, hotline, city, 0m);
        vendor.PartnershipPolicyAcceptedAt = DateTime.UtcNow;
        vendor.PartnershipPolicyVersion = acceptedPolicyVersion.Trim();
        return vendor;
    }

    /// <summary>Admin can prepare a shop, but cannot record the Vendor's own policy acknowledgement.</summary>
    public static Vendor CreateForAdministration(Guid userId, string brandName, string slug,
        string contactPerson, string hotline, string city)
        => CreateCore(userId, brandName, slug, contactPerson, hotline, city, 0m, allowMissingHotline: true);

    private static Vendor CreateCore(Guid userId, string brandName, string slug, string contactPerson,
        string hotline, string city, decimal commissionRate, bool allowMissingHotline = false)
    {
        if (string.IsNullOrWhiteSpace(brandName))
            throw new DomainException("Tên thương hiệu không được để trống.");
        if (!allowMissingHotline && string.IsNullOrWhiteSpace(hotline))
            throw new DomainException("Hotline đối tác không được để trống.");
        return new Vendor
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            BrandName = brandName.Trim(),
            Slug = slug.Trim().ToLowerInvariant(),
            ContactPerson = contactPerson.Trim(),
            Hotline = hotline?.Trim() ?? "",
            City = city.Trim(),
            CommissionRate = commissionRate,
            RatingAvg = 0m,
            TotalReviews = 0,
            IsActive = true,
            IsVerified = false,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void UpdateProfile(string brandName, string contactPerson, string hotline, 
        string? address, string city, string? bio, string? logoUrl, string? coverImageUrl)
    {
        BrandName = brandName.Trim();
        ContactPerson = contactPerson.Trim();
        Hotline = hotline.Trim();
        Address = address;
        City = city.Trim();
        Bio = bio;
        LogoUrl = logoUrl;
        CoverImageUrl = coverImageUrl;
        SetUpdated();
    }

    public void SetOperatingAreas(IReadOnlyList<string>? operatingRegions, bool nationwide, string? legacyCity = null)
    {
        var normalized = OperatingAreaRules.Normalize(legacyCity ?? City, operatingRegions, nationwide);
        if (OperatingRegions == normalized.StorageValue) return;
        OperatingRegions = normalized.StorageValue;
        SetUpdated();
    }

    public void UpdateRating(decimal newRatingAvg, int totalReviews)
    {
        RatingAvg = totalReviews == 0 ? 0m : Math.Clamp(newRatingAvg, 1.0m, 5.0m);
        TotalReviews = totalReviews;
        IsTopRated = totalReviews >= 50 && RatingAvg >= 4.8m;
        SetUpdated();
    }

    public void SetVerified(bool isVerified)
    {
        IsVerified = isVerified;
        SetUpdated();
    }

    public void ConfigureComboParticipation(bool allow, decimal maxDiscount)
    {
        if (maxDiscount < 0 || maxDiscount > 0.30m) throw new DomainException("Mức giảm combo phải từ 0 đến 30%.");
        AllowInCombos = allow; MaxComboDiscount = maxDiscount; SetUpdated();
    }
}
