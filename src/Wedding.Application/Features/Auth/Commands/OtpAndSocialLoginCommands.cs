using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Wedding.Application.Common.Interfaces;
using Wedding.Application.Features.Auth.DTOs;
using Wedding.Domain.Entities;
using Wedding.Domain.Enums;
using Wedding.Domain.Exceptions;

namespace Wedding.Application.Features.Auth.Commands;

/// <summary>P1 – Yêu cầu mã OTP qua SĐT hoặc email. Kênh gửi đang mock: mã được trả về trong DevCode.</summary>
public record RequestOtpCommand(string Target) : IRequest<RequestOtpResponseDto>;
public record RequestOtpResponseDto(bool Sent, int ExpiresInSeconds, string? DevCode);

public class RequestOtpCommandValidator : AbstractValidator<RequestOtpCommand>
{
    public RequestOtpCommandValidator()
    {
        RuleFor(x => x.Target).NotEmpty().WithMessage("Vui lòng nhập số điện thoại hoặc email.")
            .Must(OtpTarget.IsValid).WithMessage("Số điện thoại hoặc email không hợp lệ.");
    }
}

internal static class OtpTarget
{
    public static string Normalize(string t) => t.Trim().ToLowerInvariant().Replace(" ", "");
    public static bool IsEmail(string t) => t.Contains('@');
    public static bool IsValid(string t)
    {
        var n = Normalize(t ?? "");
        return IsEmail(n) ? n.Length >= 5 : System.Text.RegularExpressions.Regex.IsMatch(n, "^(0|\\+84)[0-9]{9}$");
    }
}

public class RequestOtpCommandHandler : IRequestHandler<RequestOtpCommand, RequestOtpResponseDto>
{
    private readonly IApplicationDbContext _context;
    private readonly INotificationSender _sender;

    public RequestOtpCommandHandler(IApplicationDbContext context, INotificationSender sender)
    {
        _context = context;
        _sender = sender;
    }

    public async Task<RequestOtpResponseDto> Handle(RequestOtpCommand request, CancellationToken ct)
    {
        var target = OtpTarget.Normalize(request.Target);
        var since = DateTime.UtcNow.AddSeconds(-60);
        if (await _context.OtpCodes.AnyAsync(o => o.Target == target && o.CreatedAt > since, ct))
            throw new DomainException("Vui lòng đợi 60 giây trước khi gửi lại mã.");

        var otp = OtpCode.Create(target);
        _context.OtpCodes.Add(otp);
        await _context.SaveChangesAsync(ct);
        await _sender.SendAsync(OtpTarget.IsEmail(target) ? "Email" : "SMS", target, $"Wedding Match: ma OTP cua ban la {otp.Code}", ct);
        return new RequestOtpResponseDto(true, 300, otp.Code);
    }
}

public record VerifyOtpCommand(string Target, string Code, string? FullName) : IRequest<AuthResponseDto>;

public class VerifyOtpCommandHandler : IRequestHandler<VerifyOtpCommand, AuthResponseDto>
{
    private readonly IApplicationDbContext _context;
    private readonly IPasswordHasher _hasher;
    private readonly IJwtProvider _jwt;

    public VerifyOtpCommandHandler(IApplicationDbContext context, IPasswordHasher hasher, IJwtProvider jwt)
    {
        _context = context;
        _hasher = hasher;
        _jwt = jwt;
    }

    public async Task<AuthResponseDto> Handle(VerifyOtpCommand request, CancellationToken ct)
    {
        var target = OtpTarget.Normalize(request.Target);
        var otp = (await _context.OtpCodes.Where(o => o.Target == target && !o.IsUsed).ToListAsync(ct))
            .OrderByDescending(o => o.CreatedAt).FirstOrDefault()
            ?? throw new DomainException("Bạn chưa yêu cầu mã OTP hoặc mã đã hết hạn.");
        try { otp.Verify(request.Code); }
        finally { await _context.SaveChangesAsync(ct); }

        var isEmail = OtpTarget.IsEmail(target);
        var user = await _context.Users.Include(u => u.Vendor)
            .FirstOrDefaultAsync(u => isEmail ? u.Email == target : u.PhoneNumber == target, ct);
        if (user == null)
        {
            var name = string.IsNullOrWhiteSpace(request.FullName) ? "Cặp đôi mới" : request.FullName!;
            user = User.Create(name, isEmail ? target : $"{target}@phone.weddingmatch.vn", isEmail ? "chua-cap-nhat" : target,
                _hasher.Hash(Guid.NewGuid().ToString("N")), UserRole.Customer);
            _context.Users.Add(user);
        }
        return await SocialLogin.IssueAsync(_context, _jwt, user, ct);
    }
}

/// <summary>P1 – Đăng nhập Google (mock: client gửi email/tên đã xác thực; bản thật sẽ verify id_token ở P7).</summary>
public record GoogleLoginCommand(string Email, string FullName, string? AvatarUrl) : IRequest<AuthResponseDto>;

public class GoogleLoginCommandHandler : IRequestHandler<GoogleLoginCommand, AuthResponseDto>
{
    private readonly IApplicationDbContext _context;
    private readonly IPasswordHasher _hasher;
    private readonly IJwtProvider _jwt;

    public GoogleLoginCommandHandler(IApplicationDbContext context, IPasswordHasher hasher, IJwtProvider jwt)
    {
        _context = context;
        _hasher = hasher;
        _jwt = jwt;
    }

    public async Task<AuthResponseDto> Handle(GoogleLoginCommand request, CancellationToken ct)
    {
        if (!OtpTarget.IsEmail(request.Email ?? "")) throw new DomainException("Email Google không hợp lệ.");
        var email = request.Email!.Trim().ToLowerInvariant();
        var user = await _context.Users.Include(u => u.Vendor).FirstOrDefaultAsync(u => u.Email == email, ct);
        if (user == null)
        {
            user = User.Create(string.IsNullOrWhiteSpace(request.FullName) ? email : request.FullName, email, "chua-cap-nhat",
                _hasher.Hash(Guid.NewGuid().ToString("N")), UserRole.Customer);
            user.UpdateProfile(user.FullName, request.AvatarUrl);
            _context.Users.Add(user);
        }
        return await SocialLogin.IssueAsync(_context, _jwt, user, ct);
    }
}

internal static class SocialLogin
{
    public static async Task<AuthResponseDto> IssueAsync(IApplicationDbContext context, IJwtProvider jwt, User user, CancellationToken ct)
    {
        if (!user.IsActive) throw new DomainException("Tài khoản của bạn đang bị tạm khóa. Vui lòng liên hệ hỗ trợ.");
        user.RecordLogin();
        await context.SaveChangesAsync(ct);
        return new AuthResponseDto(jwt.GenerateToken(user), new UserDto(user.Id, user.FullName, user.Email, user.PhoneNumber,
            user.Role.ToString(), user.AvatarUrl, user.Vendor?.Id, user.Vendor?.BrandName));
    }
}
