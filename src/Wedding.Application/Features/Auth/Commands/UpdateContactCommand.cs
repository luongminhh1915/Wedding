using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Wedding.Application.Common.Interfaces;
using Wedding.Application.Features.Auth.DTOs;
using Wedding.Domain.Entities;
using Wedding.Domain.Enums;
using Wedding.Domain.Exceptions;

namespace Wedding.Application.Features.Auth.Commands;

/// <summary>Updates only the signed-in customer's contact number, not their identity or wedding profile.</summary>
public record UpdateContactCommand(string PhoneNumber) : IRequest<AuthResponseDto>
{
    public override string ToString() => "UpdateContactCommand { [REDACTED] }";
}

public class UpdateContactCommandValidator : AbstractValidator<UpdateContactCommand>
{
    public UpdateContactCommandValidator() => RuleFor(x => x.PhoneNumber).Phone();
}

public class UpdateContactCommandHandler : IRequestHandler<UpdateContactCommand, AuthResponseDto>
{
    private readonly IApplicationDbContext context;
    private readonly ICurrentUserService current;
    private readonly IJwtProvider jwt;

    public UpdateContactCommandHandler(IApplicationDbContext context, ICurrentUserService current, IJwtProvider jwt)
    {
        this.context = context;
        this.current = current;
        this.jwt = jwt;
    }

    public async Task<AuthResponseDto> Handle(UpdateContactCommand request, CancellationToken cancellationToken)
    {
        var validation = new UpdateContactCommandValidator().Validate(request);
        if (!validation.IsValid) throw new DomainException(validation.Errors[0].ErrorMessage);
        if (current.UserId is not Guid userId)
            throw new DomainException("Vui lòng đăng nhập lại để cập nhật số điện thoại liên hệ.");

        var user = await context.ExecuteSerializableAsync(async ct =>
        {
            var customer = await context.Users.SingleOrDefaultAsync(u => u.Id == userId, ct);
            if (customer is not { IsActive: true, Role: UserRole.Customer })
                throw new DomainException("Chỉ tài khoản khách hàng đang hoạt động được cập nhật số điện thoại liên hệ.");

            var previousVersion = customer.SecurityVersion;
            customer.UpdateContactPhone(request.PhoneNumber);
            if (customer.SecurityVersion != previousVersion)
                context.AuditLogs.Add(AuditLog.Create(customer.Id, customer.Role.ToString(), "Auth.UpdateContact", "User", customer.Id.ToString()));
            await context.SaveChangesAsync(ct);
            return customer;
        }, cancellationToken);

        // Issue the replacement session only after the contact change and its audit are committed.
        return new AuthResponseDto(jwt.GenerateToken(user), new UserDto(user.Id, user.FullName, user.Email,
            user.PhoneNumber, user.Role.ToString(), user.AvatarUrl, null, null));
    }
}
