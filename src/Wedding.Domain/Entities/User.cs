using Wedding.Domain.Common;
using Wedding.Domain.Enums;
using Wedding.Domain.Exceptions;

namespace Wedding.Domain.Entities;

public class User : BaseEntity
{
    public string FullName { get; private set; } = string.Empty;
    public string Email { get; private set; } = string.Empty;
    public string PhoneNumber { get; private set; } = string.Empty;
    public string PasswordHash { get; private set; } = string.Empty;
    public UserRole Role { get; private set; }
    public string? AvatarUrl { get; private set; }
    public bool IsActive { get; private set; } = true;
    public DateTime? LastLoginAt { get; private set; }

    // Navigation Properties
    public Vendor? Vendor { get; private set; }
    public ICollection<Lead> Leads { get; private set; } = new List<Lead>();
    public ICollection<BookingContract> Contracts { get; private set; } = new List<BookingContract>();
    public ICollection<Review> Reviews { get; private set; } = new List<Review>();
    public ICollection<WeddingInvitation> WeddingInvitations { get; private set; } = new List<WeddingInvitation>();
    public ICollection<BudgetItem> BudgetItems { get; private set; } = new List<BudgetItem>();
    public ICollection<ChecklistTask> ChecklistTasks { get; private set; } = new List<ChecklistTask>();

    private User() { } // Dành cho EF Core

    public static User Create(string fullName, string email, string phoneNumber, string passwordHash, UserRole role)
    {
        if (string.IsNullOrWhiteSpace(fullName))
            throw new DomainException("Họ và tên không được để trống.");
        if (string.IsNullOrWhiteSpace(email))
            throw new DomainException("Email không được để trống.");
        if (string.IsNullOrWhiteSpace(phoneNumber))
            throw new DomainException("Số điện thoại không được để trống.");

        return new User
        {
            Id = Guid.NewGuid(),
            FullName = fullName.Trim(),
            Email = email.Trim().ToLowerInvariant(),
            PhoneNumber = phoneNumber.Trim(),
            PasswordHash = passwordHash,
            Role = role,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void UpdateProfile(string fullName, string? avatarUrl)
    {
        if (string.IsNullOrWhiteSpace(fullName))
            throw new DomainException("Họ và tên không được để trống.");

        FullName = fullName.Trim();
        AvatarUrl = avatarUrl;
        SetUpdated();
    }

    public void UpdatePassword(string newPasswordHash)
    {
        PasswordHash = newPasswordHash;
        SetUpdated();
    }

    public void RecordLogin()
    {
        LastLoginAt = DateTime.UtcNow;
    }

    public void SetStatus(bool isActive)
    {
        IsActive = isActive;
        SetUpdated();
    }
}
