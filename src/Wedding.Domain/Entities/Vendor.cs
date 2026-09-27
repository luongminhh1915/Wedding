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
    public string? Bio { get; private set; }
    public string? LogoUrl { get; private set; }
    public string? CoverImageUrl { get; private set; }
    public decimal CommissionRate { get; private set; } // Khung theo FM-001
    public decimal RatingAvg { get; private set; } = 5.0m;
    public int TotalReviews { get; private set; } = 0;
    public bool IsActive { get; private set; } = true;
    public bool IsVerified { get; private set; } = false;

    // Navigation Properties
    public User User { get; private set; } = null!;
    public ICollection<Listing> Listings { get; private set; } = new List<Listing>();
    public ICollection<Lead> Leads { get; private set; } = new List<Lead>();
    public ICollection<BookingContract> Contracts { get; private set; } = new List<BookingContract>();
    public ICollection<Commission> Commissions { get; private set; } = new List<Commission>();
    public ICollection<Review> Reviews { get; private set; } = new List<Review>();

    private Vendor() { } // Dành cho EF Core

    public static Vendor Create(Guid userId, string brandName, string slug, string contactPerson, 
        string hotline, string city, decimal commissionRate)
    {
        if (string.IsNullOrWhiteSpace(brandName))
            throw new DomainException("Tên thương hiệu không được để trống.");
        if (string.IsNullOrWhiteSpace(hotline))
            throw new DomainException("Hotline đối tác không được để trống.");
        if (commissionRate < 0.03m || commissionRate > 0.15m)
            throw new DomainException("Tỷ lệ hoa hồng đối tác phải nằm trong khoảng 3% đến 15%.");

        return new Vendor
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            BrandName = brandName.Trim(),
            Slug = slug.Trim().ToLowerInvariant(),
            ContactPerson = contactPerson.Trim(),
            Hotline = hotline.Trim(),
            City = city.Trim(),
            CommissionRate = commissionRate,
            RatingAvg = 5.0m,
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

    public void UpdateRating(decimal newRatingAvg, int totalReviews)
    {
        RatingAvg = Math.Clamp(newRatingAvg, 1.0m, 5.0m);
        TotalReviews = totalReviews;
        SetUpdated();
    }

    public void SetVerified(bool isVerified)
    {
        IsVerified = isVerified;
        SetUpdated();
    }
}
