using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Wedding.Application.Common.Interfaces;
using Wedding.Domain.Entities;
using Wedding.Domain.Exceptions;

namespace Wedding.Application.Features.Auth.Commands;

/// <summary>Changes only the authenticated account; callers cannot select a target user or role.</summary>
public record ChangePasswordCommand(string CurrentPassword, string NewPassword, string ConfirmPassword) : IRequest
{
    public override string ToString() => "ChangePasswordCommand { [REDACTED] }";
}

public class ChangePasswordCommandValidator : AbstractValidator<ChangePasswordCommand>
{
    public ChangePasswordCommandValidator()
    {
        RuleFor(x => x.CurrentPassword).NotEmpty().WithMessage("Vui lòng nhập mật khẩu hiện tại.");
        RuleFor(x => x.NewPassword).NewPassword();
        RuleFor(x => x.ConfirmPassword).Equal(x => x.NewPassword)
            .WithMessage("Xác nhận mật khẩu mới không khớp.");
    }
}

public class ChangePasswordCommandHandler : IRequestHandler<ChangePasswordCommand>
{
    private readonly IApplicationDbContext context;
    private readonly ICurrentUserService current;
    private readonly IPasswordHasher hasher;

    public ChangePasswordCommandHandler(IApplicationDbContext context, ICurrentUserService current, IPasswordHasher hasher)
    {
        this.context = context;
        this.current = current;
        this.hasher = hasher;
    }

    public async Task Handle(ChangePasswordCommand request, CancellationToken cancellationToken)
    {
        var validation = new ChangePasswordCommandValidator().Validate(request);
        if (!validation.IsValid) throw new DomainException(validation.Errors[0].ErrorMessage);
        if (current.UserId is not Guid userId)
            throw new DomainException("Vui lòng đăng nhập lại để đổi mật khẩu.");

        await context.ExecuteSerializableAsync(async ct =>
        {
            var user = await context.Users.SingleOrDefaultAsync(u => u.Id == userId, ct);
            if (user is not { IsActive: true })
                throw new DomainException("Tài khoản không còn hoạt động. Vui lòng đăng nhập lại.");

            // Google/OTP-only accounts have a random, unknown password: never bypass reauthentication.
            if (!hasher.Verify(request.CurrentPassword, user.PasswordHash))
                throw new DomainException("Mật khẩu hiện tại không đúng. Tài khoản chỉ đăng nhập bằng Google hoặc mã email cần phương thức xác thực phù hợp trước khi đặt mật khẩu.");
            if (hasher.Verify(request.NewPassword, user.PasswordHash))
                throw new DomainException("Mật khẩu mới phải khác mật khẩu hiện tại.");

            user.UpdatePassword(hasher.Hash(request.NewPassword));
            context.AuditLogs.Add(AuditLog.Create(user.Id, user.Role.ToString(), "Auth.ChangePassword", "User", user.Id.ToString()));
            await context.SaveChangesAsync(ct);
            return true;
        }, cancellationToken);
    }
}
