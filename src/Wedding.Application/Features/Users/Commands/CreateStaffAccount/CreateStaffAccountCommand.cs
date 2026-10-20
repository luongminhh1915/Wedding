using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Wedding.Application.Common.Interfaces;
using Wedding.Application.Features.Auth.Commands;
using Wedding.Application.Features.Users.DTOs;
using Wedding.Domain.Entities;
using Wedding.Domain.Enums;
using Wedding.Domain.Exceptions;

namespace Wedding.Application.Features.Users.Commands.CreateStaffAccount;

public record CreateStaffAccountCommand(string FullName, string Email, string PhoneNumber, string Password, string Role)
    : IRequest<UserManagementDto>;

public class CreateStaffAccountCommandValidator : AbstractValidator<CreateStaffAccountCommand>
{
    public CreateStaffAccountCommandValidator()
    {
        RuleFor(x => x.FullName).Must(value => AuthInputRules.RequiredText(value, 150)).WithMessage("Họ và tên cần từ 1 đến 150 ký tự.");
        RuleFor(x => x.Email).ValidEmail();
        RuleFor(x => x.PhoneNumber).Phone();
        RuleFor(x => x.Password).NewPassword();
        RuleFor(x => x.Role).Must(role => role is "Moderator" or "Finance" or "CustomerCare")
            .WithMessage("Chỉ được cấp tài khoản Kiểm duyệt, Tài chính hoặc Chăm sóc khách hàng.");
    }
}

public class CreateStaffAccountCommandHandler(IApplicationDbContext db, ICurrentUserService current, IPasswordHasher hasher)
    : IRequestHandler<CreateStaffAccountCommand, UserManagementDto>
{
    public async Task<UserManagementDto> Handle(CreateStaffAccountCommand request, CancellationToken ct)
    {
        if (current.UserId == null || current.Role != nameof(UserRole.SuperAdmin))
            throw new DomainException("Chỉ SuperAdmin được cấp tài khoản nhân viên.");
        var validation = new CreateStaffAccountCommandValidator().Validate(request);
        if (!validation.IsValid) throw new DomainException(validation.Errors[0].ErrorMessage);
        var email = AuthInputRules.Email(request.Email);
        var passwordHash = hasher.Hash(request.Password);
        return await db.ExecuteSerializableAsync(async token =>
        {
            // Re-check the actor in the same transaction; this command must be safe outside HTTP too.
            if (!await db.Users.AnyAsync(u => u.Id == current.UserId && u.IsActive && u.Role == UserRole.SuperAdmin, token))
                throw new DomainException("Tài khoản quản trị không còn quyền cấp tài khoản.");
            if (await db.Users.AnyAsync(u => u.Email == email, token)) throw new DomainException("Email này đã được sử dụng trên hệ thống.");
            var user = User.Create(request.FullName, email, request.PhoneNumber, passwordHash, Enum.Parse<UserRole>(request.Role));
            db.Users.Add(user);
            db.AuditLogs.Add(AuditLog.Create(current.UserId, current.Role, "Staff.Create", "User", user.Id.ToString(),
                System.Text.Json.JsonSerializer.Serialize(new { user.Role })));
            await db.SaveChangesAsync(token);
            // Creating another actor never replaces the administrator's session or discloses a password/hash/token.
            return new UserManagementDto(user.Id, user.FullName, user.Email, user.PhoneNumber, user.Role.ToString(),
                user.AvatarUrl, user.IsActive, user.CreatedAt, user.LastLoginAt, null, null);
        }, ct);
    }
}
