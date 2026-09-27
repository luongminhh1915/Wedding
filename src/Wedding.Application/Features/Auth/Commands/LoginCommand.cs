using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Wedding.Application.Common.Interfaces;
using Wedding.Application.Features.Auth.DTOs;
using Wedding.Domain.Exceptions;

namespace Wedding.Application.Features.Auth.Commands;

public record LoginCommand(
    string Email,
    string Password
) : IRequest<AuthResponseDto>;

public class LoginCommandValidator : AbstractValidator<LoginCommand>
{
    public LoginCommandValidator()
    {
        RuleFor(x => x.Email).NotEmpty().WithMessage("Email không được để trống.").EmailAddress().WithMessage("Email không hợp lệ.");
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
        var emailLower = request.Email.Trim().ToLowerInvariant();
        var user = await _context.Users
            .Include(u => u.Vendor)
            .FirstOrDefaultAsync(u => u.Email == emailLower, cancellationToken);

        if (user == null || !_passwordHasher.Verify(request.Password, user.PasswordHash))
        {
            throw new DomainException("Email hoặc mật khẩu không chính xác.");
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
            user.Vendor?.BrandName
        );

        return new AuthResponseDto(token, userDto);
    }
}
