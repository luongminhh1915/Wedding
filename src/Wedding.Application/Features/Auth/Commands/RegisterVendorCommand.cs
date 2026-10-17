using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;
using System.Globalization;
using System.Text;
using Wedding.Application.Common.Interfaces;
using Wedding.Application.Features.Auth.DTOs;
using Wedding.Domain.Entities;
using Wedding.Domain.Enums;
using Wedding.Domain.Exceptions;

namespace Wedding.Application.Features.Auth.Commands;

public record RegisterVendorCommand(
    string FullName,
    string Email,
    string PhoneNumber,
    string Password,
    string BrandName,
    string City,
    decimal CommissionRate = 0.08m
) : IRequest<AuthResponseDto>;

public class RegisterVendorCommandValidator : AbstractValidator<RegisterVendorCommand>
{
    public RegisterVendorCommandValidator()
    {
        RuleFor(x => x.FullName).Must(value => AuthInputRules.RequiredText(value, 150)).WithMessage("Họ tên người đại diện cần từ 1 đến 150 ký tự.");
        RuleFor(x => x.Email).ValidEmail();
        RuleFor(x => x.PhoneNumber).Phone();
        RuleFor(x => x.Password).NewPassword();
        RuleFor(x => x.BrandName).Must(value => AuthInputRules.RequiredText(value, 200)).WithMessage("Tên thương hiệu cần từ 1 đến 200 ký tự.");
        RuleFor(x => x.City).Must(value => AuthInputRules.RequiredText(value, 100)).WithMessage("Thành phố cần từ 1 đến 100 ký tự.");
        RuleFor(x => x.CommissionRate).Must(rate => rate is 0.03m or 0.08m or 0.10m or 0.12m)
            .WithMessage("Tỷ lệ hoa hồng phải thuộc các mức 3%, 8%, 10% hoặc 12% của hệ thống.");
    }
}

public class RegisterVendorCommandHandler : IRequestHandler<RegisterVendorCommand, AuthResponseDto>
{
    private readonly IApplicationDbContext _context;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtProvider _jwtProvider;

    public RegisterVendorCommandHandler(
        IApplicationDbContext context,
        IPasswordHasher passwordHasher,
        IJwtProvider jwtProvider)
    {
        _context = context;
        _passwordHasher = passwordHasher;
        _jwtProvider = jwtProvider;
    }

    public async Task<AuthResponseDto> Handle(RegisterVendorCommand request, CancellationToken cancellationToken)
    {
        var validation = new RegisterVendorCommandValidator().Validate(request);
        if (!validation.IsValid) throw new DomainException(validation.Errors[0].ErrorMessage);
        var emailLower = AuthInputRules.Email(request.Email);
        var passwordHash = _passwordHasher.Hash(request.Password);
        return await _context.ExecuteSerializableAsync(async ct =>
        {
            if (await _context.Users.AnyAsync(u => u.Email == emailLower, ct))
                throw new DomainException("Email đối tác này đã được sử dụng trên hệ thống.");

            // Tạo User chủ với vai trò VendorOwner (BR-001)
            var user = User.Create(request.FullName, emailLower, request.PhoneNumber, passwordHash, UserRole.VendorOwner);

            // Tạo Slug cho thương hiệu
            var baseSlug = GenerateSlug(request.BrandName);
            var slug = baseSlug;
            var counter = 1;
            while (await _context.Vendors.AnyAsync(v => v.Slug == slug, ct))
            {
                slug = $"{baseSlug}-{counter++}";
            }

            // Tạo hồ sơ Vendor gắn liền 1-1
            var vendor = Vendor.Create(user.Id, request.BrandName, slug, request.FullName, request.PhoneNumber, request.City, request.CommissionRate);

            _context.Users.Add(user);
            _context.Vendors.Add(vendor);
            await _context.SaveChangesAsync(ct);

            var token = _jwtProvider.GenerateToken(user);
            var userDto = new UserDto(user.Id, user.FullName, user.Email, user.PhoneNumber, user.Role.ToString(), user.AvatarUrl, vendor.Id, vendor.BrandName);

            return new AuthResponseDto(token, userDto);
        }, cancellationToken);
    }

    private static string GenerateSlug(string phrase)
    {
        var normalized = phrase.Trim().ToLowerInvariant().Replace('đ', 'd').Normalize(NormalizationForm.FormD);
        var ascii = new string(normalized.Where(ch => CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark).ToArray());
        var slug = Regex.Replace(ascii, @"[^a-z0-9]+", "-").Trim('-');
        // Leave room for a uniqueness suffix, including non-Latin or emoji-only brands.
        if (slug.Length == 0) slug = "doi-tac";
        return slug[..Math.Min(slug.Length, 180)].TrimEnd('-');
    }
}
