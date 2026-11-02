using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Wedding.Application.Common.Interfaces;
using Wedding.Application.Features.Auth.Commands;
using Wedding.Domain.Entities;
using Wedding.Domain.Enums;
using Wedding.Domain.Exceptions;

namespace Wedding.Infrastructure.Services;

public sealed record FirstAdministratorInput(string FullName, string Email, string PhoneNumber, string Password, string ConfirmPassword)
{
    // Do not let ordinary diagnostic interpolation reveal a deployment administrator's password.
    public override string ToString() => nameof(FirstAdministratorInput) + " { [redacted] }";
}

public sealed class FirstAdministratorInputValidator : AbstractValidator<FirstAdministratorInput>
{
    public FirstAdministratorInputValidator()
    {
        RuleFor(input => input.FullName).Must(value => AuthInputRules.RequiredText(value, 150)).WithMessage("Họ và tên cần từ 1 đến 150 ký tự.");
        RuleFor(input => input.Email).ValidEmail();
        RuleFor(input => input.PhoneNumber).Phone();
        RuleFor(input => input.Password).Must(value => value is { Length: >= 12 } && AuthInputRules.IsNewPassword(value))
            .WithMessage("Mật khẩu quản trị cần ít nhất 12 ký tự và tối đa 72 byte UTF-8.");
        RuleFor(input => input.ConfirmPassword).Equal(input => input.Password).WithMessage("Mật khẩu xác nhận không khớp.");
    }
}

/// <summary>
/// Explicit, one-off first administrator creation. Never registered as a hosted service or
/// exposed through an HTTP endpoint. It never promotes, resets, or reactivates an existing user.
/// </summary>
public sealed class FirstAdministratorBootstrapper(IApplicationDbContext db, IPasswordHasher hasher)
{
    public async Task<Guid> CreateAsync(FirstAdministratorInput input, CancellationToken ct = default)
    {
        var validation = new FirstAdministratorInputValidator().Validate(input);
        if (!validation.IsValid) throw new DomainException(validation.Errors[0].ErrorMessage);
        var email = AuthInputRules.Email(input.Email);
        var passwordHash = hasher.Hash(input.Password);
        return await db.ExecuteSerializableAsync(async token =>
        {
            if (await db.Users.AnyAsync(user => user.Role == UserRole.SuperAdmin, token))
                throw new DomainException("Đã có tài khoản SuperAdmin (kể cả đang bị khóa). Công cụ không tạo thêm hoặc đặt lại tài khoản quản trị.");
            if (await db.Users.AnyAsync(user => user.Email.ToLower() == email, token))
                throw new DomainException("Email đã tồn tại. Công cụ không nâng quyền hoặc thay đổi tài khoản hiện có.");
            var administrator = User.Create(input.FullName, email, input.PhoneNumber, passwordHash, UserRole.SuperAdmin);
            db.Users.Add(administrator);
            db.AuditLogs.Add(AuditLog.Create(null, "DeploymentTool", "Deployment.FirstAdministratorCreated", "User", administrator.Id.ToString(),
                "{\"role\":\"SuperAdmin\",\"method\":\"InteractiveConsole\"}"));
            await db.SaveChangesAsync(token);
            return administrator.Id;
        }, ct);
    }
}
