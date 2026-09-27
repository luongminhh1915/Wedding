namespace Wedding.Application.Features.Auth.DTOs;

public record UserDto(
    Guid Id, 
    string FullName, 
    string Email, 
    string PhoneNumber, 
    string Role, 
    string? AvatarUrl, 
    Guid? VendorId,
    string? VendorBrandName
);

public record AuthResponseDto(
    string Token, 
    UserDto User
);
