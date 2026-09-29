namespace Wedding.Application.Features.Users.DTOs;

public record UserManagementDto(
    Guid Id,
    string FullName,
    string Email,
    string PhoneNumber,
    string Role,
    string? AvatarUrl,
    bool IsActive,
    DateTime CreatedAt,
    DateTime? LastLoginAt,
    Guid? VendorId,
    string? VendorBrandName
);
