namespace Wedding.Application.Common.Interfaces;

/// <summary>
/// Dịch vụ cung cấp thông tin người dùng hiện tại từ JWT Claims.
/// </summary>
public interface ICurrentUserService
{
    Guid? UserId { get; }
    string? Email { get; }
    string? Role { get; }
}
