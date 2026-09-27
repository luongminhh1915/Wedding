using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;
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
        RuleFor(x => x.FullName).NotEmpty().WithMessage("Họ tên người đại diện không được để trống.");
        RuleFor(x => x.Email).NotEmpty().WithMessage("Email không được để trống.").EmailAddress().WithMessage("Email không hợp lệ.");
        RuleFor(x => x.PhoneNumber).NotEmpty().WithMessage("Hotline không được để trống.");
        RuleFor(x => x.Password).NotEmpty().WithMessage("Mật khẩu không được để trống.").MinimumLength(6);
        RuleFor(x => x.BrandName).NotEmpty().WithMessage("Tên thương hiệu không được để trống.").MaximumLength(200);
        RuleFor(x => x.City).NotEmpty().WithMessage("Thành phố hoạt động không được để trống.");
        RuleFor(x => x.CommissionRate).InclusiveBetween(0.03m, 0.15m).WithMessage("Tỷ lệ hoa hồng đối tác phải nằm trong khoảng 3% đến 15% (FM-001).");
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
        var emailLower = request.Email.Trim().ToLowerInvariant();
        if (await _context.Users.AnyAsync(u => u.Email == emailLower, cancellationToken))
        {
            throw new DomainException("Email đối tác này đã được sử dụng trên hệ thống.");
        }

        // Tạo User chủ với vai trò VendorOwner (BR-001)
        var passwordHash = _passwordHasher.Hash(request.Password);
        var user = User.Create(request.FullName, emailLower, request.PhoneNumber, passwordHash, UserRole.VendorOwner);

        // Tạo Slug cho thương hiệu
        var baseSlug = GenerateSlug(request.BrandName);
        var slug = baseSlug;
        var counter = 1;
        while (await _context.Vendors.AnyAsync(v => v.Slug == slug, cancellationToken))
        {
            slug = $"{baseSlug}-{counter++}";
        }

        // Tạo hồ sơ Vendor gắn liền 1-1
        var vendor = Vendor.Create(user.Id, request.BrandName, slug, request.FullName, request.PhoneNumber, request.City, request.CommissionRate);

        _context.Users.Add(user);
        _context.Vendors.Add(vendor);
        await _context.SaveChangesAsync(cancellationToken);

        var token = _jwtProvider.GenerateToken(user);
        var userDto = new UserDto(user.Id, user.FullName, user.Email, user.PhoneNumber, user.Role.ToString(), user.AvatarUrl, vendor.Id, vendor.BrandName);

        return new AuthResponseDto(token, userDto);
    }

    private static string GenerateSlug(string phrase)
    {
        string str = phrase.ToLowerInvariant();
        str = Regex.Replace(str, @"\s+", "-");
        str = Regex.Replace(str, @"[^a-z0-9\s-]", "");
        return str.Trim('-', ' ');
    }
}
