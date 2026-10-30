using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Wedding.Application.Common.Interfaces;
using Wedding.Application.Features.Auth.DTOs;
using Wedding.Domain.Exceptions;
using Wedding.Domain.Entities;
using Wedding.Domain.Enums;

namespace Wedding.Application.Features.Auth.Commands;

public record LoginCommand(
    string Email,
    string Password
) : IRequest<AuthResponseDto>
{
    public override string ToString() => "LoginCommand { [REDACTED] }";
}

public class LoginCommandValidator : AbstractValidator<LoginCommand>
{
    public LoginCommandValidator()
    {
        RuleFor(x => x.Email).Must(value => AuthInputRules.IsEmail(value) || User.IsValidLoginName(AuthInputRules.Email(value)))
            .WithMessage("Nhập email hoặc tên đăng nhập hợp lệ.");
        RuleFor(x => x.Password).NotEmpty().WithMessage("Mật khẩu không được để trống.");
    }
}

public class LoginCommandHandler : IRequestHandler<LoginCommand, AuthResponseDto>
{
    private readonly IApplicationDbContext _context;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtProvider _jwtProvider;

    public LoginCommandHandler(
        IApplicationDbContext context,
        IPasswordHasher passwordHasher,
        IJwtProvider jwtProvider)
    {
        _context = context;
        _passwordHasher = passwordHasher;
        _jwtProvider = jwtProvider;
    }

    public async Task<AuthResponseDto> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        var validation = new LoginCommandValidator().Validate(request);
        if (!validation.IsValid) throw new DomainException(validation.Errors[0].ErrorMessage);
        var identifier = AuthInputRules.Email(request.Email);
        var isEmail = AuthInputRules.IsEmail(identifier);
        var user = await _context.Users
            .Include(u => u.Vendor)
            .FirstOrDefaultAsync(u => isEmail ? u.Email == identifier : u.Role == UserRole.VendorOwner && u.LoginName == identifier, cancellationToken);

        if (user == null || !_passwordHasher.Verify(request.Password, user.PasswordHash))
        {
            throw new DomainException("Email / tên đăng nhập hoặc mật khẩu không chính xác.");
        }

        if (!user.IsActive)
        {
            throw new DomainException("Tài khoản của bạn đang bị tạm khóa. Vui lòng liên hệ hỗ trợ.");
        }

        user.RecordLogin();
        await _context.SaveChangesAsync(cancellationToken);

        var token = _jwtProvider.GenerateToken(user);
        var userDto = new UserDto(
            user.Id, 
            user.FullName, 
            user.Email, 
            user.PhoneNumber, 
            user.Role.ToString(), 
            user.AvatarUrl, 
            user.Vendor?.Id, 
            user.Vendor?.BrandName,
            user.LoginName
        );

        return new AuthResponseDto(token, userDto);
    }
}
