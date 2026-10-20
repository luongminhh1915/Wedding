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
    decimal? CommissionRate = null,
    List<Guid>? BusinessGroupIds = null,
    bool PartnershipPolicyAccepted = false
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
        RuleFor(x => x.City).Must(value => AuthInputRules.RequiredText(value, 100) && VietnamProvinces.TryNormalize(value, out _))
            .WithMessage("Chọn tỉnh / thành phố thuộc danh sách 34 địa phương hiện hành.");
        RuleFor(x => x.CommissionRate).Null()
            .WithMessage("Không được tự chọn tỷ lệ hoa hồng khi đăng ký. Hãy tải lại trang và chọn nhóm dịch vụ theo chính sách hiện hành.");
        RuleFor(x => x.BusinessGroupIds).Must(ids => ids is { Count: > 0 and <= 20 } &&
            ids.All(id => id != Guid.Empty) && ids.Distinct().Count() == ids.Count)
            .WithMessage("Chọn ít nhất một nhóm dịch vụ hợp lệ, không chọn trùng nhóm.");
        RuleFor(x => x.PartnershipPolicyAccepted).Equal(true)
            .WithMessage("Vui lòng xác nhận đã đọc chính sách hợp tác và hoa hồng theo nhóm dịch vụ.");
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

            // The client chooses business scope, never rates or publication/approval state.
            var selectedIds = request.BusinessGroupIds!;
            var groups = await _context.BusinessGroups.AsNoTracking()
                .Where(group => selectedIds.Contains(group.Id) && group.IsActive).ToListAsync(ct);
            if (groups.Count != selectedIds.Count)
                throw new DomainException("Có nhóm dịch vụ không tồn tại hoặc đã ngừng tiếp nhận. Hãy tải lại chính sách và chọn lại.");
            var policyVersions = groups.Select(group => group.PolicyVersion).Distinct().ToList();
            if (policyVersions.Count != 1 || string.IsNullOrWhiteSpace(policyVersions[0]))
                throw new DomainException("Chính sách nhóm dịch vụ chưa đồng bộ. Vui lòng liên hệ nền tảng trước khi đăng ký.");

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
            VietnamProvinces.TryNormalize(request.City, out var city);
            var vendor = Vendor.CreateWithCategoryDefaults(user.Id, request.BrandName, slug, request.FullName,
                request.PhoneNumber, city, policyVersions[0]);

            _context.Users.Add(user);
            _context.Vendors.Add(vendor);
            foreach (var group in groups)
                _context.VendorBusinessGroups.Add(VendorBusinessGroup.Create(vendor.Id, group.Id));
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
