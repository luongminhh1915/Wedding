using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
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
    // Compatibility input only. Service categories, not signup membership, determine policy.
    List<Guid>? BusinessGroupIds = null,
    bool PartnershipPolicyAccepted = false,
    IReadOnlyList<string>? OperatingRegions = null,
    bool Nationwide = false
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
        RuleFor(x => x).Must(value => OperatingAreaRules.IsValid(value.City, value.OperatingRegions, value.Nationwide, requireArea: true))
            .WithMessage("Chọn Toàn quốc hoặc ít nhất một tỉnh / thành phố trong danh sách 34 địa phương hiện hành.");
        RuleFor(x => x.CommissionRate).Null()
            .WithMessage("Không được tự chọn tỷ lệ hoa hồng khi đăng ký. Chính sách được xác định theo danh mục khi tạo dịch vụ.");
        RuleFor(x => x.PartnershipPolicyAccepted).Equal(true)
            .WithMessage("Vui lòng xác nhận đã hiểu chính sách hợp tác và cách xác định hoa hồng theo danh mục dịch vụ.");
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
            var baseSlug = VendorRegistrationRules.GenerateSlug(request.BrandName);
            var slug = baseSlug;
            var counter = 1;
            while (await _context.Vendors.AnyAsync(v => v.Slug == slug, ct))
            {
                slug = $"{baseSlug}-{counter++}";
            }

            // Tạo hồ sơ Vendor gắn liền 1-1
            VietnamProvinces.TryNormalize(request.City, out var city);
            var vendor = Vendor.CreateWithCategoryDefaults(user.Id, request.BrandName, slug, request.FullName,
                request.PhoneNumber, city, BusinessGroupPolicy.ServiceCategoryRegistrationVersion);
            vendor.SetOperatingAreas(request.OperatingRegions, request.Nationwide, request.City);

            _context.Users.Add(user);
            _context.Vendors.Add(vendor);
            // Old clients may still send BusinessGroupIds. Ignore them rather than
            // fabricating memberships, a group-specific acknowledgement or a rate.
            await _context.SaveChangesAsync(ct);

            var token = _jwtProvider.GenerateToken(user);
            var userDto = new UserDto(user.Id, user.FullName, user.Email, user.PhoneNumber, user.Role.ToString(), user.AvatarUrl, vendor.Id, vendor.BrandName);

            return new AuthResponseDto(token, userDto);
        }, cancellationToken);
    }

}
