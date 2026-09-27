using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Wedding.Application.Common.Interfaces;
using Wedding.Application.Features.Auth.DTOs;
using Wedding.Domain.Entities;
using Wedding.Domain.Enums;
using Wedding.Domain.Exceptions;

namespace Wedding.Application.Features.Auth.Commands;

public record RegisterCustomerCommand(
    string FullName,
    string Email,
    string PhoneNumber,
    string Password
) : IRequest<AuthResponseDto>;

public class RegisterCustomerCommandValidator : AbstractValidator<RegisterCustomerCommand>
{
    public RegisterCustomerCommandValidator()
    {
        RuleFor(x => x.FullName).NotEmpty().WithMessage("Họ và tên không được để trống.").MaximumLength(150);
        RuleFor(x => x.Email).NotEmpty().WithMessage("Email không được để trống.").EmailAddress().WithMessage("Email không hợp lệ.");
        RuleFor(x => x.PhoneNumber).NotEmpty().WithMessage("Số điện thoại không được để trống.").Matches(@"^0\d{9}$").WithMessage("Số điện thoại không đúng định dạng (10 chữ số bắt đầu bằng 0).");
        RuleFor(x => x.Password).NotEmpty().WithMessage("Mật khẩu không được để trống.").MinimumLength(6).WithMessage("Mật khẩu tối thiểu 6 ký tự.");
    }
}

public class RegisterCustomerCommandHandler : IRequestHandler<RegisterCustomerCommand, AuthResponseDto>
{
    private readonly IApplicationDbContext _context;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtProvider _jwtProvider;

    public RegisterCustomerCommandHandler(
        IApplicationDbContext context,
        IPasswordHasher passwordHasher,
        IJwtProvider jwtProvider)
    {
        _context = context;
        _passwordHasher = passwordHasher;
        _jwtProvider = jwtProvider;
    }

    public async Task<AuthResponseDto> Handle(RegisterCustomerCommand request, CancellationToken cancellationToken)
    {
        var emailLower = request.Email.Trim().ToLowerInvariant();
        var exists = await _context.Users.AnyAsync(u => u.Email == emailLower, cancellationToken);
        if (exists)
        {
            throw new DomainException("Email này đã được sử dụng trên hệ thống.");
        }

        var passwordHash = _passwordHasher.Hash(request.Password);
        var user = User.Create(request.FullName, emailLower, request.PhoneNumber, passwordHash, UserRole.Customer);

        _context.Users.Add(user);
        await _context.SaveChangesAsync(cancellationToken);

        var token = _jwtProvider.GenerateToken(user);
        var userDto = new UserDto(user.Id, user.FullName, user.Email, user.PhoneNumber, user.Role.ToString(), user.AvatarUrl, null, null);

        return new AuthResponseDto(token, userDto);
    }
}
