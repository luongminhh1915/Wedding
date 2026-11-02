using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Wedding.Application.Common.Interfaces;
using Wedding.Application.Features.Auth.Commands;
using Wedding.Application.Features.Users.DTOs;
using Wedding.Domain.Entities;
using Wedding.Domain.Enums;
using Wedding.Domain.Exceptions;

namespace Wedding.Application.Features.Users.Commands.CreateUser;

public record CreateUserCommand(string FullName, string? Email, string? PhoneNumber, string Password, string Role,
    string? BrandName = null, string? City = null, bool ConfirmSuperAdminCreation = false,
    IReadOnlyList<string>? OperatingRegions = null, bool Nationwide = false) : IRequest<UserManagementDto>
{
    public override string ToString() => "CreateUserCommand { [REDACTED] }";
}

public class CreateUserCommandValidator : AbstractValidator<CreateUserCommand>
{
    public CreateUserCommandValidator()
    {
        RuleFor(x => x.FullName).Must(value => AuthInputRules.RequiredText(value, 150)).WithMessage("Họ và tên cần từ 1 đến 150 ký tự.");
        RuleFor(x => x.Email!).ValidEmail().When(x => x.Role != nameof(UserRole.VendorOwner) || !string.IsNullOrWhiteSpace(x.Email));
        RuleFor(x => x.PhoneNumber!).Phone().When(x => x.Role != nameof(UserRole.VendorOwner) || !string.IsNullOrWhiteSpace(x.PhoneNumber));
        RuleFor(x => x.Password).NewPassword();
        RuleFor(x => x.Role).Must(role => role is "Customer" or "VendorOwner" or "Moderator" or "Finance" or "CustomerCare" or "SuperAdmin")
            .WithMessage("Vai trò tài khoản không hợp lệ.");
        When(x => x.Role == nameof(UserRole.SuperAdmin), () =>
        {
            RuleFor(x => x.Password).Must(value => value is { Length: >= 12 })
                .WithMessage("Mật khẩu SuperAdmin cần ít nhất 12 ký tự và tối đa 72 byte UTF-8.");
            RuleFor(x => x.ConfirmSuperAdminCreation).Equal(true)
                .WithMessage("Cần xác nhận rõ việc cấp toàn quyền SuperAdmin cho tài khoản mới.");
        });
        When(x => x.Role == nameof(UserRole.VendorOwner), () =>
        {
            RuleFor(x => x.BrandName).Must(value => string.IsNullOrWhiteSpace(value) || AuthInputRules.RequiredText(value, 200))
                .WithMessage("Tên thương hiệu cần từ 1 đến 200 ký tự.");
            RuleFor(x => x).Must(value => OperatingAreaRules.IsValid(value.City, value.OperatingRegions, value.Nationwide))
                .WithMessage("Chọn Toàn quốc hoặc các tỉnh / thành phố hợp lệ trong danh sách 34 địa phương.");
        });
    }
}

/// <summary>Administrative provisioning never signs in as the newly created actor or accepts Vendor terms for them.</summary>
public class CreateUserCommandHandler(IApplicationDbContext db, ICurrentUserService current, IPasswordHasher hasher)
    : IRequestHandler<CreateUserCommand, UserManagementDto>
{
    public async Task<UserManagementDto> Handle(CreateUserCommand request, CancellationToken ct)
    {
        if (current.UserId == null || current.Role != nameof(UserRole.SuperAdmin))
            throw new DomainException("Chỉ SuperAdmin được tạo tài khoản người dùng.");
        var validation = new CreateUserCommandValidator().Validate(request);
        if (!validation.IsValid) throw new DomainException(validation.Errors[0].ErrorMessage);
        var email = AuthInputRules.Email(request.Email);
        var passwordHash = hasher.Hash(request.Password);
        return await db.ExecuteSerializableAsync(async token =>
        {
            // Re-read the actor in the same transaction as all provisioning writes.
            if (!await db.Users.AnyAsync(user => user.Id == current.UserId && user.IsActive && user.Role == UserRole.SuperAdmin, token))
                throw new DomainException("Tài khoản quản trị không còn quyền tạo người dùng.");
            if (email.Length > 0 && await db.Users.AnyAsync(user => user.Email == email, token))
                throw new DomainException("Email này đã được sử dụng trên hệ thống.");

            User user;
            if (request.Role == nameof(UserRole.VendorOwner))
            {
                var baseName = VendorRegistrationRules.GenerateSlug(request.FullName);
                baseName = baseName[..Math.Min(baseName.Length, 68)].TrimEnd('-');
                var loginName = baseName;
                var loginSuffix = 1;
                while (await db.Users.AnyAsync(existing => existing.LoginName == loginName, token))
                    loginName = $"{baseName}-{loginSuffix++}";
                user = User.CreateAdministrativeVendor(request.FullName, email, request.PhoneNumber, passwordHash, loginName);
            }
            else user = User.Create(request.FullName, email, request.PhoneNumber!, passwordHash, Enum.Parse<UserRole>(request.Role));
            Vendor? vendor = null;
            if (user.Role == UserRole.VendorOwner)
            {
                var brandName = string.IsNullOrWhiteSpace(request.BrandName) ? request.FullName.Trim() : request.BrandName.Trim();
                var baseSlug = VendorRegistrationRules.GenerateSlug(brandName);
                var slug = baseSlug;
                var suffix = 1;
                while (await db.Vendors.AnyAsync(existing => existing.Slug == slug, token)) slug = $"{baseSlug}-{suffix++}";
                VietnamProvinces.TryNormalize(request.City, out var city);
                vendor = Vendor.CreateForAdministration(user.Id, brandName, slug, user.FullName, user.PhoneNumber, city);
                vendor.SetOperatingAreas(request.OperatingRegions, request.Nationwide, request.City);
                db.Vendors.Add(vendor);
            }
            db.Users.Add(user);
            db.AuditLogs.Add(AuditLog.Create(current.UserId, current.Role, "User.Create", "User", user.Id.ToString(),
                System.Text.Json.JsonSerializer.Serialize(new
                {
                    Role = user.Role.ToString(),
                    VendorId = vendor?.Id,
                    SuperAdminGrantConfirmed = user.Role == UserRole.SuperAdmin && request.ConfirmSuperAdminCreation
                })));
            await db.SaveChangesAsync(token);
            var coverage = vendor == null ? null : OperatingAreaRules.Read(vendor.OperatingRegions, vendor.City);
            return new UserManagementDto(user.Id, user.FullName, user.Email, user.PhoneNumber, user.Role.ToString(), user.AvatarUrl,
                user.IsActive, user.CreatedAt, user.LastLoginAt, vendor?.Id, vendor?.BrandName, user.LoginName,
                coverage?.Regions, coverage?.Nationwide ?? false);
        }, ct);
    }
}
