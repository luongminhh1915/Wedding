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
        RuleFor(x => x.FullName).Must(value => AuthInputRules.RequiredText(value, 150)).WithMessage("Họ và tên cần từ 1 đến 150 ký tự.");
        RuleFor(x => x.Email).ValidEmail();
        RuleFor(x => x.PhoneNumber).Phone();
        RuleFor(x => x.Password).NewPassword();
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
        var validation = new RegisterCustomerCommandValidator().Validate(request);
        if (!validation.IsValid) throw new DomainException(validation.Errors[0].ErrorMessage);
        var emailLower = AuthInputRules.Email(request.Email);
        var passwordHash = _passwordHasher.Hash(request.Password);
        return await _context.ExecuteSerializableAsync(async ct =>
        {
            if (await _context.Users.AnyAsync(u => u.Email == emailLower, ct))
                throw new DomainException("Email này đã được sử dụng trên hệ thống.");
            var user = User.Create(request.FullName, emailLower, request.PhoneNumber, passwordHash, UserRole.Customer);

            _context.Users.Add(user);
            await _context.SaveChangesAsync(ct);

            var token = _jwtProvider.GenerateToken(user);
            var userDto = new UserDto(user.Id, user.FullName, user.Email, user.PhoneNumber, user.Role.ToString(), user.AvatarUrl, null, null);

            return new AuthResponseDto(token, userDto);
        }, cancellationToken);
    }
}
