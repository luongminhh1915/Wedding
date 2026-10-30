using Wedding.Domain.Common;
using Wedding.Domain.Enums;
using Wedding.Domain.Exceptions;

namespace Wedding.Domain.Entities;

public class User : BaseEntity
{
    public string FullName { get; private set; } = string.Empty;
    private string? _email;
    // Social identities may omit contact email. Preserve the existing DTO/string API for callers.
    public string Email { get => _email ?? string.Empty; private set => _email = value; }
    // Stable login identifier for administratively provisioned vendors without required contact data.
    public string? LoginName { get; private set; }
    public string NotificationRecipient => string.IsNullOrWhiteSpace(_email) ? Id.ToString() : _email;
    public string PhoneNumber { get; private set; } = string.Empty;
    public string PasswordHash { get; private set; } = string.Empty;
    // Credential, contact, and status changes revoke previously issued access tokens.
    public int SecurityVersion { get; private set; }
    public UserRole Role { get; private set; }
    public string? AvatarUrl { get; private set; }
    public bool IsActive { get; private set; } = true;
    public DateTime? LastLoginAt { get; private set; }
    public string? GoogleSubject { get; private set; }
    public string? FacebookSubject { get; private set; }
    public DateTime? PhoneVerifiedAt { get; private set; }

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

    public static bool IsValidLoginName(string? value) => value is { Length: >= 1 and <= 80 }
        && System.Text.RegularExpressions.Regex.IsMatch(value, "^[a-z0-9]+(?:[._-][a-z0-9]+)*$");

    /// <summary>Only administrative Vendor provisioning may omit contact email/phone.</summary>
    public static User CreateAdministrativeVendor(string fullName, string? email, string? phoneNumber,
        string passwordHash, string loginName)
    {
        if (string.IsNullOrWhiteSpace(fullName) || fullName.Trim().Length > 150 ||
            string.IsNullOrWhiteSpace(passwordHash) || !IsValidLoginName(loginName))
            throw new DomainException("Thông tin tài khoản nhà cung cấp không hợp lệ.");
        return new User
        {
            Id = Guid.NewGuid(), FullName = fullName.Trim(),
            _email = string.IsNullOrWhiteSpace(email) ? null : email.Trim().ToLowerInvariant(),
            PhoneNumber = phoneNumber?.Trim() ?? string.Empty, PasswordHash = passwordHash,
            Role = UserRole.VendorOwner, LoginName = loginName, IsActive = true, CreatedAt = DateTime.UtcNow
        };
    }

    public static User CreateSocialCustomer(string fullName, string passwordHash, string? googleSubject = null, string? facebookSubject = null)
    {
        var subject = googleSubject ?? facebookSubject;
        if ((googleSubject == null) == (facebookSubject == null) || string.IsNullOrWhiteSpace(subject) || subject.Length > 255 ||
            string.IsNullOrWhiteSpace(fullName) || fullName.Trim().Length > 150 || string.IsNullOrWhiteSpace(passwordHash))
            throw new DomainException("Thông tin đăng nhập mạng xã hội không hợp lệ.");
        return new User
        {
            Id = Guid.NewGuid(), FullName = fullName.Trim(), _email = null,
            PhoneNumber = "chua-cap-nhat", PasswordHash = passwordHash, Role = UserRole.Customer,
            GoogleSubject = googleSubject, FacebookSubject = facebookSubject,
            IsActive = true, CreatedAt = DateTime.UtcNow
        };
    }

    public void UpdatePassword(string newPasswordHash)
    {
        if (string.IsNullOrWhiteSpace(newPasswordHash))
            throw new DomainException("Mật khẩu mới không hợp lệ.");

        var nextSecurityVersion = checked(SecurityVersion + 1);
        PasswordHash = newPasswordHash;
        SecurityVersion = nextSecurityVersion;
        SetUpdated();
    }

    public void RecordLogin()
    {
        LastLoginAt = DateTime.UtcNow;
    }

    public void SetStatus(bool isActive)
    {
        if (IsActive == isActive) return;
        var nextSecurityVersion = checked(SecurityVersion + 1);
        IsActive = isActive;
        SecurityVersion = nextSecurityVersion;
        SetUpdated();
    }

    public void LinkGoogle(string subject)
    {
        if (Role != UserRole.Customer || string.IsNullOrWhiteSpace(subject)) throw new DomainException("Chỉ tài khoản khách hàng được liên kết Google.");
        if (GoogleSubject != null && GoogleSubject != subject) throw new DomainException("Tài khoản đã liên kết Google khác.");
        GoogleSubject = subject; SetUpdated();
    }

    public void LinkFacebook(string subject)
    {
        if (Role != UserRole.Customer || string.IsNullOrWhiteSpace(subject)) throw new DomainException("Chỉ tài khoản khách hàng được liên kết Facebook.");
        if (FacebookSubject != null && FacebookSubject != subject) throw new DomainException("Tài khoản đã liên kết Facebook khác.");
        FacebookSubject = subject; SetUpdated();
    }

    public void VerifyPhone(string phone)
    {
        PhoneNumber = phone; PhoneVerifiedAt = DateTime.UtcNow; SetUpdated();
    }

    /// <summary>A contact number is self-reported, not proof of phone ownership.</summary>
    public void UpdateContactPhone(string phoneNumber)
    {
        var normalized = phoneNumber?.Trim();
        if (normalized == null || !System.Text.RegularExpressions.Regex.IsMatch(normalized, "^0[0-9]{9}$"))
            throw new DomainException("Số điện thoại cần 10 chữ số, bắt đầu bằng 0.");
        if (PhoneNumber == normalized) return;

        var nextSecurityVersion = checked(SecurityVersion + 1);
        PhoneNumber = normalized;
        PhoneVerifiedAt = null;
        SecurityVersion = nextSecurityVersion;
        SetUpdated();
    }
}
