using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Wedding.Application.Common.Interfaces;
using Wedding.Application.Features.Auth.DTOs;
using Wedding.Domain.Entities;
using Wedding.Domain.Exceptions;

namespace Wedding.Application.Features.Auth.Commands;

/// <summary>Self-service display name only. Identity, role, avatar and wedding answers are not writable here.</summary>
public record UpdateAccountProfileCommand(string FullName) : IRequest<UserDto>
{
    public override string ToString() => "UpdateAccountProfileCommand { [REDACTED] }";
}

public class UpdateAccountProfileValidator : AbstractValidator<UpdateAccountProfileCommand>
{
    public UpdateAccountProfileValidator() => RuleFor(x => x.FullName)
        .Must(value => AuthInputRules.RequiredText(value, 150) && !value.Any(char.IsControl))
        .WithMessage("Họ và tên cần từ 1 đến 150 ký tự, không chứa ký tự điều khiển.");
}

public class UpdateAccountProfileHandler(IApplicationDbContext db, ICurrentUserService current)
    : IRequestHandler<UpdateAccountProfileCommand, UserDto>
{
    public async Task<UserDto> Handle(UpdateAccountProfileCommand request, CancellationToken cancellationToken)
    {
        var validation = new UpdateAccountProfileValidator().Validate(request);
        if (!validation.IsValid) throw new DomainException(validation.Errors[0].ErrorMessage);
        if (current.UserId is not Guid userId) throw new DomainException("Vui lòng đăng nhập để cập nhật tài khoản.");
        return await db.ExecuteSerializableAsync(async ct =>
        {
            var user = await db.Users.Include(u => u.Vendor).SingleOrDefaultAsync(u => u.Id == userId, ct);
            if (user is not { IsActive: true }) throw new DomainException("Tài khoản không tồn tại hoặc đang bị khóa.");
            if (user.FullName != request.FullName.Trim())
            {
                user.UpdateProfile(request.FullName, user.AvatarUrl);
                db.AuditLogs.Add(AuditLog.Create(user.Id, user.Role.ToString(), "Auth.UpdateProfile", "User", user.Id.ToString()));
                await db.SaveChangesAsync(ct);
            }
            return new UserDto(user.Id, user.FullName, user.Email, user.PhoneNumber, user.Role.ToString(),
                user.AvatarUrl, user.Vendor?.Id, user.Vendor?.BrandName, user.LoginName);
        }, cancellationToken);
    }
}
