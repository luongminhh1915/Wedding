using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Wedding.Application.Common.Interfaces;
using Wedding.Application.Features.Auth.DTOs;
using Wedding.Application.Features.Auth.Commands;
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
    public static string Normalize(string? target) => AuthInputRules.Email(target);
    public static bool IsValid(string? target) => AuthInputRules.IsEmail(target);
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
            var account = await context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Email == target, token);
            if (account != null && account.Role != UserRole.Customer)
                throw new DomainException("Tài khoản vận hành/NCC vui lòng đăng nhập bằng mật khẩu.");
            if (account is { IsActive: false }) throw new DomainException("Tài khoản đang bị khóa. Vui lòng liên hệ hỗ trợ.");
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
public class VerifyOtpCommandValidator : AbstractValidator<VerifyOtpCommand>
{
    public VerifyOtpCommandValidator()
    {
        RuleFor(x => x.Target).Must(OtpTarget.IsValid).WithMessage("Vui lòng nhập email hợp lệ.");
        RuleFor(x => x.Code).NotEmpty().Matches(@"^[0-9]{6}$").WithMessage("Mã OTP cần đúng 6 chữ số.");
        RuleFor(x => x.FullName).Must(name => name == null || name.Trim().Length <= 150).WithMessage("Họ và tên tối đa 150 ký tự.");
    }
}
public class VerifyOtpCommandHandler(IApplicationDbContext context, IPasswordHasher hasher, IJwtProvider jwt) : IRequestHandler<VerifyOtpCommand, AuthResponseDto>
{
    public async Task<AuthResponseDto> Handle(VerifyOtpCommand request, CancellationToken ct)
    {
        var validation = new VerifyOtpCommandValidator().Validate(request);
        if (!validation.IsValid) throw new DomainException(validation.Errors[0].ErrorMessage);
        var target = OtpTarget.Normalize(request.Target);
        var otp = await context.OtpCodes.Where(o => o.Target == target).OrderByDescending(o => o.CreatedAt).FirstOrDefaultAsync(ct)
            ?? throw new DomainException("Bạn chưa yêu cầu mã OTP.");
        try { otp.Verify(request.Code); }
        finally
        {
            try { await context.SaveChangesAsync(ct); }
            catch (DbUpdateConcurrencyException) { throw new DomainException("Mã OTP đã được sử dụng. Vui lòng gửi lại mã."); }
        }
        return await context.ExecuteSerializableAsync(async token =>
        {
            var user = await context.Users.Include(u => u.Vendor).FirstOrDefaultAsync(u => u.Email == target, token);
            if (user == null)
            {
                user = User.Create(string.IsNullOrWhiteSpace(request.FullName) ? "Cặp đôi mới" : request.FullName, target, "chua-cap-nhat", hasher.Hash(Guid.NewGuid().ToString("N")), UserRole.Customer);
                context.Users.Add(user);
            }
            if (user.Role != UserRole.Customer) throw new DomainException("Tài khoản vận hành/NCC vui lòng đăng nhập bằng mật khẩu.");
            return await SocialLogin.IssueAsync(context, jwt, user, token);
        }, ct);
    }
}
public record GoogleLoginCommand(string IdToken) : IRequest<AuthResponseDto>;
public class GoogleLoginCommandValidator : AbstractValidator<GoogleLoginCommand>
{
    public GoogleLoginCommandValidator() => RuleFor(x => x.IdToken).NotEmpty().MaximumLength(16000).WithMessage("Google ID token không hợp lệ.");
}
public class GoogleLoginCommandHandler(IApplicationDbContext context, IPasswordHasher hasher, IJwtProvider jwt, IGoogleIdentityVerifier google) : IRequestHandler<GoogleLoginCommand, AuthResponseDto>
{
    public async Task<AuthResponseDto> Handle(GoogleLoginCommand request, CancellationToken ct)
    {
        var validation = new GoogleLoginCommandValidator().Validate(request);
        if (!validation.IsValid) throw new DomainException(validation.Errors[0].ErrorMessage);
        var identity = await google.VerifyAsync(request.IdToken, ct);
        if (!AuthInputRules.IsEmail(identity.Email) || string.IsNullOrWhiteSpace(identity.Subject) || identity.Subject.Length > 255)
            throw new DomainException("Thông tin tài khoản Google không hợp lệ.");
        var email = AuthInputRules.Email(identity.Email);
        var name = string.IsNullOrWhiteSpace(identity.Name) ? email : identity.Name.Trim();
        if (name.Length > 150) name = name[..150];
        return await context.ExecuteSerializableAsync(async token =>
        {
            var user = await context.Users.FirstOrDefaultAsync(u => u.GoogleSubject == identity.Subject, token);
            if (user == null)
            {
                var emailAccount = await context.Users.FirstOrDefaultAsync(u => u.Email == email, token);
                if (emailAccount != null && emailAccount.Role != UserRole.Customer)
                    throw new DomainException("Tài khoản vận hành/NCC vui lòng đăng nhập bằng mật khẩu.");
                // Provider subject is the identity key. An email collision never takes over another account.
                user = emailAccount == null
                    ? User.Create(name, email, "chua-cap-nhat", hasher.Hash(Guid.NewGuid().ToString("N")), UserRole.Customer)
                    : User.CreateSocialCustomer(name, hasher.Hash(Guid.NewGuid().ToString("N")), googleSubject: identity.Subject);
                user.LinkGoogle(identity.Subject); user.UpdateProfile(name, identity.Picture?.Length <= 500 ? identity.Picture : null); context.Users.Add(user);
            }
            if (user.Role != UserRole.Customer) throw new DomainException("Tài khoản vận hành/NCC vui lòng đăng nhập bằng mật khẩu.");
            return await SocialLogin.IssueAsync(context, jwt, user, token);
        }, ct);
    }
}
public record FacebookLoginCommand(string AccessToken) : IRequest<AuthResponseDto>;
public class FacebookLoginCommandValidator : AbstractValidator<FacebookLoginCommand>
{
    public FacebookLoginCommandValidator() => RuleFor(x => x.AccessToken).NotEmpty().MaximumLength(8192).WithMessage("Facebook access token không hợp lệ.");
}
public class FacebookLoginCommandHandler(IApplicationDbContext context, IPasswordHasher hasher, IJwtProvider jwt, IFacebookIdentityVerifier facebook) : IRequestHandler<FacebookLoginCommand, AuthResponseDto>
{
    public async Task<AuthResponseDto> Handle(FacebookLoginCommand request, CancellationToken ct)
    {
        var validation = new FacebookLoginCommandValidator().Validate(request);
        if (!validation.IsValid) throw new DomainException(validation.Errors[0].ErrorMessage);
        var identity = await facebook.VerifyAsync(request.AccessToken, ct);
        if (string.IsNullOrWhiteSpace(identity.Subject) || identity.Subject.Length > 255)
            throw new DomainException("Thông tin tài khoản Facebook không hợp lệ.");
        var name = string.IsNullOrWhiteSpace(identity.Name) ? "Khách hàng Facebook" : identity.Name.Trim();
        if (name.Length > 150) name = name[..150];
        return await context.ExecuteSerializableAsync(async token =>
        {
            var user = await context.Users.FirstOrDefaultAsync(u => u.FacebookSubject == identity.Subject, token);
            if (user == null)
            {
                user = User.CreateSocialCustomer(name, hasher.Hash(Guid.NewGuid().ToString("N")), facebookSubject: identity.Subject);
                user.UpdateProfile(name, identity.Picture?.Length <= 500 ? identity.Picture : null);
                context.Users.Add(user);
            }
            if (user.Role != UserRole.Customer) throw new DomainException("Tài khoản vận hành/NCC vui lòng đăng nhập bằng mật khẩu.");
            return await SocialLogin.IssueAsync(context, jwt, user, token);
        }, ct);
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
