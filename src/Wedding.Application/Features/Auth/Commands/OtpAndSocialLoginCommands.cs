using System.Net.Mail;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Wedding.Application.Common.Interfaces;
using Wedding.Application.Features.Auth.DTOs;
using Wedding.Domain.Entities;
using Wedding.Domain.Enums;
using Wedding.Domain.Exceptions;

public record RequestOtpCommand(string Target) : IRequest<RequestOtpResponseDto>;
public record RequestOtpResponseDto(bool Sent, int ExpiresInSeconds);
public class RequestOtpCommandValidator : AbstractValidator<RequestOtpCommand>
{
    public RequestOtpCommandValidator() => RuleFor(x => x.Target).NotEmpty().Must(OtpTarget.IsValid).WithMessage("Vui lòng nhập email hợp lệ.");
}
internal static class OtpTarget
{
    public static string Normalize(string? target) => (target ?? "").Trim().ToLowerInvariant();
    public static bool IsValid(string? target) => Normalize(target).Length <= 150 && MailAddress.TryCreate(Normalize(target), out var mail) && mail.Address == Normalize(target) && mail.Host.Contains('.');
}
public class RequestOtpCommandHandler(IApplicationDbContext context, IOtpDelivery delivery) : IRequestHandler<RequestOtpCommand, RequestOtpResponseDto>
{
    public async Task<RequestOtpResponseDto> Handle(RequestOtpCommand request, CancellationToken ct)
    {
        if (!delivery.EmailEnabled) throw new DomainException("OTP email chưa được cấu hình. Vui lòng đăng nhập bằng mật khẩu.");
        var target = OtpTarget.Normalize(request.Target);
        if (!OtpTarget.IsValid(target)) throw new DomainException("Vui lòng nhập email hợp lệ. OTP SMS chưa được cấu hình.");
        var pending = await context.ExecuteSerializableAsync(async token =>
        {
            var since = DateTime.UtcNow.AddMinutes(-1);
            if (await context.OtpCodes.AnyAsync(o => o.Target == target && o.CreatedAt > since, token)) throw new DomainException("Vui lòng đợi 60 giây trước khi gửi lại mã.");
            var hour = DateTime.UtcNow.AddHours(-1);
            if (await context.OtpCodes.CountAsync(o => o.Target == target && o.CreatedAt > hour, token) >= 8) throw new DomainException("Bạn đã yêu cầu quá nhiều mã. Vui lòng thử lại sau.");
            var otp = OtpCode.Create(target, out var code); context.OtpCodes.Add(otp);
            await context.SaveChangesAsync(token);
            return (Id: otp.Id, Code: code);
        }, ct);
        // SMTP is deliberately outside the database transaction/global inventory lock.
        try { await delivery.SendEmailAsync(target, pending.Code, ct); }
        catch
        {
            // Invalidate only this generation, never a later OTP requested by the same account.
            var failed = await context.OtpCodes.FirstOrDefaultAsync(o => o.Id == pending.Id, CancellationToken.None);
            if (failed != null && !failed.IsUsed)
            {
                failed.InvalidateDelivery();
                try { await context.SaveChangesAsync(CancellationToken.None); }
                catch (DbUpdateConcurrencyException) { /* Already consumed by a concurrent successful verification. */ }
            }
            throw;
        }
        return new RequestOtpResponseDto(true, 300);
    }
}
public record VerifyOtpCommand(string Target, string Code, string? FullName) : IRequest<AuthResponseDto>;
public class VerifyOtpCommandHandler(IApplicationDbContext context, IPasswordHasher hasher, IJwtProvider jwt) : IRequestHandler<VerifyOtpCommand, AuthResponseDto>
{
    public async Task<AuthResponseDto> Handle(VerifyOtpCommand request, CancellationToken ct)
    {
        var target = OtpTarget.Normalize(request.Target);
        var otp = await context.OtpCodes.Where(o => o.Target == target).OrderByDescending(o => o.CreatedAt).FirstOrDefaultAsync(ct)
            ?? throw new DomainException("Bạn chưa yêu cầu mã OTP.");
        try { otp.Verify(request.Code); }
        finally
        {
            try { await context.SaveChangesAsync(ct); }
            catch (DbUpdateConcurrencyException) { throw new DomainException("Mã OTP đã được sử dụng. Vui lòng gửi lại mã."); }
        }
        var user = await context.Users.Include(u => u.Vendor).FirstOrDefaultAsync(u => u.Email == target, ct);
        if (user == null)
        {
            user = User.Create(string.IsNullOrWhiteSpace(request.FullName) ? "Cặp đôi mới" : request.FullName, target, "chua-cap-nhat", hasher.Hash(Guid.NewGuid().ToString("N")), UserRole.Customer);
            context.Users.Add(user);
        }
        if (user.Role != UserRole.Customer) throw new DomainException("Tài khoản vận hành/NCC vui lòng đăng nhập bằng mật khẩu.");
        return await SocialLogin.IssueAsync(context, jwt, user, ct);
    }
}
public record GoogleLoginCommand(string IdToken) : IRequest<AuthResponseDto>;
public class GoogleLoginCommandHandler(IApplicationDbContext context, IPasswordHasher hasher, IJwtProvider jwt, IGoogleIdentityVerifier google) : IRequestHandler<GoogleLoginCommand, AuthResponseDto>
{
    public async Task<AuthResponseDto> Handle(GoogleLoginCommand request, CancellationToken ct)
    {
        var identity = await google.VerifyAsync(request.IdToken, ct);
        var user = await context.Users.FirstOrDefaultAsync(u => u.GoogleSubject == identity.Subject, ct);
        if (user == null)
        {
            if (await context.Users.AnyAsync(u => u.Email == identity.Email, ct)) throw new DomainException("Email đã có tài khoản. Đăng nhập bằng mật khẩu/OTP rồi liên kết Google.");
            user = User.Create(identity.Name, identity.Email, "chua-cap-nhat", hasher.Hash(Guid.NewGuid().ToString("N")), UserRole.Customer);
            user.LinkGoogle(identity.Subject); user.UpdateProfile(identity.Name, identity.Picture); context.Users.Add(user);
        }
        return await SocialLogin.IssueAsync(context, jwt, user, ct);
    }
}
internal static class SocialLogin
{
    public static async Task<AuthResponseDto> IssueAsync(IApplicationDbContext context, IJwtProvider jwt, User user, CancellationToken ct)
    {
        if (!user.IsActive) throw new DomainException("Tài khoản đang bị khóa. Vui lòng liên hệ hỗ trợ.");
        user.RecordLogin(); await context.SaveChangesAsync(ct);
        return new AuthResponseDto(jwt.GenerateToken(user), new UserDto(user.Id, user.FullName, user.Email, user.PhoneNumber, user.Role.ToString(), user.AvatarUrl, user.Vendor?.Id, user.Vendor?.BrandName));
    }
}
